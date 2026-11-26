using GestaoPagamentos.Application.Persistence;
using GestaoPagamentos.Domain.Configuracoes;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;
using Microsoft.EntityFrameworkCore;

namespace GestaoPagamentos.Infrastructure.Persistence;

public sealed class EfUnitOfWorkFactory(IDbContextFactory<AppDbContext> dbFactory) : IUnitOfWorkFactory
{
    public async Task<IUnitOfWork> CriarAsync(CancellationToken ct = default) =>
        new EfUnitOfWork(await dbFactory.CreateDbContextAsync(ct));
}

internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    public EfUnitOfWork(AppDbContext db)
    {
        _db = db;
        Devedores = new EfDevedorRepository(db);
        Dividas = new EfDividaRepository(db);
        Notificacoes = new EfNotificacaoRepository(db);
        Configuracoes = new EfConfiguracaoRepository(db);
    }

    public IDevedorRepository Devedores { get; }
    public IDividaRepository Dividas { get; }
    public INotificacaoRepository Notificacoes { get; }
    public IConfiguracaoRepository Configuracoes { get; }

    public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}

internal sealed class EfDevedorRepository(AppDbContext db) : IDevedorRepository
{
    public async Task<IReadOnlyList<Devedor>> ListarAsync(string? filtroNome, CancellationToken ct = default)
    {
        var query = db.Devedores.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtroNome))
        {
            var padrao = $"%{filtroNome.Trim()}%";
            query = query.Where(d => EF.Functions.Like(d.Nome, padrao));
        }

        return await query.OrderBy(d => d.Nome).ToListAsync(ct);
    }

    public Task<Devedor?> ObterAsync(Guid id, CancellationToken ct = default) =>
        db.Devedores.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyDictionary<Guid, Devedor>> ObterPorIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return new Dictionary<Guid, Devedor>();

        var lista = ids.ToList();
        return await db.Devedores.AsNoTracking()
            .Where(d => lista.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);
    }

    public Task<bool> ExisteAsync(Guid id, CancellationToken ct = default) =>
        db.Devedores.AnyAsync(d => d.Id == id, ct);

    public async Task AdicionarAsync(Devedor devedor, CancellationToken ct = default) =>
        await db.Devedores.AddAsync(devedor, ct);

    public void Remover(Devedor devedor) => db.Devedores.Remove(devedor);
}

internal sealed class EfDividaRepository(AppDbContext db) : IDividaRepository
{
    private IQueryable<Divida> ComParcelas() =>
        db.Dividas
            .Include(d => d.Parcelas)
            .ThenInclude(p => p.Pagamentos)
            .AsSplitQuery();

    public async Task<IReadOnlyList<Divida>> ListarAsync(
        StatusDivida? status, Guid? devedorId, CancellationToken ct = default)
    {
        var query = ComParcelas().AsNoTracking();
        if (status.HasValue) query = query.Where(d => d.Status == status.Value);
        if (devedorId.HasValue) query = query.Where(d => d.DevedorId == devedorId.Value);
        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Divida>> ListarPorParcelasAsync(
        IReadOnlyCollection<Guid> parcelaIds, CancellationToken ct = default)
    {
        if (parcelaIds.Count == 0) return [];

        var ids = parcelaIds.ToList();
        return await ComParcelas()
            .AsNoTracking()
            .Where(d => d.Parcelas.Any(p => ids.Contains(p.Id)))
            .ToListAsync(ct);
    }

    public Task<Divida?> ObterAsync(Guid dividaId, CancellationToken ct = default) =>
        ComParcelas().FirstOrDefaultAsync(d => d.Id == dividaId, ct);

    public Task<Divida?> ObterPorParcelaAsync(Guid parcelaId, CancellationToken ct = default) =>
        ComParcelas().FirstOrDefaultAsync(d => d.Parcelas.Any(p => p.Id == parcelaId), ct);

    public Task<Divida?> ObterPorPagamentoAsync(Guid pagamentoId, CancellationToken ct = default) =>
        ComParcelas().FirstOrDefaultAsync(
            d => d.Parcelas.Any(p => p.Pagamentos.Any(x => x.Id == pagamentoId)), ct);

    public Task<bool> ExisteAtivaDoDevedorAsync(Guid devedorId, CancellationToken ct = default) =>
        db.Dividas.AnyAsync(d => d.DevedorId == devedorId && d.Status == StatusDivida.Ativa, ct);

    public async Task AdicionarAsync(Divida divida, CancellationToken ct = default) =>
        await db.Dividas.AddAsync(divida, ct);
}

internal sealed class EfNotificacaoRepository(AppDbContext db) : INotificacaoRepository
{
    public async Task<IReadOnlyList<Notificacao>> ListarAsync(
        StatusNotificacao? status, TipoNotificacao? tipo, CancellationToken ct = default)
    {
        var query = db.Notificacoes.AsNoTracking();
        if (status.HasValue) query = query.Where(n => n.Status == status.Value);
        if (tipo.HasValue) query = query.Where(n => n.Tipo == tipo.Value);
        return await query.ToListAsync(ct);
    }

    public Task<int> ContarPendentesAsync(CancellationToken ct = default) =>
        db.Notificacoes.CountAsync(n => n.Status == StatusNotificacao.Pendente, ct);

    public Task<Notificacao?> ObterAsync(Guid id, CancellationToken ct = default) =>
        db.Notificacoes.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<Notificacao>> ListarPendentesPorParcelasAsync(
        IReadOnlyCollection<Guid> parcelaIds, TipoNotificacao? tipo, CancellationToken ct = default)
    {
        if (parcelaIds.Count == 0) return [];

        var ids = parcelaIds.Select(id => (Guid?)id).ToList();
        var query = db.Notificacoes.Where(n => n.Status == StatusNotificacao.Pendente && ids.Contains(n.ParcelaId));
        if (tipo.HasValue) query = query.Where(n => n.Tipo == tipo.Value);
        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<(Guid ParcelaId, TipoNotificacao Tipo)>> ListarChavesExistentesAsync(
        CancellationToken ct = default)
    {
        var chaves = await db.Notificacoes
            .Where(n => n.ParcelaId != null)
            .Select(n => new { n.ParcelaId, n.Tipo })
            .ToListAsync(ct);

        return chaves.Select(x => (x.ParcelaId!.Value, x.Tipo)).ToList();
    }

    public async Task AdicionarAsync(Notificacao notificacao, CancellationToken ct = default) =>
        await db.Notificacoes.AddAsync(notificacao, ct);
}

internal sealed class EfConfiguracaoRepository(AppDbContext db) : IConfiguracaoRepository
{
    public Task<Configuracao?> ObterAsync(CancellationToken ct = default) =>
        db.Configuracoes.FirstOrDefaultAsync(ct);

    public async Task AdicionarAsync(Configuracao configuracao, CancellationToken ct = default) =>
        await db.Configuracoes.AddAsync(configuracao, ct);
}
