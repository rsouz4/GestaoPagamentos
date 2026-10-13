using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Application.Relatorios;
using GestaoPagamentos.Wpf.Common;

namespace GestaoPagamentos.Wpf.Dashboard;

public sealed partial class DashboardViewModel(UseCaseRunner useCases) : ViewModelBase
{
    public ObservableCollection<ParcelaAtrasadaDto> Atrasados { get; } = [];

    [ObservableProperty] private ResumoDto? _resumo;

    public override Task InicializarAsync() => AtualizarAsync();

    [RelayCommand]
    private Task AtualizarAsync() => ExecutarAsync(async () =>
    {
        Resumo = await useCases.ExecutarAsync((ObterResumoUseCase u) => u.ExecuteAsync());
        var lista = await useCases.ExecutarAsync((ListarAtrasadosUseCase u) => u.ExecuteAsync());
        Atrasados.Clear();
        foreach (var a in lista) Atrasados.Add(a);
    });
}
