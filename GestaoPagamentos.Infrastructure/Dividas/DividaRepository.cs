using Microsoft.EntityFrameworkCore;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Infrastructure.Persistence;

namespace GestaoPagamentos.Infrastructure.Dividas;

public sealed class DividaRepository(AppDbContext db) : RepositoryBase<Divida>(db), IDividaRepository
{
    protected override IQueryable<Divida> Query =>
        Db.Dividas.Include(d => d.Parcelas).ThenInclude(p => p.Pagamentos).AsSplitQuery();

    public Task<Divida?> ObterPorParcelaAsync(Guid parcelaId, CancellationToken ct = default) =>
        Query.FirstOrDefaultAsync(d => d.Parcelas.Any(p => p.Id == parcelaId), ct);

    public Task<Divida?> ObterPorPagamentoAsync(Guid pagamentoId, CancellationToken ct = default) =>
        Query.FirstOrDefaultAsync(d => d.Parcelas.Any(p => p.Pagamentos.Any(x => x.Id == pagamentoId)), ct);

    public async Task<IReadOnlyList<Divida>> ListarAsync(StatusDivida? status, Guid? devedorId, CancellationToken ct = default)
    {
        var query = Query;
        if (status.HasValue) query = query.Where(d => d.Status == status.Value);
        if (devedorId.HasValue) query = query.Where(d => d.DevedorId == devedorId.Value);
        return await query.OrderByDescending(d => d.DataPrimeiroVencimento).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Divida>> ListarAtivasAsync(CancellationToken ct = default) =>
        await Query.Where(d => d.Status == StatusDivida.Ativa).ToListAsync(ct);

    public async Task<IReadOnlyList<Divida>> ListarPorParcelasAsync(IEnumerable<Guid> parcelaIds, CancellationToken ct = default)
    {
        var ids = parcelaIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await Query.Where(d => d.Parcelas.Any(p => ids.Contains(p.Id))).ToListAsync(ct);
    }

    public Task<bool> ExisteAtivaDoDevedorAsync(Guid devedorId, CancellationToken ct = default) =>
        Db.Dividas.AnyAsync(d => d.DevedorId == devedorId && d.Status == StatusDivida.Ativa, ct);
}
