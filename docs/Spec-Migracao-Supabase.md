# Spec: Migração para Supabase e versão para celular

Versão ajustada em 19/11/2026, a partir da spec de 18/11/2026 e do código atual do repositório.

## O que mudou em relação à spec original

| # | Spec original | O que o código mostra | Efeito |
| --- | --- | --- | --- |
| 1 | "App WPF com interface Blazor" | O WPF é XAML puro (`*.xaml.cs`, ViewModels). Não há `.razor` nem WebView no projeto. | A biblioteca Razor compartilhada (passos 2 e 5) não é um "mover telas": seria **reescrever a UI em Blazor**. Ver decisão A. |
| 2 | 4 tabelas, saldo = valor total − pagamentos | 6 tabelas. O saldo inclui **multa de 15% ao dia** sobre o valor previsto, calculada em C# (`Parcela.Simular`), e depende da data e da ordem dos pagamentos. | O saldo **não** pode ser uma view/soma SQL. Regras de negócio ficam no Domain. |
| 3 | `pagamentos` ligado à dívida, `parcela_id` opcional | `pagamentos` pertence sempre a uma `parcela`; a dívida vem pela parcela. | Mantido como no app. |
| 4 | Dívida só com `valor_total` | `valor_emprestado`, `taxa_juros` (padrão 30%), `tipo` (Avulsa/Parcelada), `data_emprestimo`, renovação com juros. | Campos mantidos. |
| 5 | Status `aberta / quitada / cancelada` | `Ativa / Quitada / Cancelada` (smallint 0/1/2). Quem recalcula é o Domain (`RecalcularStatus`). | Sem trigger de status no banco. |
| 6 | Devedores: "criar, editar, buscar e arquivar" | Não existe arquivar; o app exclui (com cascata). | "Arquivar" sai da v1, ou vira funcionalidade nova. |
| 7 | Playwright "a implementar" no passo 6 | Já existe: `PlaywrightWhatsAppService`, fila de `notificacoes`, tela de logs. | Passo 6 vira **adaptar** o que existe para ler/gravar no Supabase. |
| 8 | Tabelas `notificacoes` e `configuracoes` não citadas | Existem e vão para o Supabase. | Incluídas. |
| 9 | Banco atual "SQLite" | SQLite em `%LOCALAPPDATA%\GestaoPagamentos\gestao_pagamentos.db`. O `docs/SDD.md` ainda descreve PostgreSQL local (desatualizado). | Atualizar o SDD ao fim da migração. |

## Visão geral e objetivos

- Guardar os dados no Supabase e fazer o WPF e o celular lerem e gravarem nele, para mostrarem sempre o mesmo saldo.
- Exigir internet; offline fica para uma versão futura.
- Manter o envio automático de cobranças no WPF, com Playwright.
- Manter o custo em R$ 0 com planos gratuitos.
- Celular como PWA instalada pelo Safari (iPhone), sem lojas.

## Escopo da v1

**Entra:** cadastro de devedores e dívidas com parcelas; pagamentos (parcial/total) com multa e juros como hoje; renovação com juros; vencimentos e atrasos; notificações e cobrança por WhatsApp (automática no WPF, wa.me manual); configurações; login por e-mail e senha para uma conta, cadastro desativado; exportação CSV.

**Fica fora:** envio automático pelo celular; offline com sincronização; publicação nas lojas; vários usuários.

## Arquitetura

- Solution atual (Clean Architecture): `Domain`, `Application`, `Infrastructure`, `Wpf`, `Tests`. `Domain` é `net10.0` sem dependência de WPF, então **pode ser reaproveitado por uma PWA Blazor WebAssembly**, junto com as regras de multa, juros e status.
- Dados no Supabase (PostgreSQL + Auth), isolados por Row Level Security (`user_id`).
- Sem API própria: os clientes falam direto com o Supabase usando só a chave pública (anon).

## Modelo de dados

Definido em [`db/supabase/01_schema.sql`](../db/supabase/01_schema.sql), espelhando o EF Core atual em snake_case: `devedores`, `dividas`, `parcelas`, `pagamentos`, `notificacoes`, `configuracoes`. Ids `uuid` (os `Guid` v7 do app), valores `numeric(18,2)`, enums como `smallint` com os mesmos valores do C#. Toda tabela tem `user_id` com RLS.

## Decisões em aberto

- **A. Interface do celular.** Opções: (1) PWA Blazor WASM nova, reaproveitando `Domain` e `Application` e reescrevendo as telas em Razor, mantendo o WPF em XAML (proposta); (2) migrar também o WPF para Blazor Hybrid, o que é reescrever as duas UIs; (3) app nativo, que exige Mac e conta Apple (US$ 99/ano). A spec original assumia Blazor já existente, o que não é verdade.
- **B. Como o WPF acessa o Supabase.** Hoje os serviços usam `AppDbContext` (EF Core) direto, inclusive na camada `Wpf` (ex.: `DevedorService`). Ligar o EF direto ao Postgres do Supabase exigiria a senha do banco no computador e ignoraria Auth/RLS; usar o cliente do Supabase (PostgREST) respeita o login e o RLS, mas pede interfaces de repositório que ainda não existem.
- **C. Uso do computador para o Playwright:** o PC com o WPF fica ligado nos horários de cobrança?
- **D. Risco de bloqueio do número** pela automação do WhatsApp, ou manter só o botão manual (wa.me).

## Requisitos não funcionais e segurança

| Tema | Requisito |
| --- | --- |
| Login | E-mail e senha pelo Supabase Auth, conta única, cadastro de novos usuários desativado. |
| Isolamento | RLS em todas as tabelas: `user_id = auth.uid()`. |
| Chaves | Só a chave anon nos apps; a chave de serviço nunca vai para o código nem para o repositório. |
| Internet | Sem conexão, o app avisa e não permite lançar. |
| WhatsApp | Playwright só no WPF; automatizar o WhatsApp Web pode violar os termos e bloquear o número: poucas mensagens, com intervalo (`intervalo_envio_lote_ms`). |
| Backup | CSV pelo app e cópia periódica do banco (o plano gratuito não garante backups). |
| Dados reais | Arquivos gerados na importação (`db/import/`) e cópias do `.db` ficam fora do git. |
| Limites | Projetos inativos no plano gratuito podem ser pausados; conferir limites vigentes antes de publicar. |

## Plano de migração

1. **Copiar o SQLite** (`gestao_pagamentos.db`) para um local seguro e criar o projeto no Supabase. Rodar `db/supabase/01_schema.sql`. Antes: desligar o cadastro de usuários e criar a conta única. *(Script pronto.)*
2. **Isolar o acesso a dados atrás de interfaces** (repositórios) e tirar o `AppDbContext` da camada `Wpf`, sem mudar o comportamento. Os testes atuais cobrem só o Domain e o envio de notificações, não os serviços da camada `Wpf`; vale escrever testes antes de mexer neles.
3. **Implementar as interfaces com o Supabase** (login, leitura, gravação) e usar no WPF no lugar do SQLite. Depende da decisão B.
4. **Importar os dados:** `dotnet run db/tools/ExportarSqlite.cs -- <db> <user_id> db/import/02_import.sql`, revisar e rodar o `.sql` no SQL Editor; conferir os totais por devedor com a consulta ao final do arquivo. *(Script pronto.)*
5. **PWA Blazor WebAssembly** reaproveitando `Domain`/`Application`, hospedada em site estático e instalada no iPhone. Depende da decisão A. Testar cedo no iPhone.
6. **Adaptar o worker do Playwright** existente para ler `notificacoes`/`parcelas` e gravar o resultado no Supabase.

**Risco principal:** a importação. Dados atuais (cópia de 19/11/2026): 5 devedores, 25 dívidas, 31 parcelas, 6 pagamentos, 41 notificações, 1 configuração. É pouco volume; a conferência por devedor é viável a olho.

## Próximos passos

- Decidir A e B (as demais perguntas não bloqueiam os passos 1 e 4).
- Criar o projeto Supabase, rodar o passo 1 e criar o usuário.
- Gerar e rodar a importação (passo 4) com o `user_id` real.
