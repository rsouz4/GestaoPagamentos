using Microsoft.EntityFrameworkCore;
using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Domain.Notificacoes;
using GestaoPagamentos.Infrastructure.Persistence;

namespace GestaoPagamentos.Infrastructure.Notificacoes;

public sealed class NotificacaoRepository(AppDbContext db) : RepositoryBase<Notificacao>(db), INotificacaoRepository
{
    public async Task<IReadOnlyList<Notificacao>> ListarAsync(
        StatusNotificacao? status,
        TipoNotificacao? tipo,
        CancellationToken ct = default)
    {
        var query = Db.Notificacoes.AsQueryable();
        if (status.HasValue) query = query.Where(n => n.Status == status.Value);
        if (tipo.HasValue) query = query.Where(n => n.Tipo == tipo.Value);
        return await query.OrderByDescending(n => n.DataGeracao).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Notificacao>> ListarPendentesPorParcelasAsync(
        IEnumerable<Guid> parcelaIds,
        CancellationToken ct = default)
    {
        var ids = parcelaIds.Distinct().Cast<Guid?>().ToList();
        if (ids.Count == 0) return [];
        return await Db.Notificacoes
            .Where(n => n.Status == StatusNotificacao.Pendente && ids.Contains(n.ParcelaId))
            .ToListAsync(ct);
    }

    public Task<bool> ExisteAsync(Guid parcelaId, TipoNotificacao tipo, CancellationToken ct = default) =>
        Db.Notificacoes.AnyAsync(n => n.ParcelaId == parcelaId && n.Tipo == tipo, ct);

    public Task<int> ContarPendentesAsync(CancellationToken ct = default) =>
        Db.Notificacoes.CountAsync(n => n.Status == StatusNotificacao.Pendente, ct);
}
