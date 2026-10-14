using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Notificacoes;

namespace GestaoPagamentos.Application.Dividas;

public sealed class CancelarDividaUseCase(
    IDividaRepository dividas,
    INotificacaoRepository notificacoes,
    IUnitOfWork uow)
{
    public async Task<Result> ExecuteAsync(Guid dividaId, CancellationToken ct = default)
    {
        var divida = await dividas.ObterPorIdAsync(dividaId, ct);
        if (divida is null)
            return Result.Falha("Dívida não encontrada.");

        divida.Cancelar();

        var pendentes = await notificacoes.ListarPendentesPorParcelasAsync(divida.Parcelas.Select(p => p.Id), ct);
        foreach (var n in pendentes) n.Descartar();

        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
