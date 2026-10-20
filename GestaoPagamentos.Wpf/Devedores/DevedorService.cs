using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestaoPagamentos.Wpf.Devedores;

public sealed class DevedorService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<IReadOnlyList<DevedorDto>> ListarAsync(string? filtroNome = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = db.Devedores.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filtroNome))
        {
            var padrao = $"%{filtroNome.Trim()}%";
            query = query.Where(d => EF.Functions.Like(d.Nome, padrao));
        }

        var lista = await query.OrderBy(d => d.Nome).ToListAsync(ct);
        return lista.Select(DevedorDto.De).ToList();
    }

    public async Task<Result<DevedorDto>> SalvarAsync(SalvarDevedorCommand cmd, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            Devedor devedor;
            if (cmd.Id is null)
            {
                devedor = Devedor.Criar(cmd.Nome, cmd.Telefone, cmd.Email, cmd.Observacao);
                await db.Devedores.AddAsync(devedor, ct);
            }
            else
            {
                var existente = await db.Devedores.FirstOrDefaultAsync(d => d.Id == cmd.Id.Value, ct);
                if (existente is null) return Result.Falha<DevedorDto>("Devedor não encontrado.");
                existente.Atualizar(cmd.Nome, cmd.Telefone, cmd.Email, cmd.Observacao);
                devedor = existente;
            }

            await db.SaveChangesAsync(ct);
            return Result.Ok(DevedorDto.De(devedor));
        }
        catch (DomainException ex)
        {
            return Result.Falha<DevedorDto>(ex.Message);
        }
    }

    public async Task<Result> ExcluirAsync(Guid devedorId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var devedor = await db.Devedores.FirstOrDefaultAsync(d => d.Id == devedorId, ct);
        if (devedor is null) return Result.Falha("Devedor não encontrado.");

        if (await db.Dividas.AnyAsync(d => d.DevedorId == devedorId && d.Status == StatusDivida.Ativa, ct))
            return Result.Falha("Não é possível excluir um devedor com dívidas ativas.");

        db.Devedores.Remove(devedor);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }
}
