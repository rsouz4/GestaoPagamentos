namespace GestaoPagamentos.Infrastructure.Nuvem;

/// <summary>
/// Cria uma única vez o cliente do Supabase e o reaproveita (um cliente novo a cada operação
/// abriria uma conexão nova toda vez). Compartilhado pelo login e, depois, pelo acesso a dados.
/// </summary>
public sealed class ClienteSupabase(SupabaseConfig config, SessaoPersistenteSupabase sessao)
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private Supabase.Client? _cliente;

    public SessaoPersistenteSupabase Sessao => sessao;

    public async Task<Supabase.Client> ObterAsync(CancellationToken ct = default)
    {
        if (_cliente is not null) return _cliente;

        await _trava.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cliente is not null) return _cliente;

            config.Validar();
            var opcoes = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false,
                SessionHandler = sessao
            };

            var cliente = new Supabase.Client(config.Url!, config.AnonKey!, opcoes);
            await cliente.InitializeAsync().ConfigureAwait(false);
            _cliente = cliente;
            return cliente;
        }
        finally
        {
            _trava.Release();
        }
    }
}
