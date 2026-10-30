using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Domain.Notificacoes;
using GestaoPagamentos.Wpf.Common;
using System.Collections.ObjectModel;

namespace GestaoPagamentos.Wpf.Notificacoes;

public sealed partial class NotificacoesViewModel(NotificacaoService notificacaoService) : ViewModelBase
{
    public ObservableCollection<NotificacaoDto> Itens { get; } = [];

    [ObservableProperty] private NotificacaoDto? _selecionada;
    [ObservableProperty] private string _mensagem = string.Empty;

    public override Task InicializarAsync() => CarregarAsync();

    partial void OnSelecionadaChanged(NotificacaoDto? value) => Mensagem = value?.Mensagem ?? string.Empty;

    [RelayCommand]
    private Task CarregarAsync() => ExecutarAsync(async () =>
    {
        var lista = await notificacaoService.ListarAsync(StatusNotificacao.Enviada, null);
        Itens.Clear();
        foreach (var n in lista.Where(n => n.Tipo != TipoNotificacao.PagamentoRecebido)
                               .OrderByDescending(n => n.DataEnvio ?? n.DataGeracao))
            Itens.Add(n);
    });
}
