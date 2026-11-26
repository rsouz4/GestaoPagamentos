# Gestão de Pagamentos

Aplicação desktop Windows para controle de empréstimos, parcelas, pagamentos e cobranças via WhatsApp.

## Stack

| Camada | Tecnologia |
| --- | --- |
| Runtime | .NET 10 (`net10.0` / `net10.0-windows`) |
| UI | WPF + MVVM (CommunityToolkit.Mvvm 8.4) |
| Host / DI / Logging | Microsoft.Extensions.Hosting, DependencyInjection, Logging (provider em memória próprio) |
| Persistência | EF Core 10 + SQLite (`%LOCALAPPDATA%\GestaoPagamentos\gestao_pagamentos.db`) |
| Automação WhatsApp | Microsoft.Playwright 1.64 |
| Testes | xUnit 2.9, Moq 4.21, coverlet |

## Arquitetura

```
GestaoPagamentos.Domain          Entidades, enums e regras de cálculo puras
GestaoPagamentos.Application     Casos de uso e abstrações (IClock, repositórios)
GestaoPagamentos.Infrastructure  EF Core/SQLite, migrations, integração Playwright
GestaoPagamentos.Wpf             Shell, Views/ViewModels por feature, composição do Host
GestaoPagamentos.Tests           Testes unitários
```

Telas: **Dashboard**, **Devedores**, **Dívidas**, **Notificações**, **Logs** e **Configurações**.

## Regras de negócio

### Juros iniciais
- `valor_a_receber = valor_emprestado × 1,30`, aplicado **uma única vez** na criação.
- Valores em centavos; arredondamento a 2 casas. Diferença de centavos vai para a última parcela (ex.: R$ 130,00 / 3 → 43,33 · 43,33 · 43,34).
- Vencimentos mensais a partir do primeiro; se o dia não existir no mês, usa o último dia. Sem ajuste para feriados/fins de semana.

### Multa por atraso
- Atraso a partir do dia seguinte ao vencimento, em dias corridos.
- Multa diária **simples de 15% sobre o valor original da parcela** (já com juros), sem capitalização e sem incidir sobre multas acumuladas.
- Pagamentos parciais **não reduzem a base** da multa.
- Ex.: parcela de R$ 1.300,00 → +R$ 195,00/dia (1.495,00 → 1.690,00 → 1.885,00).
- Percentuais não são editáveis pela UI.

### Pagamentos
- Registrados por parcela; valor > 0 e ≤ saldo na data informada. Sem rateio entre parcelas.
- Ordem de abatimento: **multa acumulada → principal**. Multa do dia é calculada antes do abatimento.
- Exclusão de pagamento recalcula saldos (pode reabrir dívida quitada). Não há estorno bancário.

### Status
| Parcela | Regra |
| --- | --- |
| Pendente | Não vencida e sem pagamento (inclui o dia do vencimento) |
| Paga parcial | Com pagamento, saldo > 0 e não vencida |
| Atrasada | Vencida com saldo > 0 |
| Paga | Saldo zerado |

| Dívida | Regra |
| --- | --- |
| Ativa | Possui parcela em aberto |
| Quitada | Todas as parcelas pagas |
| Cancelada | Bloqueia pagamentos/exclusões, descarta notificações pendentes e sai dos totais do Dashboard. Não reativável |

### Devedores
- Nome obrigatório; telefone com DDD necessário para notificações.
- Exclusão bloqueada se houver dívidas ativas.

### Dashboard
- **A receber**: saldo atualizado (com multas) das dívidas ativas. **Em atraso** é um subconjunto de *A receber*.
- **Recebido**: pagamentos de dívidas não canceladas.

### Notificações (WhatsApp)
- **Lembrete**: parcelas com vencimento entre hoje e `N` dias (padrão 3). **Atraso**: parcelas vencidas.
- Uma notificação por tipo/parcela; gerar atraso descarta lembretes pendentes; quitação descarta pendentes.
- A mensagem é congelada na geração; marcada como enviada ao abrir a conversa (não confirma entrega/leitura).
- Envio em lote com intervalo configurável (padrão 1.500 ms), ignora telefones inválidos; cancelável.

### Configurações
- Antecedência do lembrete, intervalo do lote e modelos de mensagem com marcadores `{Nome}`, `{Valor}`, `{Descricao}`, `{Vencimento}`.
- Alterações não reescrevem mensagens já geradas.

## Execução

```powershell
dotnet build GestaoPagamentos.slnx
dotnet test
dotnet run --project GestaoPagamentos.Wpf
```

## Limitações
- Sem edição de valores/vencimentos após criação, descontos ou taxas individuais.
- Sem backup automático nem sincronização entre máquinas; para backup, feche o app e copie a pasta de dados.
- Pagamentos e envios WhatsApp dependem de confirmação manual.

> Este documento descreve o software e não substitui o contrato com o devedor nem avaliação jurídica das cobranças.
