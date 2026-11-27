-- =====================================================================
-- Gestão de Pagamentos — Passo 3: função de gravação atômica
-- Rodar no SQL Editor do Supabase depois de 01_schema.sql.
--
-- O app não grava tabela por tabela: ele monta tudo o que mudou numa operação
-- (por exemplo, um pagamento + o aviso + o descarte de notificações) e chama
-- esta função uma vez. Ela grava tudo na mesma transação: ou vai tudo, ou nada.
--
-- Formato de p (cada tabela é opcional):
--   { "devedores": { "upsert": [ {linha}, ... ], "delete": [ "id", ... ] },
--     "dividas": {...}, "parcelas": {...}, "pagamentos": {...},
--     "notificacoes": {...}, "configuracoes": {...} }
--
-- security invoker: roda com o usuário logado, então o RLS continua valendo.
-- user_id vem sempre de auth.uid(); o que vier no JSON é ignorado.
-- =====================================================================

create or replace function public.aplicar_mudancas(p jsonb)
returns void
language plpgsql
security invoker
set search_path = public
as $$
declare
  v_user uuid := auth.uid();
begin
  if v_user is null then
    raise exception 'Usuário não autenticado' using errcode = '28000';
  end if;

  -- Gravações, na ordem das chaves estrangeiras ------------------------

  insert into devedores (id, user_id, nome, telefone, email, observacao)
  select r.id, v_user, r.nome, r.telefone, r.email, r.observacao
    from jsonb_to_recordset(coalesce(p #> '{devedores,upsert}', '[]'::jsonb))
         as r(id uuid, nome text, telefone text, email text, observacao text)
  on conflict (id) do update set
    nome = excluded.nome,
    telefone = excluded.telefone,
    email = excluded.email,
    observacao = excluded.observacao;

  insert into dividas (id, user_id, devedor_id, descricao, valor_emprestado, taxa_juros, valor_total,
                       data_primeiro_vencimento, data_emprestimo, tipo, quantidade_parcelas, status, observacao)
  select r.id, v_user, r.devedor_id, r.descricao, r.valor_emprestado, r.taxa_juros, r.valor_total,
         r.data_primeiro_vencimento, r.data_emprestimo, r.tipo, r.quantidade_parcelas, r.status, r.observacao
    from jsonb_to_recordset(coalesce(p #> '{dividas,upsert}', '[]'::jsonb))
         as r(id uuid, devedor_id uuid, descricao text, valor_emprestado numeric, taxa_juros numeric,
              valor_total numeric, data_primeiro_vencimento date, data_emprestimo date, tipo smallint,
              quantidade_parcelas integer, status smallint, observacao text)
  on conflict (id) do update set
    descricao = excluded.descricao,
    valor_emprestado = excluded.valor_emprestado,
    taxa_juros = excluded.taxa_juros,
    valor_total = excluded.valor_total,
    data_primeiro_vencimento = excluded.data_primeiro_vencimento,
    data_emprestimo = excluded.data_emprestimo,
    tipo = excluded.tipo,
    quantidade_parcelas = excluded.quantidade_parcelas,
    status = excluded.status,
    observacao = excluded.observacao;

  insert into parcelas (id, user_id, divida_id, numero, valor_previsto, data_vencimento)
  select r.id, v_user, r.divida_id, r.numero, r.valor_previsto, r.data_vencimento
    from jsonb_to_recordset(coalesce(p #> '{parcelas,upsert}', '[]'::jsonb))
         as r(id uuid, divida_id uuid, numero integer, valor_previsto numeric, data_vencimento date)
  on conflict (id) do update set
    numero = excluded.numero,
    valor_previsto = excluded.valor_previsto,
    data_vencimento = excluded.data_vencimento;

  insert into pagamentos (id, user_id, parcela_id, valor, data_pagamento, observacao)
  select r.id, v_user, r.parcela_id, r.valor, r.data_pagamento, r.observacao
    from jsonb_to_recordset(coalesce(p #> '{pagamentos,upsert}', '[]'::jsonb))
         as r(id uuid, parcela_id uuid, valor numeric, data_pagamento date, observacao text)
  on conflict (id) do update set
    valor = excluded.valor,
    data_pagamento = excluded.data_pagamento,
    observacao = excluded.observacao;

  insert into notificacoes (id, user_id, devedor_id, parcela_id, tipo, mensagem, data_geracao, data_envio, status)
  select r.id, v_user, r.devedor_id, r.parcela_id, r.tipo, r.mensagem, r.data_geracao, r.data_envio, r.status
    from jsonb_to_recordset(coalesce(p #> '{notificacoes,upsert}', '[]'::jsonb))
         as r(id uuid, devedor_id uuid, parcela_id uuid, tipo smallint, mensagem text,
              data_geracao timestamp, data_envio timestamp, status smallint)
  on conflict (id) do update set
    tipo = excluded.tipo,
    mensagem = excluded.mensagem,
    data_geracao = excluded.data_geracao,
    data_envio = excluded.data_envio,
    status = excluded.status;

  insert into configuracoes (id, user_id, dias_antecedencia_lembrete, intervalo_envio_lote_ms,
                             template_lembrete, template_atraso)
  select r.id, v_user, r.dias_antecedencia_lembrete, r.intervalo_envio_lote_ms,
         r.template_lembrete, r.template_atraso
    from jsonb_to_recordset(coalesce(p #> '{configuracoes,upsert}', '[]'::jsonb))
         as r(id uuid, dias_antecedencia_lembrete integer, intervalo_envio_lote_ms integer,
              template_lembrete text, template_atraso text)
  on conflict (id) do update set
    dias_antecedencia_lembrete = excluded.dias_antecedencia_lembrete,
    intervalo_envio_lote_ms = excluded.intervalo_envio_lote_ms,
    template_lembrete = excluded.template_lembrete,
    template_atraso = excluded.template_atraso;

  -- Exclusões, na ordem inversa -----------------------------------------

  delete from notificacoes where id in (
    select x::uuid from jsonb_array_elements_text(coalesce(p #> '{notificacoes,delete}', '[]'::jsonb)) as x);
  delete from pagamentos where id in (
    select x::uuid from jsonb_array_elements_text(coalesce(p #> '{pagamentos,delete}', '[]'::jsonb)) as x);
  delete from parcelas where id in (
    select x::uuid from jsonb_array_elements_text(coalesce(p #> '{parcelas,delete}', '[]'::jsonb)) as x);
  delete from dividas where id in (
    select x::uuid from jsonb_array_elements_text(coalesce(p #> '{dividas,delete}', '[]'::jsonb)) as x);
  delete from devedores where id in (
    select x::uuid from jsonb_array_elements_text(coalesce(p #> '{devedores,delete}', '[]'::jsonb)) as x);
end
$$;

-- Só usuários logados executam; a chave pública sem login não.
revoke all on function public.aplicar_mudancas(jsonb) from public, anon;
grant execute on function public.aplicar_mudancas(jsonb) to authenticated;
