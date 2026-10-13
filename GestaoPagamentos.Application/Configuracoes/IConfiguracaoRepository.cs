using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Domain.Configuracoes;

namespace GestaoPagamentos.Application.Configuracoes;

public interface IConfiguracaoRepository
{
    Task<Configuracao?> ObterAsync(CancellationToken ct = default);
    Task AdicionarAsync(Configuracao configuracao, CancellationToken ct = default);
}

public sealed record ConfiguracaoDto(
    int DiasAntecedenciaLembrete,
    int IntervaloEnvioLoteMs,
    string TemplateLembrete,
    string TemplateAtraso)
{
    public static ConfiguracaoDto De(Configuracao c) =>
        new(c.DiasAntecedenciaLembrete, c.IntervaloEnvioLoteMs, c.TemplateLembrete, c.TemplateAtraso);
}
