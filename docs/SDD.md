# SDD — Sistema de Gestão de Pagamentos de Dívidas (WPF)

## 1. Visão Geral
Aplicação desktop WPF (.NET 10) para substituir o controle de dívidas hoje feito no papel. Gerencia devedores, dívidas (avulsas e parceladas), parcelas, pagamentos (totais e parciais), notificações via WhatsApp (`wa.me`) e relatórios. Uso individual, sem autenticação. Dados em PostgreSQL local.

## 2. Objetivos
- Registrar devedores com nome, telefone (WhatsApp) e e-mail opcional.
- Registrar dívidas avulsas e parceladas com valor, vencimentos e observações.
- Controlar status de parcelas: pendentes, pagas parcialmente, pagas e atrasadas.
- Registrar pagamentos totais ou parciais por parcela.
- Avisar o devedor (lembrete e atraso) via WhatsApp, sempre com confirmação do usuário.
- Avisar o usuário, dentro do app, quando um pagamento for registrado.
- Exibir relatórios: resumo financeiro, parcelas em atraso e histórico de pagamentos.

## 3. Escopo
### Incluído
- CRUD de devedores e dívidas.
- Geração automática de parcelas ao criar dívida.
- Registro e exclusão de pagamentos.
- Fila de notificações com envio individual e envio em lote dos atrasados.
- Dashboard com resumo e lista de atrasados.
- Configurações de notificação.

### Fora de escopo
- Autenticação/autorização e multiusuário.
- Cálculo de juros ou multa por atraso (atraso é apenas informativo).
- Envio automático de WhatsApp por API (Cloud API, Twilio etc.). Pode ser avaliado numa fase futura.
- Notificação externa (WhatsApp/push) ao usuário quando um pagamento é registrado: apenas aviso dentro do app.
- Aplicação web / API REST.
- Exportação e impressão (fase futura).

## 4. Arquitetura

### 4.1 Projetos da solution (Clean Architecture)
| Projeto | Responsabilidade |
|---|---|
| `GestaoPagamentos.Domain` | Entidades (`IEntity<TId>`, `Entity<TId>`), enums e regras de domínio. Sem dependências. |
| `GestaoPagamentos.Application` | Casos de uso (`...UseCase`, sem MediatR), `Result`, abstrações (`IRepository<T,TId>`, repositórios por feature, `IUnitOfWork`, `IClock`, `ILauncher`, `IRelatorioQuery`). Referencia o Domain. |
| `GestaoPagamentos.Infrastructure` | EF Core + Npgsql: `AppDbContext`, mapeamentos, repositórios, `UnitOfWork`, migrações, `AddInfrastructure`. Referencia a Application. |
| `GestaoPagamentos.Wpf` | Views, ViewModels (CommunityToolkit.Mvvm), navegação, hosted service, `ProcessLauncher`. Referencia a Application e, no composition root, a Infrastructure. |
| `GestaoPagamentos.Tests` | Projeto único de testes (xUnit), pastas por camada e feature, com fakes em memória. |

### 4.2 Dependências
```
Wpf ──► Application ──► Domain
Infrastructure ──► Application
Wpf ──► Infrastructure   (somente App.xaml.cs, chamando AddInfrastructure)
Tests ──► Application, Domain
```

### 4.3 Vertical slice por feature
Features: `Devedores`, `Dividas`, `Pagamentos`, `Notificacoes`, `Relatorios`, `Configuracoes`, mais `Common` para o compartilhado. Cada projeto organiza as pastas por feature (não por tipo técnico). Namespaces seguem as pastas. Uma feature usa outra apenas por suas interfaces ou casos de uso públicos.

### 4.4 Entidades e Ids
- `IEntity<TId>` e `Entity<TId>` em `Domain/Common`, com igualdade por Id.
- Id é `Guid` gerado no domínio com `Guid.CreateVersion7()`.
- `Divida` é a raiz do agregado, contendo `Parcela` e `Pagamento`.

### 4.5 Casos de uso
Classes `...UseCase` com `ExecuteAsync`, recebendo um `record` de comando/consulta e retornando `Result`/`Result<T>`. Injetados direto nos ViewModels. Gravação transacional via `IUnitOfWork.SaveChangesAsync`. Tempo via `IClock`.

### 4.6 Tecnologias
- .NET 10, WPF, MVVM com CommunityToolkit.Mvvm.
- Microsoft.Extensions.DependencyInjection / Hosting / Configuration.
- EF Core + Npgsql (PostgreSQL), migrações via `dotnet ef`.
- Cultura pt-BR.

## 5. Modelo de Domínio

### Devedor
| Campo | Tipo | Observação |
|---|---|---|
| Id | Guid | PK (v7, gerado no domínio) |
| Nome | string | obrigatório |
| Telefone | string | WhatsApp; normalizado com DDI 55 |
| Email | string | opcional |
| Observacao | string | opcional |

### Divida
| Campo | Tipo | Observação |
|---|---|---|
| Id | Guid | PK (v7, gerado no domínio) |
| DevedorId | Guid | FK |
| Descricao | string | obrigatório |
| ValorTotal | decimal | > 0 |
| DataPrimeiroVencimento | date | base para gerar parcelas |
| Tipo | enum | Avulsa, Parcelada |
| QuantidadeParcelas | int | 1 para avulsa |
| Status | enum | Ativa, Quitada, Cancelada |
| Observacao | string | opcional |

### Parcela
| Campo | Tipo | Observação |
|---|---|---|
| Id | Guid | PK (v7, gerado no domínio) |
| DividaId | Guid | FK |
| Numero | int | sequencial a partir de 1 |
| ValorPrevisto | decimal | |
| DataVencimento | date | |
| Pagamentos | lista | |

Status da parcela é **calculado** (não persistido):
- `Paga`: soma dos pagamentos >= ValorPrevisto
- `PagaParcial`: 0 < soma < ValorPrevisto e não vencida
- `Atrasada`: soma < ValorPrevisto e DataVencimento < hoje
- `Pendente`: demais casos

### Pagamento
| Campo | Tipo | Observação |
|---|---|---|
| Id | Guid | PK (v7, gerado no domínio) |
| ParcelaId | Guid | FK |
| Valor | decimal | > 0 |
| DataPagamento | date | |
| Observacao | string | opcional |

### Notificacao
| Campo | Tipo | Observação |
|---|---|---|
| Id | Guid | PK (v7, gerado no domínio) |
| DevedorId | Guid | FK |
| ParcelaId | Guid | FK (nulo para PagamentoRecebido de outro contexto, se aplicável) |
| Tipo | enum | Lembrete, Atraso, PagamentoRecebido |
| Mensagem | string | texto gerado a partir do template, editável |
| DataGeracao | datetime | |
| DataEnvio | datetime? | preenchida ao abrir o link |
| Status | enum | Pendente, Enviada, Ignorada, Descartada |

### Configuracao
| Campo | Tipo | Observação |
|---|---|---|
| DiasAntecedenciaLembrete | int | valor padrão a definir |
| IntervaloEnvioLoteMs | int | padrão 1500 |
| TemplateLembrete | string | placeholders: `{Nome}`, `{Valor}`, `{Vencimento}`, `{Descricao}` |
| TemplateAtraso | string | mesmos placeholders |

## 6. Regras de Negócio
1. Dívida **Avulsa** gera exatamente 1 parcela com o valor total.
2. Dívida **Parcelada** gera N parcelas mensais consecutivas a partir do primeiro vencimento. A última parcela absorve a diferença de arredondamento.
3. Pagamento parcial é permitido; a soma dos pagamentos de uma parcela não pode exceder o ValorPrevisto.
4. Dívida passa a **Quitada** quando todas as parcelas estiverem pagas.
5. Dívida **Cancelada** não aceita novos pagamentos.
6. Excluir pagamento recalcula o status da parcela e da dívida.
7. Não há juros/multa: atraso é apenas informativo.
8. Devedor com dívidas ativas não pode ser excluído.

## 7. Notificações

### 7.1 Princípios
- O envio ao devedor é feito por link `https://wa.me/<telefone>?text=<mensagem codificada>`.
- **O app nunca abre o WhatsApp sem ação explícita do usuário.** A geração da fila é automática; o envio exige confirmação.
- O app não confirma entrega: o usuário ainda toca em enviar no WhatsApp. O item é marcado como `Enviada` ao abrir o link.

### 7.2 Geração da fila
- Executada pelo hosted service ao iniciar o app e periodicamente (ex.: a cada hora).
- **Lembrete**: parcela não quitada cujo vencimento ocorre em até `DiasAntecedenciaLembrete` dias.
- **Atraso**: parcela não quitada, 1 dia após o vencimento.
- Sem duplicidade por (ParcelaId, Tipo). Itens `Ignorada` não são regenerados.
- Parcela quitada ou dívida cancelada: notificações `Pendente` dela passam a `Descartada`.

### 7.3 Envio individual
Tela de Notificações lista as pendentes (devedor, parcela, valor, mensagem editável). Botões **Enviar** (abre `wa.me` e marca `Enviada`) e **Ignorar**.

### 7.4 Enviar todas os atrasados
- Botão disponível na tela de Notificações; vale apenas para `Atraso` com status `Pendente`. Lembretes são sempre individuais.
- Abre diálogo de confirmação listando devedor, valor e telefone. Itens sem telefone válido aparecem destacados e são pulados.
- Ao confirmar, abre os links um a um com intervalo `IntervaloEnvioLoteMs`, exibindo progresso (ex.: 3 de 10) e permitindo cancelar. Usa a mensagem atual de cada item (inclusive editada).
- Alternativa prevista caso a abertura sequencial seja problemática: botão **Próximo** que abre uma conversa por vez.

### 7.5 Pagamento recebido
Ao registrar um pagamento, o app exibe um toast e registra uma notificação informativa (`PagamentoRecebido`) no histórico. Não há envio externo.

### 7.6 Indicadores
Selo com o número de pendências no menu e aviso ao abrir o app.

### 7.7 Telefone
Normalização: remover caracteres não numéricos e prefixar `55` quando ausente. Telefone inválido bloqueia o envio daquele item.

## 8. Telas (WPF)
1. **Dashboard**: cards (total a receber, recebido, em atraso) e lista de parcelas atrasadas.
2. **Devedores**: listagem, cadastro, edição e exclusão.
3. **Dívidas**: listagem com filtros por status/devedor e cadastro de dívida avulsa/parcelada.
4. **Detalhe da Dívida**: parcelas com status, registrar e excluir pagamento (total/parcial), histórico.
5. **Notificações**: fila, envio individual, **Enviar todas os atrasados**, histórico.
6. **Configurações**: antecedência do lembrete, intervalo do envio em lote e templates.

Navegação lateral com selo de pendências de notificação.

## 9. Relatórios
- Resumo: total a receber, recebido, em atraso, contagem por status.
- Parcelas atrasadas com devedor e telefone.
- Histórico de pagamentos por dívida e por devedor.
Consultas implementadas via `IRelatorioQuery` (projeções na Infra).

## 10. Configuração
- `appsettings.json` no app WPF, com `ConnectionStrings:DefaultConnection`. Exemplo:
  `Host=localhost;Port=5432;Database=gestao_pagamentos;Username=postgres;Password=postgres`
- Sobrescrita por variável de ambiente `ConnectionStrings__DefaultConnection`.
- Migrações aplicadas automaticamente no startup.

## 11. Banco de Dados
- PostgreSQL local, acessado via EF Core (Npgsql) exclusivamente pela camada Infra.
`GestaoPagamentos.Infrastructure`.

## 12. Backup e Distribuição

### 12.1 Backup do PostgreSQL
- Backup: `pg_dump -h localhost -U postgres -Fc -f gestao_pagamentos.backup gestao_pagamentos`
- Restauração: `pg_restore -h localhost -U postgres -d gestao_pagamentos --clean gestao_pagamentos.backup`
- Recomenda-se agendar o backup diário (Agendador de Tarefas do Windows) e guardar uma cópia fora do computador.

### 12.2 Empacotamento
- `dotnet publish GestaoPagamentos.Wpf -c Release -r win-x64 --self-contained -o publish`
- O `appsettings.json` ao lado do executável contém a connection string; ajustar usuário/senha do PostgreSQL local.
- As migrações são aplicadas automaticamente na inicialização do app.

## 13. Pontos em Aberto
- Comportamento do envio em lote caso a abertura sequencial de conversas seja instável.
- Agendamento automático do backup.
- Testes de casos de uso com repositórios em memória e testes de integração com PostgreSQL.
