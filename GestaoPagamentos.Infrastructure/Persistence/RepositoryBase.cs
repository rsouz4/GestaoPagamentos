using Microsoft.EntityFrameworkCore;
using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Infrastructure.Persistence;

public abstract class RepositoryBase<T>(AppDbContext db) : IRepository<T, Guid>
    where T : class, IEntity<Guid>
{
    protected AppDbContext Db { get; } = db;

    protected virtual IQueryable<T> Query => Db.Set<T>();

    public virtual Task<T?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Query.FirstOrDefaultAsync(e => e.Id == id, ct);

    public virtual async Task AdicionarAsync(T entidade, CancellationToken ct = default) =>
        await Db.Set<T>().AddAsync(entidade, ct);

    public virtual void Remover(T entidade) => Db.Set<T>().Remove(entidade);
}
