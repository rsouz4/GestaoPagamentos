using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Domain.Devedores;

namespace GestaoPagamentos.Application.Devedores;

public interface IDevedorRepository : IRepository<Devedor, Guid>
{
    Task<IReadOnlyList<Devedor>> ListarAsync(string? filtroNome, CancellationToken ct = default);
    Task<IReadOnlyList<Devedor>> ObterPorIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
