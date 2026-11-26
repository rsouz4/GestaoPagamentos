# Spec: Migração para Supabase e versão para celular

Versão ajustada em 25/11/2026, a partir da spec de 18/11/2026 e do código atual do repositório.

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
- Manter o envio automático de cobranças no WPF, com Playwright. O PC liga só para processar a fila (decisão C); não precisa ficar ligado o tempo todo.
- Manter o custo em R$ 0 com planos gratuitos.
- Celular como PWA instalada pelo Safari (iPhone), sem lojas.

## Escopo da v1

**Entra:** cadastro de devedores e dívidas com parcelas; pagamentos (parcial/total) com multa e juros como hoje; renovação com juros; vencimentos e atrasos; notificações e cobrança por WhatsApp (automática no WPF, wa.me manual); configurações; login por e-mail e senha para uma conta, cadastro desativado; exportação CSV.

**Fica fora:** envio automático pelo celular (o Playwright não roda no iPhone); offline com sincronização; publicação nas lojas; vários usuários.

**Ideias para depois da v1** (para cobrar sem depender do PC):
- E-mail automático por função agendada no Supabase (o devedor já tem o campo `email`), sem risco de bloqueio; checar os limites do plano gratuito do serviço de e-mail.
- Notificação na PWA para lembrar o próprio usuário das cobranças pendentes (iOS 16.4 ou superior, com a PWA instalada na tela inicial).
- API oficial do WhatsApp Business: automática e sem PC, mas paga por mensagem e com verificação da conta; checar preços e se a política da Meta permite cobrança de dívida.

## Arquitetura

- Solution atual (Clean Architecture): `Domain`, `Application`, `Infrastructure`, `Wpf`, `Tests`. `Domain` é `net10.0` sem dependência de WPF, então **pode ser reaproveitado por uma PWA Blazor WebAssembly**, junto com as regras de multa, juros e status.
- Dados no Supabase (PostgreSQL + Auth), isolados por Row Level Security (`user_id`).
- Sem API própria: os clientes falam direto com o Supabase usando só a chave pública (anon).

## Modelo de dados

Definido em [`db/supabase/01_schema.sql`](../db/supabase/01_schema.sql), espelhando o EF Core atual em snake_case: `devedores`, `dividas`, `parcelas`, `pagamentos`, `notificacoes`, `configuracoes`. Ids `uuid` (os `Guid` v7 do app), valores `numeric(18,2)`, enums como `smallint` com os mesmos valores do C#. Toda tabela tem `user_id` com RLS.

## Decisões em aberto

- **A. Interface do celular.** Opções: (1) PWA Blazor WASM nova, reaproveitando `Domain` e `Application` e reescrevendo as telas em Razor, mantendo o WPF em XAML (proposta); (2) migrar também o WPF para Blazor Hybrid, o que é reescrever as duas UIs; (3) app nativo, que exige Mac e conta Apple (US$ 99/ano). A spec original assumia Blazor já existente, o que não é verdade.
- **D. Risco de bloqueio do número** pela automação do WhatsApp. A automação segue ativa com intervalo de 40 s, mas o botão manual (wa.me) continua como alternativa e é a opção sem risco. Pendente: decidir se a automação fica ligada por padrão ou opcional, e definir um limite diário de envios.

## Decisões tomadas

- **B. Como o WPF acessa o Supabase (25/11/2026):** pela biblioteca cliente do Supabase (a API REST automática, PostgREST), com login e RLS. Fica descartado ligar o EF Core/Npgsql direto ao Postgres: exigiria a senha do banco no computador, ignoraria Auth/RLS e não funcionaria na PWA. O acesso a dados passa a ser uma interface (`IUnitOfWorkFactory`), com o EF Core/SQLite como implementação atual e uma implementação Supabase a escrever no passo 3.

- **C. Uso do computador (25/11/2026):** o PC liga só para processar a fila de cobranças. Ao abrir, o app gera a fila e envia as notificações pendentes de atraso e de vencimento (`NotificacaoAutoEnvioService`). Como os dados ficam no Supabase, o que for lançado pelo celular entra na fila na próxima abertura; não é preciso um worker com comandos na v1. Lançamentos feitos pelo celular podem ficar sem cobrança até o PC ser aberto.
- **Intervalo do envio automático (25/11/2026):** 40 segundos entre mensagens, configurável em `WhatsAppAutomation:IntervaloEntreEnvios` no `appsettings.json` (antes era um atraso fixo de 2 s no código). O `IntervaloEnvioLoteMs` das configurações vale só para o lote manual (wa.me).

## Requisitos não funcionais e segurança

| Tema | Requisito |
| --- | --- |
| Login | E-mail e senha pelo Supabase Auth, conta única, cadastro de novos usuários desativado. |
| Isolamento | RLS em todas as tabelas: `user_id = auth.uid()`. |
| Chaves | Só a chave anon nos apps; a chave de serviço nunca vai para o código nem para o repositório. |
| Internet | Sem conexão, o app avisa e não permite lançar. |
| WhatsApp | Playwright só no WPF; automatizar o WhatsApp Web pode violar os termos e bloquear o número: poucas mensagens, com 40 s entre elas (`WhatsAppAutomation:IntervaloEntreEnvios`). Recomendável usar um número secundário só para cobrança e enviar só a contatos que já conversaram com você. |
| Backup | CSV pelo app e cópia periódica do banco (o plano gratuito não garante backups). |
| Dados reais | Arquivos gerados na importação (`db/import/`) e cópias do `.db` ficam fora do git. |
| Limites | Projetos inativos no plano gratuito podem ser pausados; conferir limites vigentes antes de publicar. |

## Plano de migração

1. **Copiar o SQLite** (`gestao_pagamentos.db`) para um local seguro e criar o projeto no Supabase. Rodar `db/supabase/01_schema.sql`. Antes: desligar o cadastro de usuários e criar a conta única. *(Script pronto.)*
2. **Isolar o acesso a dados atrás de interfaces.** *(Feito em 25/11/2026, a conferir no uso real do app.)* Os serviços saíram do projeto `Wpf` para `Application` (onde a PWA também poderá usá-los) e passaram a usar `IUnitOfWorkFactory` e os repositórios de `Application/Persistence`. A implementação EF Core/SQLite está em `Infrastructure/Persistence/EfUnitOfWork.cs`. Antes da troca foram escritos 75 testes dos serviços (SQLite em memória); os mesmos testes passam depois dela. Sem mudança de comportamento prevista.
3. **Implementar `IUnitOfWorkFactory` com o Supabase** (login, leitura, gravação) e usar no WPF no lugar do SQLite. Andamento: **etapa 1 (base e login) feita em 25/11/2026** — pacote `Supabase` 8.14.0 (verificado na documentação oficial e no NuGet), `IAuthService` com implementação Supabase, tela de login no WPF, opção "Manter conectado" (sessão guardada cifrada pelo DPAPI do Windows em `%LOCALAPPDATA%\GestaoPagamentos\sessao.bin`) e botão Sair. O modo de login é escolhido em `Autenticacao:Modo` (`Nenhuma` ou `Supabase`), e a URL e a chave pública ficam em `%LOCALAPPDATA%\GestaoPagamentos\appsettings.Local.json`, fora do repositório. Nesta etapa os dados continuam no SQLite; o login já protege a abertura do app. Faltam: leitura (etapa 2), gravação (etapa 3) e a troca com validação (etapa 4). Pontos a resolver: (a) o EF acompanha as mudanças das entidades; o cliente do Supabase não, então a implementação precisa guardar um retrato do que foi lido e gravar a diferença em `SalvarAsync`; (b) operações como registrar pagamento mexem em várias tabelas de uma vez (pagamento, notificações, status), e a API REST não tem transação entre tabelas, então essas gravações devem virar funções SQL (RPC) no Supabase para continuarem atômicas; (c) o startup deixa de aplicar migrações do SQLite.
4. **Importar os dados:** `dotnet run db/tools/ExportarSqlite.cs -- <db> <user_id> db/import/02_import.sql`, revisar e rodar o `.sql` no SQL Editor; conferir os totais por devedor com a consulta ao final do arquivo. *(Script pronto.)*
5. **PWA Blazor WebAssembly** reaproveitando `Domain`/`Application`, hospedada em site estático e instalada no iPhone. Depende da decisão A. Testar cedo no iPhone.
   - Desenvolvimento: DevTools do Chrome/Edge no modo dispositivo, Playwright com WebKit e perfil iPhone como checagem extra. Simulador de iOS só existe em Mac.
   - Validar num iPhone real a cada etapa (instalação pela tela inicial, áreas seguras, sessão de login, link wa.me abrindo o WhatsApp). HTTPS é obrigatório para instalar a PWA, então publicar cedo em Cloudflare Pages ou GitHub Pages, ou usar um túnel para o servidor local.
6. **Adaptar o fluxo de envio do Playwright** existente (geração da fila e envio ao abrir o app) para ler `notificacoes`/`parcelas` e gravar o resultado no Supabase, conforme a decisão C.

**Risco principal:** a importação. Dados atuais (cópia de 19/11/2026): 5 devedores, 25 dívidas, 31 parcelas, 6 pagamentos, 41 notificações, 1 configuração. É pouco volume; a conferência por devedor é viável a olho.

## Próximos passos

- Passo 3: escrever a implementação Supabase (item B decidido). A decisão A (interface do celular) só trava o passo 5, e a D não bloqueia nada agora.
- Criar o projeto Supabase, rodar o passo 1 e criar o usuário.
- Gerar e rodar a importação (passo 4) com o `user_id` real.
