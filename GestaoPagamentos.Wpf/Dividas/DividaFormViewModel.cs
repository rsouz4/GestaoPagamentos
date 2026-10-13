using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Wpf.Common;
using GestaoPagamentos.Wpf.Shell;

namespace GestaoPagamentos.Wpf.Dividas;

public sealed partial class DividaFormViewModel(UseCaseRunner useCases, INavigator navigator, AppEvents eventos) : ViewModelBase
{
    public ObservableCollection<DevedorDto> Devedores { get; } = [];
    public IReadOnlyList<TipoDivida> Tipos { get; } = [TipoDivida.Avulsa, TipoDivida.Parcelada];

    [ObservableProperty] private DevedorDto? _devedor;
    [ObservableProperty] private string _descricao = string.Empty;
    [ObservableProperty] private string? _valorTotal;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExibeParcelas))]
    private TipoDivida _tipo = TipoDivida.Avulsa;
    [ObservableProperty] private int _quantidadeParcelas = 1;
    [ObservableProperty] private DateTime _primeiroVencimento = DateTime.Today.AddDays(30);
    [ObservableProperty] private string? _observacao;

    public bool ExibeParcelas => Tipo == TipoDivida.Parcelada;

    public override async Task InicializarAsync()
    {
        await ExecutarAsync(async () =>
        {
            var lista = await useCases.ExecutarAsync((ListarDevedoresUseCase u) => u.ExecuteAsync());
            Devedores.Clear();
            foreach (var d in lista) Devedores.Add(d);
        });
    }

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async () =>
    {
        if (Devedor is null) { Erro = "Selecione o devedor."; return; }
        if (!Parsing.TentarDecimal(ValorTotal, out var valor)) { Erro = "Valor total inválido."; return; }

        var parcelas = Tipo == TipoDivida.Avulsa ? 1 : QuantidadeParcelas;
        var cmd = new CriarDividaCommand(
            Devedor.Id, Descricao, valor, DateOnly.FromDateTime(PrimeiroVencimento), Tipo, parcelas, Observacao);

        var r = await useCases.ExecutarAsync((CriarDividaUseCase u) => u.ExecuteAsync(cmd));
        if (!r.Sucesso) { Erro = r.Erro; return; }

        eventos.MostrarToast("Dívida criada.");
        await navigator.NavegarAsync<DividaDetalheViewModel>(vm => vm.CarregarAsync(r.Valor));
    });

    [RelayCommand]
    private Task CancelarAsync() => navigator.NavegarAsync<DividasViewModel>();
}
