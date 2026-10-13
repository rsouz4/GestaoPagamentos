using GestaoPagamentos.Application.Common;

namespace GestaoPagamentos.Application.Notificacoes;

public sealed class IgnorarNotificacaoUseCase(INotificacaoRepository notificacoes, IUnitOfWork uow)
{
    public async Task<Result> ExecuteAsync(Guid notificacaoId, CancellationToken ct = default)
    {
        var notificacao = await notificacoes.ObterPorIdAsync(notificacaoId, ct);
        if (notificacao is null) return Result.Falha("Notificação não encontrada.");

        notificacao.Ignorar();
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
