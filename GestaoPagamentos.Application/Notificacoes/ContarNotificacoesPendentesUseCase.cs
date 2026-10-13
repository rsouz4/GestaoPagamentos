namespace GestaoPagamentos.Application.Notificacoes;

public sealed class ContarNotificacoesPendentesUseCase(INotificacaoRepository notificacoes)
{
    public Task<int> ExecuteAsync(CancellationToken ct = default) => notificacoes.ContarPendentesAsync(ct);
}
