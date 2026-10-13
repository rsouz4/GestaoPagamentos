using Microsoft.EntityFrameworkCore;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Infrastructure.Persistence;

namespace GestaoPagamentos.Infrastructure.Devedores;

public sealed class DevedorRepository(AppDbContext db) : RepositoryBase<Devedor>(db), IDevedorRepository
{
    public async Task<IReadOnlyList<Devedor>> ListarAsync(string? filtroNome, CancellationToken ct = default)
    {
        var query = Db.Devedores.AsQueryable();
        if (!string.IsNullOrWhiteSpace(filtroNome))
        {
            var padrao = $"%{filtroNome.Trim()}%";
            query = query.Where(d => EF.Functions.ILike(d.Nome, padrao));
        }

        return await query.OrderBy(d => d.Nome).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Devedor>> ObterPorIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return [];
        return await Db.Devedores.Where(d => lista.Contains(d.Id)).ToListAsync(ct);
    }
}
