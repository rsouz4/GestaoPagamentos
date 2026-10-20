using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Wpf.Common;
using GestaoPagamentos.Wpf.Devedores;
using GestaoPagamentos.Wpf.Shell;
using System.Collections.ObjectModel;

namespace GestaoPagamentos.Wpf.Dividas;

public sealed partial class DividaFormViewModel(
    DevedorService devedorService,
    DividaService dividaService,
    INavigator navigator,
    AppEvents eventos) : ViewModelBase
{
    public ObservableCollection<DevedorDto> Devedores { get; } = [];
    public IReadOnlyList<TipoDivida> Tipos { get; } = [TipoDivida.Avulsa, TipoDivida.Parcelada];

    [ObservableProperty] private DevedorDto? _devedor;
    [ObservableProperty] private string _descricao = string.Empty;

    private string? _valorEmprestado;
    public string? ValorEmprestado
    {
        get => _valorEmprestado;
        set
        {
            if (Parsing.TentarDecimal(value, out var v))
            {
                value = v.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            }

            if (SetProperty(ref _valorEmprestado, value))
            {
                OnPropertyChanged(nameof(ValorAReceberTexto));
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExibeParcelas))]
    private TipoDivida _tipo = TipoDivida.Avulsa;
    [ObservableProperty] private int _quantidadeParcelas = 1;
    [ObservableProperty] private DateTime _primeiroVencimento = DateTime.Today.AddDays(30);
    [ObservableProperty] private string? _observacao;

    public bool ExibeParcelas => Tipo == TipoDivida.Parcelada;

    public string ValorAReceberTexto =>
        Parsing.TentarDecimal(ValorEmprestado, out var v) && v > 0
            ? $"Valor a receber (+{Divida.TaxaJurosPadrao:P0} de juros): {decimal.Round(v * (1 + Divida.TaxaJurosPadrao), 2, MidpointRounding.AwayFromZero):C}"
            : $"Valor a receber: acrescenta {Divida.TaxaJurosPadrao:P0} de juros";

    public override async Task InicializarAsync()
    {
        await ExecutarAsync(async () =>
        {
            var lista = await devedorService.ListarAsync();
            Devedores.Clear();
            foreach (var d in lista) Devedores.Add(d);
        });
    }

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async () =>
    {
        if (Devedor is null)
        {
            Erro = "Selecione o devedor.";
            return;
        }

        if (!Parsing.TentarDecimal(ValorEmprestado, out var valor))
        {
            Erro = "Valor emprestado inválido.";
            return;
        }

        var parcelas = Tipo == TipoDivida.Avulsa ? 1 : QuantidadeParcelas;
        var cmd = new CriarDividaCommand(
            Devedor.Id,
            Descricao,
            valor,
            DateOnly.FromDateTime(PrimeiroVencimento),
            Tipo, parcelas,
            Observacao);

        var r = await dividaService.CriarAsync(cmd);
        if (!r.Sucesso) { Erro = r.Erro; return; }

        eventos.MostrarToast("Dívida criada.");
        await navigator.NavegarAsync<DividaDetalheViewModel>(vm => vm.CarregarAsync(r.Valor));
    });

    [RelayCommand]
    private Task CancelarAsync() => navigator.NavegarAsync<DividasViewModel>();
}
