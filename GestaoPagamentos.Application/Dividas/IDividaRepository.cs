using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Domain.Dividas;

namespace GestaoPagamentos.Application.Dividas;

public interface IDividaRepository : IRepository<Divida, Guid>
{
    Task<Divida?> ObterPorParcelaAsync(Guid parcelaId, CancellationToken ct = default);
    Task<Divida?> ObterPorPagamentoAsync(Guid pagamentoId, CancellationToken ct = default);
    Task<IReadOnlyList<Divida>> ListarAsync(StatusDivida? status, Guid? devedorId, CancellationToken ct = default);
    Task<IReadOnlyList<Divida>> ListarAtivasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Divida>> ListarPorParcelasAsync(IEnumerable<Guid> parcelaIds, CancellationToken ct = default);
    Task<bool> ExisteAtivaDoDevedorAsync(Guid devedorId, CancellationToken ct = default);
}
