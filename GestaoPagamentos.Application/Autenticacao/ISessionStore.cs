namespace GestaoPagamentos.Application.Autenticacao;

/// <summary>
/// Onde fica guardada a sessão do usuário entre uma abertura e outra do app.
/// Quem implementa decide como proteger o conteúdo (no WPF, pela proteção de dados do Windows).
/// </summary>
public interface ISessionStore
{
    Task<string?> LerAsync(CancellationToken ct = default);
    Task GravarAsync(string conteudo, CancellationToken ct = default);
    Task ApagarAsync(CancellationToken ct = default);
}
