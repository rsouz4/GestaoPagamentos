-- =====================================================================
-- Gestão de Pagamentos — Passo 1: esquema Supabase (PostgreSQL)
-- Espelha fielmente o modelo EF Core atual (GestaoPagamentos.Domain +
-- GestaoPagamentos.Infrastructure/*Configuration.cs), em snake_case.
--
-- Rodar uma vez no SQL Editor, em projeto Supabase novo.
--
-- ANTES de rodar (no painel, não é SQL):
--   Authentication > Sign In / Providers > desligar "Allow new users to sign up"
--   Authentication > Users > "Add user" para criar a sua única conta
--
-- Regras de negócio (multa diária de 15%, status da parcela/dívida,
-- saldo, geração de parcelas) continuam NO DOMÍNIO C#, como hoje.
-- O banco só guarda os dados e isola por usuário (RLS). Por isso não há
-- triggers de status nem views de saldo aqui: a multa depende da data e
-- da ordem dos pagamentos (Parcela.Simular) e não é uma soma simples.
--
-- Enums continuam como smallint, iguais aos valores do C#:
--   dividas.status        0 Ativa | 1 Quitada | 2 Cancelada
--   dividas.tipo          0 Avulsa | 1 Parcelada
--   notificacoes.tipo     0 Lembrete | 1 Atraso | 2 PagamentoRecebido | 3 Vencimento
--   notificacoes.status   0 Pendente | 1 Enviada | 2 Ignorada | 3 Descartada
-- Ids: uuid, gerados pelo app (Guid.CreateVersion7). O default só cobre
-- inserts manuais.
-- =====================================================================

create table public.devedores (
  id          uuid primary key default gen_random_uuid(),
  user_id     uuid not null default auth.uid()
              references auth.users (id) on delete cascade,
  nome        varchar(200) not null check (length(btrim(nome)) > 0),
  telefone    varchar(30),
  email       varchar(200),
  observacao  varchar(1000)
);

create table public.dividas (
  id                       uuid primary key default gen_random_uuid(),
  user_id                  uuid not null default auth.uid()
                           references auth.users (id) on delete cascade,
  devedor_id               uuid not null
                           references public.devedores (id) on delete cascade,
  descricao                varchar(300) not null,
  valor_emprestado         numeric(18,2) not null check (valor_emprestado > 0),
  taxa_juros               numeric(7,4)  not null check (taxa_juros >= 0),
  valor_total              numeric(18,2) not null,
  data_primeiro_vencimento date not null,
  data_emprestimo          date not null,
  tipo                     smallint not null check (tipo in (0, 1)),
  quantidade_parcelas      integer  not null check (quantidade_parcelas >= 1),
  status                   smallint not null check (status in (0, 1, 2)),
  observacao               varchar(1000)
);

create table public.parcelas (
  id              uuid primary key default gen_random_uuid(),
  user_id         uuid not null default auth.uid()
                  references auth.users (id) on delete cascade,
  divida_id       uuid not null
                  references public.dividas (id) on delete cascade,
  numero          integer not null check (numero >= 1),
  valor_previsto  numeric(18,2) not null,
  data_vencimento date not null,
  unique (divida_id, numero)
);

create table public.pagamentos (
  id              uuid primary key default gen_random_uuid(),
  user_id         uuid not null default auth.uid()
                  references auth.users (id) on delete cascade,
  parcela_id      uuid not null
                  references public.parcelas (id) on delete cascade,
  valor           numeric(18,2) not null check (valor > 0),
  data_pagamento  date not null,
  observacao      varchar(500)
);

-- DataGeracao/DataEnvio: o app usa DateTime e a coluna era
-- "timestamp without time zone"; mantido igual para não deslocar horários.
create table public.notificacoes (
  id            uuid primary key default gen_random_uuid(),
  user_id       uuid not null default auth.uid()
                references auth.users (id) on delete cascade,
  devedor_id    uuid not null
                references public.devedores (id) on delete cascade,
  parcela_id    uuid
                references public.parcelas (id) on delete cascade,
  tipo          smallint not null check (tipo in (0, 1, 2, 3)),
  mensagem      varchar(2000) not null,
  data_geracao  timestamp without time zone not null,
  data_envio    timestamp without time zone,
  status        smallint not null check (status in (0, 1, 2, 3))
);

-- Uma linha de configuração por usuário (o app lê/grava uma só).
create table public.configuracoes (
  id                          uuid primary key default gen_random_uuid(),
  user_id                     uuid not null default auth.uid() unique
                              references auth.users (id) on delete cascade,
  dias_antecedencia_lembrete  integer not null check (dias_antecedencia_lembrete >= 0),
  intervalo_envio_lote_ms     integer not null check (intervalo_envio_lote_ms >= 0),
  template_lembrete           text not null,
  template_atraso             text not null
);

-- Índices (espelham os do EF + user_id para o RLS)
create index on public.devedores     (user_id);
create index on public.devedores     (nome);
create index on public.dividas       (user_id);
create index on public.dividas       (devedor_id);
create index on public.dividas       (status);
create index on public.parcelas      (user_id);
create index on public.parcelas      (data_vencimento);
create index on public.pagamentos    (user_id);
create index on public.pagamentos    (parcela_id);
create index on public.notificacoes  (user_id);
create index on public.notificacoes  (devedor_id);
create index on public.notificacoes  (parcela_id, tipo);
create index on public.notificacoes  (status);

-- ---------------------------------------------------------------------
-- Row Level Security: cada linha só é vista/alterada pelo próprio user_id.
-- O papel anon não recebe privilégio nem policy.
-- ---------------------------------------------------------------------
do $$
declare t text;
begin
  foreach t in array array[
    'devedores', 'dividas', 'parcelas', 'pagamentos', 'notificacoes', 'configuracoes'
  ]
  loop
    execute format('alter table public.%I enable row level security', t);
    execute format('revoke all on public.%I from anon', t);
    execute format('grant select, insert, update, delete on public.%I to authenticated', t);
    execute format(
      'create policy %I on public.%I for all to authenticated
         using (user_id = (select auth.uid()))
         with check (user_id = (select auth.uid()))',
      t || '_dono', t);
  end loop;
end $$;
