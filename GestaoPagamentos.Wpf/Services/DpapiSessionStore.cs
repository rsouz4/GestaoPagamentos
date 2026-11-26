using System.IO;
using System.Security.Cryptography;
using System.Text;
using GestaoPagamentos.Application.Autenticacao;

namespace GestaoPagamentos.Wpf.Services;

/// <summary>
/// Guarda a sessão em %LOCALAPPDATA%\GestaoPagamentos\sessao.bin, cifrada pela proteção de dados do
/// Windows (DPAPI) para o usuário atual: outro usuário do Windows, ou o arquivo copiado para outro
/// computador, não consegue ler. A leitura e a escrita são síncronas de propósito (arquivo pequeno),
/// para não travar a interface quando chamadas de dentro do cliente do Supabase.
/// </summary>
public sealed class DpapiSessionStore : ISessionStore
{
    private static readonly byte[] Entropia = Encoding.UTF8.GetBytes("GestaoPagamentos.Sessao.v1");

    private readonly string _caminho;

    public DpapiSessionStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GestaoPagamentos",
            "sessao.bin"))
    {
    }

    public DpapiSessionStore(string caminho) => _caminho = caminho;

    public Task<string?> LerAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_caminho)) return Task.FromResult<string?>(null);

        try
        {
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(_caminho), Entropia, DataProtectionScope.CurrentUser);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(bytes));
        }
        catch (CryptographicException)
        {
            // Arquivo de outro usuário, de outro computador ou corrompido: tratado como sem sessão.
            return Task.FromResult<string?>(null);
        }
    }

    public Task GravarAsync(string conteudo, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_caminho)!);
        var cifrado = ProtectedData.Protect(Encoding.UTF8.GetBytes(conteudo), Entropia, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_caminho, cifrado);
        return Task.CompletedTask;
    }

    public Task ApagarAsync(CancellationToken ct = default)
    {
        if (File.Exists(_caminho)) File.Delete(_caminho);
        return Task.CompletedTask;
    }
}
