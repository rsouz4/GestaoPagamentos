using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;
using GestaoPagamentos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestaoPagamentos.Wpf.Dividas;

public sealed class DividaService(
    IDbContextFactory<AppDbContext> dbFactory,
    IClock clock)
{
    internal static IQueryable<Divida> ComParcelas(AppDbContext db) =>
        db.Dividas
            .Include(d => d.Parcelas)
            .ThenInclude(p => p.Pagamentos)
            .AsSplitQuery();

    public async Task<IReadOnlyList<DividaResumoDto>> ListarAsync(
        StatusDivida? status = null,
        Guid? devedorId = null,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = ComParcelas(db)
            .AsNoTracking();

        if (status.HasValue) query = query
                .Where(d => d.Status == status.Value);

        if (devedorId.HasValue) query = query
                .Where(d => d.DevedorId == devedorId.Value);

        var lista = await query
            .OrderByDescending(d => d.DataPrimeiroVencimento)
            .ToListAsync(ct);

        var ids = lista
            .Select(d => d.DevedorId)
            .Distinct()
            .ToList();

        var nomes = await db.Devedores.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Nome, ct);

        return lista
            .Select(d => DividaMapper.ParaResumo(d, nomes.GetValueOrDefault(d.DevedorId, "-"), clock.Hoje))
            .ToList();
    }

    public async Task<Result<DividaDetalheDto>> ObterAsync(Guid dividaId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var divida = await ComParcelas(db).AsNoTracking().FirstOrDefaultAsync(d => d.Id == dividaId, ct);
        if (divida is null) return Result.Falha<DividaDetalheDto>("Dívida não encontrada.");

        var devedor = await db.Devedores.AsNoTracking().FirstOrDefaultAsync(d => d.Id == divida.DevedorId, ct);
        if (devedor is null) return Result.Falha<DividaDetalheDto>("Devedor da dívida não encontrado.");

        return Result.Ok(DividaMapper.ParaDetalhe(divida, devedor, clock.Hoje));
    }
    public async Task<Result<Guid>> CriarAsync(CriarDividaCommand cmd, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            if (!await db.Devedores.AnyAsync(d => d.Id == cmd.DevedorId, ct))
                return Result.Falha<Guid>("Devedor não encontrado.");

            var divida = Divida.Criar(
                cmd.DevedorId,
                cmd.Descricao,
                cmd.ValorEmprestado,
                cmd.PrimeiroVencimento,
                cmd.Tipo,
                cmd.QuantidadeParcelas,
                cmd.Observacao);

            await db.Dividas
                .AddAsync(divida, ct);
            await db.SaveChangesAsync(ct);
            return Result.Ok(divida.Id);
        }
        catch (DomainException ex)
        {
            return Result.Falha<Guid>(ex.Message);
        }
    }
    public async Task<Result> AtualizarAsync(AtualizarDividaCommand cmd, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var divida = await ComParcelas(db).FirstOrDefaultAsync(d => d.Id == cmd.DividaId, ct);
            if (divida is null) return Result.Falha("Dívida não encontrada.");

            divida.AtualizarDados(cmd.Descricao, cmd.Observacao);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }
    public async Task<Result> AlterarTaxaJurosAsync(Guid dividaId, decimal taxaJuros, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var divida = await ComParcelas(db).FirstOrDefaultAsync(d => d.Id == dividaId, ct);
            if (divida is null) return Result.Falha("Dívida não encontrada.");

            divida.AlterarTaxaJuros(taxaJuros);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }

    public async Task<Result> CancelarAsync(Guid dividaId, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var divida = await ComParcelas(db).FirstOrDefaultAsync(d => d.Id == dividaId, ct);
            if (divida is null) return Result.Falha("Dívida não encontrada.");

            divida.Cancelar();

            var parcelaIds = divida.Parcelas.Select(p => (Guid?)p.Id).ToList();
            var pendentes = await db.Notificacoes
                .Where(n => n.Status == StatusNotificacao.Pendente && parcelaIds.Contains(n.ParcelaId))
                .ToListAsync(ct);
            foreach (var n in pendentes) n.Descartar();

            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }
}
