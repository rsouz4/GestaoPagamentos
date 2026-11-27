using GestaoPagamentos.Tests.Autenticacao;
using System.Text.Json;
using GestaoPagamentos.Application.Persistence;
using GestaoPagamentos.Infrastructure.Nuvem;
using Microsoft.Extensions.Logging.Abstractions;

namespace GestaoPagamentos.Tests.Servicos;

/// <summary>
/// Roda os mesmos testes dos serviços contra o Supabase de verdade, em vez do SQLite em memória, para
/// provar que as duas camadas de dados se comportam igual. Fica desligado, a menos que se peça.
///
/// ATENÇÃO: cada teste APAGA todos os dados da conta de teste antes de rodar. Por isso só aceita
/// e-mails que começam com "teste@". Nunca aponte para a sua conta de uso real.
///
/// Como ligar (PowerShell, na raiz do repositório):
///   $env:GESTAO_TESTES_ARMAZENAMENTO = "Supabase"
///   $env:SUPABASE_TESTE_EMAIL = "teste@teste.com.br"
///   $env:SUPABASE_TESTE_SENHA = Read-Host "Senha da conta de teste"
///   dotnet test GestaoPagamentos.Tests
///
/// A URL e a chave pública vêm de %LOCALAPPDATA%\GestaoPagamentos\appsettings.Local.json
/// (ou de SUPABASE_URL e SUPABASE_CHAVE). A senha nunca é gravada em arquivo.
/// </summary>
internal static class ContratoSupabase
{
    public static bool Ativo =>
        string.Equals(Environment.GetEnvironmentVariable("GESTAO_TESTES_ARMAZENAMENTO"), "Supabase", StringComparison.OrdinalIgnoreCase);

    private static readonly object Trava = new();
    private static Task<(IApiSupabase Api, IUnitOfWorkFactory Fabrica)>? _iniciado;

    public static IUnitOfWorkFactory ObterFabricaELimpar()
    {
        Task<(IApiSupabase Api, IUnitOfWorkFactory Fabrica)> iniciado;
        lock (Trava) iniciado = _iniciado ??= EntrarAsync();

        var (api, fabrica) = iniciado.GetAwaiter().GetResult();
        LimparAsync(api).GetAwaiter().GetResult();
        return fabrica;
    }

    private static async Task<(IApiSupabase, IUnitOfWorkFactory)> EntrarAsync()
    {
        var email = Environment.GetEnvironmentVariable("SUPABASE_TESTE_EMAIL");
        var senha = Environment.GetEnvironmentVariable("SUPABASE_TESTE_SENHA");
        var faltando = new List<string>();
        if (string.IsNullOrWhiteSpace(email)) faltando.Add("SUPABASE_TESTE_EMAIL");
        if (string.IsNullOrEmpty(senha)) faltando.Add("SUPABASE_TESTE_SENHA");
        if (faltando.Count > 0)
            throw new InvalidOperationException(
                $"Para rodar contra o Supabase, falta definir (ou está vazio): {string.Join(" e ", faltando)}. " +
                "Defina-as na mesma janela do terminal em que roda o dotnet test.");

        if (!email!.StartsWith("teste@", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Recusado: os testes apagam todos os dados da conta usada, e por segurança só aceitam e-mails que começam com 'teste@'.");

        var config = LerConfiguracao();
        var sessao = new SessaoPersistenteSupabase(new ArmazenamentoEmMemoria()) { Persistir = false };
        var cliente = new ClienteSupabase(config, sessao);
        var auth = new SupabaseAuthService(cliente, new ArmazenamentoEmMemoria(), NullLogger<SupabaseAuthService>.Instance);

        var entrada = await auth.EntrarAsync(email!, senha!, manterConectado: false);
        if (!entrada.Sucesso)
            throw new InvalidOperationException($"Não foi possível entrar com a conta de teste: {entrada.Erro}");

        var api = new ApiSupabase(cliente, config, new HttpClient());
        return (api, new SupabaseUnitOfWorkFactory(api));
    }

    private static async Task LimparAsync(IApiSupabase api)
    {
        // Só apaga linhas da conta logada (RLS). Apagar o devedor leva junto dívidas, parcelas e pagamentos.
        await api.ExcluirAsync("notificacoes?id=not.is.null");
        await api.ExcluirAsync("devedores?id=not.is.null");
        await api.ExcluirAsync("configuracoes?id=not.is.null");
    }

    private static SupabaseConfig LerConfiguracao()
    {
        var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
        var chave = Environment.GetEnvironmentVariable("SUPABASE_CHAVE");

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(chave))
        {
            var arquivo = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GestaoPagamentos", "appsettings.Local.json");
            if (File.Exists(arquivo))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(arquivo));
                if (doc.RootElement.TryGetProperty("Supabase", out var s))
                {
                    url = s.TryGetProperty("Url", out var u) ? u.GetString() : url;
                    chave = s.TryGetProperty("AnonKey", out var k) ? k.GetString() : chave;
                }
            }
        }

        return new SupabaseConfig { Url = url, AnonKey = chave };
    }
}
