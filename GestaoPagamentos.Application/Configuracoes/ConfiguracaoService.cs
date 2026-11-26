using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Persistence;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Configuracoes;

namespace GestaoPagamentos.Application.Configuracoes;

public sealed class ConfiguracaoService(IUnitOfWorkFactory uowFactory)
{
    /// <summary>Devolve a configuração do usuário, criando e gravando a padrão na primeira vez.</summary>
    internal static async Task<Configuracao> ObterOuCriarAsync(IUnitOfWork uow, CancellationToken ct)
    {
        var config = await uow.Configuracoes.ObterAsync(ct);
        if (config is not null)
            return config;

        config = Configuracao.CriarPadrao();
        await uow.Configuracoes.AdicionarAsync(config, ct);
        await uow.SalvarAsync(ct);

        return config;
    }

    public async Task<ConfiguracaoDto> ObterAsync(CancellationToken ct = default)
    {
        await using var uow = await uowFactory.CriarAsync(ct);
        return ConfiguracaoDto.De(await ObterOuCriarAsync(uow, ct));
    }

    public async Task<Result> SalvarAsync(ConfiguracaoDto dto, CancellationToken ct = default)
    {
        try
        {
            await using var uow = await uowFactory.CriarAsync(ct);
            var config = await ObterOuCriarAsync(uow, ct);
            config.Atualizar(dto.DiasAntecedenciaLembrete, dto.IntervaloEnvioLoteMs, dto.TemplateLembrete, dto.TemplateAtraso);
            await uow.SalvarAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }
}
