using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Persistence;
using GestaoPagamentos.Domain.Dividas;

namespace GestaoPagamentos.Application.Relatorios;

public sealed class RelatorioService(IUnitOfWorkFactory uowFactory, IClock clock)
{
    public async Task<ResumoDto> ObterResumoAsync(CancellationToken ct = default)
    {
        await using var uow = await uowFactory.CriarAsync(ct);

        var hoje = clock.Hoje;
        var validas = (await uow.Dividas.ListarAsync(null, null, ct))
            .Where(d => d.Status != StatusDivida.Cancelada)
            .ToList();
        var ativas = validas.Where(d => d.Status == StatusDivida.Ativa).ToList();

        var abertas = ativas.SelectMany(d => d.Parcelas).Where(p => !p.EstaQuitada).ToList();
        var atrasadas = abertas.Where(p => p.StatusEm(hoje) == StatusParcela.Atrasada).ToList();

        return new ResumoDto(
            TotalARreceber: abertas.Sum(p => p.ValorRestanteEm(hoje)),
            TotalRecebido: validas.Sum(d => d.ValorPago),
            TotalEmAtraso: atrasadas.Sum(p => p.ValorRestanteEm(hoje)),
            DividasAtivas: ativas.Count,
            ParcelasPendentes: abertas.Count(p => p.StatusEm(hoje) == StatusParcela.Pendente),
            ParcelasPagasParcialmente: abertas.Count(p => p.StatusEm(hoje) == StatusParcela.PagaParcial),
            ParcelasAtrasadas: atrasadas.Count);
    }

    public async Task<IReadOnlyList<ParcelaAtrasadaDto>> ListarAtrasadosAsync(CancellationToken ct = default)
    {
        await using var uow = await uowFactory.CriarAsync(ct);

        var hoje = clock.Hoje;
        var ativas = await uow.Dividas.ListarAsync(StatusDivida.Ativa, null, ct);
        var devedorIds = ativas.Select(d => d.DevedorId).Distinct().ToList();
        var mapa = await uow.Devedores.ObterPorIdsAsync(devedorIds, ct);

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

    public async Task<IReadOnlyList<HistoricoPagamentoDto>> ListarHistoricoPagamentosAsync(
        Guid? dividaId = null,
        Guid? devedorId = null,
        CancellationToken ct = default)
    {
        await using var uow = await uowFactory.CriarAsync(ct);

        IReadOnlyList<Divida> lista;
        if (dividaId.HasValue)
        {
            var divida = await uow.Dividas.ObterAsync(dividaId.Value, ct);
            lista = divida is not null && (!devedorId.HasValue || divida.DevedorId == devedorId.Value)
                ? [divida]
                : [];
        }
        else
        {
            lista = await uow.Dividas.ListarAsync(null, devedorId, ct);
        }

        var ids = lista.Select(d => d.DevedorId).Distinct().ToList();
        var devedores = await uow.Devedores.ObterPorIdsAsync(ids, ct);

        return lista
            .SelectMany(d => d.Parcelas.SelectMany(p => p.Pagamentos.Select(pg => new HistoricoPagamentoDto(
                pg.Id,
                d.Id,
                devedores.TryGetValue(d.DevedorId, out var devedor) ? devedor.Nome : "-",
                d.Descricao,
                p.Numero,
                pg.Valor,
                pg.DataPagamento,
                pg.Observacao))))
            .OrderByDescending(h => h.DataPagamento)
            .ToList();
    }
}
