using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Wpf.Common;
using GestaoPagamentos.Wpf.Shell;
using System.Collections.ObjectModel;

namespace GestaoPagamentos.Wpf.Dividas;

public sealed record FiltroStatus(string Titulo, StatusDivida? Status);

public sealed partial class DividasViewModel(DividaService dividaService, INavigator navigator) : ViewModelBase
{
    public ObservableCollection<DividaResumoDto> Itens { get; } = [];

    public IReadOnlyList<FiltroStatus> Filtros { get; } =
    [
        new("Todas", null),
        new("Ativas", StatusDivida.Ativa),
        new("Quitadas", StatusDivida.Quitada),
        new("Canceladas", StatusDivida.Cancelada)
    ];

    [ObservableProperty] private FiltroStatus _filtro = null!;
    [ObservableProperty] private DividaResumoDto? _selecionada;

    public override Task InicializarAsync()
    {
        Filtro = Filtros[1];
        return Task.CompletedTask;
    }

    partial void OnFiltroChanged(FiltroStatus value) => _ = CarregarAsync();

    [RelayCommand]
    private Task CarregarAsync() => ExecutarAsync(async () =>
    {
        var status = Filtro?.Status;
        var lista = await dividaService.ListarAsync(status);
        Itens.Clear();
        foreach (var d in lista) Itens.Add(d);
    });

    [RelayCommand]
    private Task NovaAsync() => navigator.NavegarAsync<DividaFormViewModel>();

    [RelayCommand]
    private Task AbrirAsync()
    {
        if (Selecionada is not { } d) return Task.CompletedTask;
        return navigator.NavegarAsync<DividaDetalheViewModel>(vm => vm.CarregarAsync(d.Id));
    }
}
