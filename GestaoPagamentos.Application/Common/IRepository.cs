using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Application.Common;

public interface IRepository<T, in TId>
    where T : class, IEntity<TId>
    where TId : notnull
{
    Task<T?> ObterPorIdAsync(TId id, CancellationToken ct = default);
    Task AdicionarAsync(T entidade, CancellationToken ct = default);
    void Remover(T entidade);
}
