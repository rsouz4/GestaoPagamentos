using GestaoPagamentos.Application.Configuracoes;
using GestaoPagamentos.Domain.Configuracoes;

namespace GestaoPagamentos.Tests.Servicos;

public sealed class ConfiguracaoServiceTests : IDisposable
{
    private readonly Ambiente _a = new();
    public void Dispose() => _a.Dispose();

    [Fact]
    public async Task Obter_PrimeiraVez_CriaConfiguracaoPadrao()
    {
        var dto = await _a.Configuracoes.ObterAsync();

        Assert.Equal(3, dto.DiasAntecedenciaLembrete);
        Assert.Equal(1500, dto.IntervaloEnvioLoteMs);
        Assert.Equal(Configuracao.TemplateLembretePadrao, dto.TemplateLembrete);
        Assert.Equal(Configuracao.TemplateAtrasoPadrao, dto.TemplateAtraso);
    }

    [Fact]
    public async Task Salvar_Valido_Persiste()
    {
        var r = await _a.Configuracoes.SalvarAsync(new ConfiguracaoDto(5, 40000, "Lembrete {Nome}", "Atraso {Nome}"));

        Assert.True(r.Sucesso);
        var dto = await _a.Configuracoes.ObterAsync();
        Assert.Equal(5, dto.DiasAntecedenciaLembrete);
        Assert.Equal(40000, dto.IntervaloEnvioLoteMs);
        Assert.Equal("Lembrete {Nome}", dto.TemplateLembrete);
        Assert.Equal("Atraso {Nome}", dto.TemplateAtraso);
    }

    [Fact]
    public async Task Salvar_DiasNegativos_FalhaESemAlterarOValorSalvo()
    {
        var r = await _a.Configuracoes.SalvarAsync(new ConfiguracaoDto(-1, 1000, "a", "b"));

        Assert.False(r.Sucesso);
        Assert.Equal("A antecedência do lembrete não pode ser negativa.", r.Erro);
        Assert.Equal(3, (await _a.Configuracoes.ObterAsync()).DiasAntecedenciaLembrete);
    }

    [Fact]
    public async Task Salvar_TemplateVazio_Falha()
    {
        var r = await _a.Configuracoes.SalvarAsync(new ConfiguracaoDto(3, 1000, " ", "b"));

        Assert.False(r.Sucesso);
        Assert.Equal("Os templates de mensagem são obrigatórios.", r.Erro);
    }
}
