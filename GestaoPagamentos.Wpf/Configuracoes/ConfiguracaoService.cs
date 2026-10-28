using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Configuracoes;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Configuracoes;
using GestaoPagamentos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestaoPagamentos.Wpf.Configuracoes;

public sealed class ConfiguracaoService(
    IDbContextFactory<AppDbContext> dbFactory
    )
{
    internal static async Task<Configuracao> ObterEntidadeAsync(AppDbContext db, CancellationToken ct)
    {
        var config = await db.Configuracoes
            .FirstOrDefaultAsync(ct);

        if (config is not null)
            return config;

        config = Configuracao.CriarPadrao();
        await db.Configuracoes.AddAsync(config, ct);
        await db.SaveChangesAsync(ct);

        return config;
    }

    public async Task<ConfiguracaoDto> ObterAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return ConfiguracaoDto.De(await ObterEntidadeAsync(db, ct));
    }

    public async Task<Result> SalvarAsync(ConfiguracaoDto dto, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var config = await ObterEntidadeAsync(db, ct);
            config.Atualizar(dto.DiasAntecedenciaLembrete, dto.IntervaloEnvioLoteMs, dto.TemplateLembrete, dto.TemplateAtraso);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }
}
