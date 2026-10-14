using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Domain.Dividas;

namespace GestaoPagamentos.Application.Relatorios;

public sealed class ListarAtrasadosUseCase(IDividaRepository dividas, IDevedorRepository devedores, IClock clock)
{
    public async Task<IReadOnlyList<ParcelaAtrasadaDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var hoje = clock.Hoje;
        var ativas = await dividas.ListarAtivasAsync(ct);
        var mapa = (await devedores.ObterPorIdsAsync(ativas.Select(d => d.DevedorId).Distinct(), ct))
            .ToDictionary(d => d.Id);

        return ativas
            .SelectMany(d => d.Parcelas.Select(p => (Divida: d, Parcela: p)))
            .Where(x => x.Parcela.StatusEm(hoje) == StatusParcela.Atrasada)
            .Select(x =>
            {
                mapa.TryGetValue(x.Divida.DevedorId, out var devedor);
                return new ParcelaAtrasadaDto(
                    x.Divida.Id,
                    x.Parcela.Id,
                    x.Divida.DevedorId,
                    devedor?.Nome ?? "-",
                    devedor?.Telefone,
                    x.Divida.Descricao,
                    x.Parcela.Numero,
                    x.Parcela.ValorRestanteEm(hoje),
                    x.Parcela.DataVencimento,
                    hoje.DayNumber - x.Parcela.DataVencimento.DayNumber);
            })
            .OrderByDescending(a => a.DiasAtraso)
            .ToList();
    }
}
