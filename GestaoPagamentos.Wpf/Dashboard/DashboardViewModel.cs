using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Application.Relatorios;
using GestaoPagamentos.Wpf.Common;
using GestaoPagamentos.Wpf.Services;

namespace GestaoPagamentos.Wpf.Dashboard;

public sealed partial class DashboardViewModel(RelatorioService relatorioService) : ViewModelBase
{
    public ObservableCollection<ParcelaAtrasadaDto> Atrasados { get; } = [];

    [ObservableProperty] private ResumoDto? _resumo;

    public override Task InicializarAsync() => AtualizarAsync();

    [RelayCommand]
    private Task AtualizarAsync() => ExecutarAsync(async () =>
    {
        Resumo = await relatorioService.ObterResumoAsync();
        var lista = await relatorioService.ListarAtrasadosAsync();
        Atrasados.Clear();
        foreach (var a in lista) Atrasados.Add(a);
    });
}
