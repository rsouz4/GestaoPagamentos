using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Devedores;

namespace GestaoPagamentos.Application.Dividas;

public sealed class ObterDividaUseCase(IDividaRepository dividas, IDevedorRepository devedores, IClock clock)
{
    public async Task<Result<DividaDetalheDto>> ExecuteAsync(Guid dividaId, CancellationToken ct = default)
    {
        var divida = await dividas.ObterPorIdAsync(dividaId, ct);
        if (divida is null) return Result.Falha<DividaDetalheDto>("Dívida não encontrada.");

        var devedor = await devedores.ObterPorIdAsync(divida.DevedorId, ct);
        if (devedor is null) return Result.Falha<DividaDetalheDto>("Devedor da dívida não encontrado.");

        return Result.Ok(DividaMapper.ParaDetalhe(divida, devedor, clock.Hoje));
    }
}
