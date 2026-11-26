using GestaoPagamentos.Application.Common;

namespace GestaoPagamentos.Application.Autenticacao;

/// <summary>
/// Entrada do usuário no app. Com o armazenamento na nuvem o login é obrigatório (é ele que
/// libera as linhas do usuário pelo RLS); com o SQLite local não existe login.
/// </summary>
public interface IAuthService
{
    /// <summary>Se o app precisa de login antes de abrir.</summary>
    bool ExigeLogin { get; }

    /// <summary>E-mail da conta logada, ou <c>null</c> sem login.</summary>
    string? EmailAtual { get; }

    /// <summary>Tenta reaproveitar a sessão guardada na última vez em que o usuário pediu para continuar conectado.</summary>
    Task<bool> RestaurarSessaoAsync(CancellationToken ct = default);

    /// <param name="manterConectado">Se verdadeiro, a sessão fica guardada (protegida) neste computador.</param>
    Task<Result> EntrarAsync(string email, string senha, bool manterConectado, CancellationToken ct = default);

    /// <summary>Encerra a sessão e apaga a que estiver guardada neste computador.</summary>
    Task SairAsync(CancellationToken ct = default);
}

/// <summary>Modo sem login (SQLite local): o app abre direto.</summary>
public sealed class SemAutenticacao : IAuthService
{
    public bool ExigeLogin => false;
    public string? EmailAtual => null;
    public Task<bool> RestaurarSessaoAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<Result> EntrarAsync(string email, string senha, bool manterConectado, CancellationToken ct = default) =>
        Task.FromResult(Result.Ok());

    public Task SairAsync(CancellationToken ct = default) => Task.CompletedTask;
}
