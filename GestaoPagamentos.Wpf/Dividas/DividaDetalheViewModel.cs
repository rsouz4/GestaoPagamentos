using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Application.Pagamentos;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Wpf.Common;
using GestaoPagamentos.Wpf.Shell;

namespace GestaoPagamentos.Wpf.Dividas;

public sealed partial class DividaDetalheViewModel(
    UseCaseRunner useCases,
    INavigator navigator,
    IDialogService dialogs,
    AppEvents eventos) : ViewModelBase
{
    private Guid _dividaId;

    public ObservableCollection<ParcelaDto> Parcelas { get; } = [];
    public ObservableCollection<PagamentoDto> Pagamentos { get; } = [];

    [ObservableProperty] private DividaDetalheDto? _divida;
    [ObservableProperty] private ParcelaDto? _parcelaSelecionada;
    [ObservableProperty] private PagamentoDto? _pagamentoSelecionado;
    [ObservableProperty] private string? _valorPagamento;
    [ObservableProperty] private DateTime _dataPagamento = DateTime.Today;
    [ObservableProperty] private string? _observacaoPagamento;

    public bool PodeEditar => Divida?.Status == StatusDivida.Ativa;

    public async Task CarregarAsync(Guid dividaId)
    {
        _dividaId = dividaId;
        await RecarregarAsync();
    }

    private Task RecarregarAsync() => ExecutarAsync(async () =>
    {
        var id = _dividaId;
        var selecionada = ParcelaSelecionada?.Id;
        var r = await useCases.ExecutarAsync((ObterDividaUseCase u) => u.ExecuteAsync(id));
        if (!r.Sucesso) { Erro = r.Erro; return; }

        Divida = r.Valor;
        OnPropertyChanged(nameof(PodeEditar));
        Parcelas.Clear();
        foreach (var p in r.Valor!.Parcelas) Parcelas.Add(p);

        ParcelaSelecionada = Parcelas.FirstOrDefault(p => p.Id == selecionada)
                             ?? Parcelas.FirstOrDefault(p => p.ValorRestante > 0);
        AtualizarPagamentos();
    });

    partial void OnParcelaSelecionadaChanged(ParcelaDto? value)
    {
        AtualizarPagamentos();
        ValorPagamento = value is { ValorRestante: > 0 } ? value.ValorRestante.ToString("N2") : null;
    }

    private void AtualizarPagamentos()
    {
        Pagamentos.Clear();
        if (ParcelaSelecionada is null) return;
        foreach (var p in ParcelaSelecionada.Pagamentos) Pagamentos.Add(p);
    }

    [RelayCommand]
    private Task RegistrarPagamentoAsync() => ExecutarAsync(async () =>
    {
        if (ParcelaSelecionada is null) { Erro = "Selecione uma parcela."; return; }
        if (!Parsing.TentarDecimal(ValorPagamento, out var valor)) { Erro = "Valor inválido."; return; }

        var cmd = new RegistrarPagamentoCommand(
            ParcelaSelecionada.Id, valor, DateOnly.FromDateTime(DataPagamento), ObservacaoPagamento);
        var r = await useCases.ExecutarAsync((RegistrarPagamentoUseCase u) => u.ExecuteAsync(cmd));
        if (!r.Sucesso) { Erro = r.Erro; return; }

        ObservacaoPagamento = null;
        eventos.MostrarToast("Pagamento registrado.");
        eventos.NotificarNotificacoesAlteradas();
        await RecarregarInternoAsync();
    });

    [RelayCommand]
    private Task ExcluirPagamentoAsync() => ExecutarAsync(async () =>
    {
        if (PagamentoSelecionado is not { } p) return;
        if (!dialogs.Confirmar("Excluir o pagamento selecionado?")) return;

        var r = await useCases.ExecutarAsync((ExcluirPagamentoUseCase u) => u.ExecuteAsync(p.Id));
        if (!r.Sucesso) { Erro = r.Erro; return; }

        eventos.MostrarToast("Pagamento excluído.");
        await RecarregarInternoAsync();
    });

    [RelayCommand]
    private Task CancelarDividaAsync() => ExecutarAsync(async () =>
    {
        if (!dialogs.Confirmar("Cancelar esta dívida? As notificações pendentes serão descartadas.")) return;

        var id = _dividaId;
        var r = await useCases.ExecutarAsync((CancelarDividaUseCase u) => u.ExecuteAsync(id));
        if (!r.Sucesso) { Erro = r.Erro; return; }

        eventos.NotificarNotificacoesAlteradas();
        await RecarregarInternoAsync();
    });

    [RelayCommand]
    private Task VoltarAsync() => navigator.NavegarAsync<DividasViewModel>();

    private async Task RecarregarInternoAsync()
    {
        var id = _dividaId;
        var selecionada = ParcelaSelecionada?.Id;
        var r = await useCases.ExecutarAsync((ObterDividaUseCase u) => u.ExecuteAsync(id));
        if (!r.Sucesso) { Erro = r.Erro; return; }

        Divida = r.Valor;
        OnPropertyChanged(nameof(PodeEditar));
        Parcelas.Clear();
        foreach (var p in r.Valor!.Parcelas) Parcelas.Add(p);
        ParcelaSelecionada = Parcelas.FirstOrDefault(p => p.Id == selecionada);
    }
}
