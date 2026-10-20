using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Wpf.Common;
using GestaoPagamentos.Wpf.Shell;

namespace GestaoPagamentos.Wpf.Devedores;

public sealed partial class DevedoresViewModel(DevedorService devedorService, IDialogService dialogs, AppEvents eventos) : ViewModelBase
{
    public ObservableCollection<DevedorDto> Itens { get; } = [];

    [ObservableProperty] private string? _filtro;
    [ObservableProperty] private DevedorDto? _selecionado;

    [ObservableProperty] private Guid? _editandoId;
    [ObservableProperty] private string _nome = string.Empty;
    [ObservableProperty] private string? _telefone;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _observacao;

    public string TituloFormulario => EditandoId is null ? "Novo devedor" : "Editar devedor";

    public override Task InicializarAsync() => BuscarAsync();

    partial void OnEditandoIdChanged(Guid? value) => OnPropertyChanged(nameof(TituloFormulario));

    partial void OnSelecionadoChanged(DevedorDto? value)
    {
        if (value is null) return;
        EditandoId = value.Id;
        Nome = value.Nome;
        Telefone = value.Telefone;
        Email = value.Email;
        Observacao = value.Observacao;
    }

    [RelayCommand]
    private Task BuscarAsync() => ExecutarAsync(async () =>
    {
        var lista = await devedorService.ListarAsync(Filtro);
        Itens.Clear();
        foreach (var d in lista) Itens.Add(d);
    });

    [RelayCommand]
    private void Novo()
    {
        Selecionado = null;
        EditandoId = null;
        Nome = string.Empty;
        Telefone = Email = Observacao = null;
        Erro = null;
    }

    [RelayCommand]
    private Task SalvarAsync() => ExecutarAsync(async () =>
    {
        var cmd = new SalvarDevedorCommand(EditandoId, Nome, Telefone, Email, Observacao);
        var r = await devedorService.SalvarAsync(cmd);
        if (!r.Sucesso)
        {
            Erro = r.Erro;
            return;
        }

        eventos.MostrarToast("Devedor salvo.");
        Novo();
        await BuscarAsync();
    });

    [RelayCommand]
    private Task ExcluirAsync() => ExecutarAsync(async () =>
    {
        if (EditandoId is not { } id) return;
        if (!dialogs.Confirmar($"Excluir o devedor \"{Nome}\"?")) return;

        var r = await devedorService.ExcluirAsync(id);
        if (!r.Sucesso)
        {
            Erro = r.Erro;
            return;
        }

        eventos.MostrarToast("Devedor excluído.");
        Novo();
        await BuscarAsync();
    });
}
