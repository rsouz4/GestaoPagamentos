using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Application.Notificacoes;

public interface INotificacaoRepository : IRepository<Notificacao, Guid>
{
    Task<IReadOnlyList<Notificacao>> ListarAsync(StatusNotificacao? status, TipoNotificacao? tipo, CancellationToken ct = default);
    Task<IReadOnlyList<Notificacao>> ListarPendentesPorParcelasAsync(IEnumerable<Guid> parcelaIds, CancellationToken ct = default);
    Task<bool> ExisteAsync(Guid parcelaId, TipoNotificacao tipo, CancellationToken ct = default);
    Task<int> ContarPendentesAsync(CancellationToken ct = default);
}
