using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Application.Configuracoes;
using GestaoPagamentos.Wpf.Common;
using GestaoPagamentos.Wpf.Shell;

namespace GestaoPagamentos.Wpf.Configuracoes;

public sealed partial class ConfiguracoesViewModel(UseCaseRunner useCases, AppEvents eventos) : ViewModelBase
{
    [ObservableProperty] private int _diasAntecedenciaLembrete;
    [ObservableProperty] private int _intervaloEnvioLoteMs;
    [ObservableProperty] private string _templateLembrete = string.Empty;
    [ObservableProperty] private string _templateAtraso = string.Empty;

    public override Task InicializarAsync() => ExecutarAsync(async () =>
    {
        var c = await useCases.ExecutarAsync((ObterConfiguracaoUseCase u) => u.ExecuteAsync());
        DiasAntecedenciaLembrete = c.DiasAntecedenciaLembrete;
        IntervaloEnvioLoteMs = c.IntervaloEnvioLoteMs;
        TemplateLembrete = c.TemplateLembrete;
        TemplateAtraso = c.TemplateAtraso;
    });

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async () =>
    {
        var dto = new ConfiguracaoDto(DiasAntecedenciaLembrete, IntervaloEnvioLoteMs, TemplateLembrete, TemplateAtraso);
        var r = await useCases.ExecutarAsync((SalvarConfiguracaoUseCase u) => u.ExecuteAsync(dto));
        if (!r.Sucesso) { Erro = r.Erro; return; }
        eventos.MostrarToast("Configurações salvas.");
    });
}
