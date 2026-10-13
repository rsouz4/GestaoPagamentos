using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Dividas;

namespace GestaoPagamentos.Application.Devedores;

public sealed class ExcluirDevedorUseCase(IDevedorRepository devedores, IDividaRepository dividas, IUnitOfWork uow)
{
    public async Task<Result> ExecuteAsync(Guid devedorId, CancellationToken ct = default)
    {
        var devedor = await devedores.ObterPorIdAsync(devedorId, ct);
        if (devedor is null) return Result.Falha("Devedor não encontrado.");

        if (await dividas.ExisteAtivaDoDevedorAsync(devedorId, ct))
            return Result.Falha("Não é possível excluir um devedor com dívidas ativas.");

        devedores.Remover(devedor);
        await uow.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
