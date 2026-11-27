// Os testes de contrato contra o Supabase compartilham uma única conta de teste, então não podem
// rodar em paralelo (um limparia os dados do outro). Com SQLite em memória a diferença é de segundos.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
