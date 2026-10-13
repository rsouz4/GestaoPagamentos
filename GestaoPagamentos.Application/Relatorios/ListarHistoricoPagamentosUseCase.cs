using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Application.Dividas;

namespace GestaoPagamentos.Application.Relatorios;

public sealed class ListarHistoricoPagamentosUseCase(IDividaRepository dividas, IDevedorRepository devedores)
{
    public async Task<IReadOnlyList<HistoricoPagamentoDto>> ExecuteAsync(
        Guid? dividaId = null,
        Guid? devedorId = null,
        CancellationToken ct = default)
    {
        var lista = await dividas.ListarAsync(null, devedorId, ct);
        if (dividaId.HasValue) lista = lista.Where(d => d.Id == dividaId.Value).ToList();

        var nomes = (await devedores.ObterPorIdsAsync(lista.Select(d => d.DevedorId).Distinct(), ct))
            .ToDictionary(d => d.Id, d => d.Nome);

        return lista
            .SelectMany(d => d.Parcelas.SelectMany(p => p.Pagamentos.Select(pg => new HistoricoPagamentoDto(
                pg.Id,
                d.Id,
                nomes.GetValueOrDefault(d.DevedorId, "-"),
                d.Descricao,
                p.Numero,
                pg.Valor,
                pg.DataPagamento,
                pg.Observacao))))
            .OrderByDescending(h => h.DataPagamento)
            .ToList();
    }
}
