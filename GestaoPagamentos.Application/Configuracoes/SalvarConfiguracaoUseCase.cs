using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Application.Configuracoes;

public sealed class SalvarConfiguracaoUseCase(ObterConfiguracaoUseCase obter, IUnitOfWork uow)
{
    public async Task<Result> ExecuteAsync(ConfiguracaoDto dto, CancellationToken ct = default)
    {
        try
        {
            var config = await obter.ObterEntidadeAsync(ct);
            config.Atualizar(dto.DiasAntecedenciaLembrete, dto.IntervaloEnvioLoteMs, dto.TemplateLembrete, dto.TemplateAtraso);
            await uow.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }
}
