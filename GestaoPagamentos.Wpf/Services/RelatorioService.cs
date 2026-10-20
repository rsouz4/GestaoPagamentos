using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Relatorios;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Infrastructure.Persistence;
using GestaoPagamentos.Wpf.Dividas;
using Microsoft.EntityFrameworkCore;

namespace GestaoPagamentos.Wpf.Services;

public sealed class RelatorioService(IDbContextFactory<AppDbContext> dbFactory, IClock clock)
{
    public async Task<ResumoDto> ObterResumoAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var hoje = clock.Hoje;
        var validas = await DividaService.ComParcelas(db).AsNoTracking()
            .Where(d => d.Status != StatusDivida.Cancelada)
            .ToListAsync(ct);
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
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var hoje = clock.Hoje;
        var ativas = await DividaService.ComParcelas(db).AsNoTracking()
            .Where(d => d.Status == StatusDivida.Ativa)
            .ToListAsync(ct);
        var devedorIds = ativas.Select(d => d.DevedorId).Distinct().ToList();
        var mapa = await db.Devedores.AsNoTracking()
            .Where(d => devedorIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);

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
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = DividaService.ComParcelas(db).AsNoTracking();
        if (dividaId.HasValue) query = query.Where(d => d.Id == dividaId.Value);
        if (devedorId.HasValue) query = query.Where(d => d.DevedorId == devedorId.Value);
        var lista = await query.ToListAsync(ct);

        var ids = lista.Select(d => d.DevedorId).Distinct().ToList();
        var nomes = await db.Devedores.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Nome, ct);

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
