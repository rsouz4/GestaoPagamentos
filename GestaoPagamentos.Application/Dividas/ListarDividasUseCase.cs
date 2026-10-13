using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Domain.Dividas;

namespace GestaoPagamentos.Application.Dividas;

public sealed class ListarDividasUseCase(IDividaRepository dividas, IDevedorRepository devedores, IClock clock)
{
    public async Task<IReadOnlyList<DividaResumoDto>> ExecuteAsync(
        StatusDivida? status = null,
        Guid? devedorId = null,
        CancellationToken ct = default)
    {
        var lista = await dividas.ListarAsync(status, devedorId, ct);
        var nomes = (await devedores.ObterPorIdsAsync(lista.Select(d => d.DevedorId).Distinct(), ct))
            .ToDictionary(d => d.Id, d => d.Nome);

        return lista
            .Select(d => DividaMapper.ParaResumo(d, nomes.GetValueOrDefault(d.DevedorId, "-"), clock.Hoje))
            .ToList();
    }
}
