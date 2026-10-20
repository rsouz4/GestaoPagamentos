using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Domain.Notificacoes;
using GestaoPagamentos.Wpf.Common;
using GestaoPagamentos.Wpf.Shell;
using System.Collections.ObjectModel;

namespace GestaoPagamentos.Wpf.Notificacoes;

public sealed partial class NotificacoesViewModel(
    NotificacaoService notificacaoService,
    IDialogService dialogs,
    AppEvents eventos) : ViewModelBase
{
    private CancellationTokenSource? _loteCts;

    public ObservableCollection<NotificacaoDto> Itens { get; } = [];

    [ObservableProperty] private NotificacaoDto? _selecionada;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private string? _progresso;
    [ObservableProperty] private bool _loteEmAndamento;

    public int TotalAtrasadosPendentes => Itens.Count(n => n.Tipo == TipoNotificacao.Atraso && n.TelefoneValido);

    public override Task InicializarAsync() => CarregarAsync();

    partial void OnSelecionadaChanged(NotificacaoDto? value) => Mensagem = value?.Mensagem ?? string.Empty;

    [RelayCommand]
    private Task GerarAsync() => ExecutarAsync(async () =>
    {
        var novas = await notificacaoService.GerarAsync();
        eventos.MostrarToast(novas == 0 ? "Nenhuma notificação nova." : $"{novas} notificação(ões) gerada(s).");
        await RecarregarAsync();
    });

    [RelayCommand]
    private Task CarregarAsync() => ExecutarAsync(RecarregarAsync);

    private async Task RecarregarAsync()
    {
        var lista = await notificacaoService.ListarAsync(StatusNotificacao.Pendente, null);
        Itens.Clear();
        foreach (var n in lista) Itens.Add(n);
        OnPropertyChanged(nameof(TotalAtrasadosPendentes));
        eventos.NotificarNotificacoesAlteradas();
    }

    [RelayCommand]
    private Task EnviarAsync() => ExecutarAsync(async () =>
    {
        if (Selecionada is not { } n) return;
        var msg = Mensagem;
        var r = await notificacaoService.EnviarAsync(n.Id, msg);
        if (!r.Sucesso) { Erro = r.Erro; return; }

        eventos.MostrarToast("WhatsApp aberto. Confirme o envio no aplicativo.");
        await RecarregarAsync();
    });

    [RelayCommand]
    private Task IgnorarAsync() => ExecutarAsync(async () =>
    {
        if (Selecionada is not { } n) return;
        await notificacaoService.IgnorarAsync(n.Id);
        await RecarregarAsync();
    });

    [RelayCommand]
    private async Task EnviarAtrasadosAsync()
    {
        var itens = Itens.Where(n => n.Tipo == TipoNotificacao.Atraso).ToList();
        if (itens.Count == 0) { dialogs.Aviso("Não há notificações de atraso pendentes."); return; }

        if (!dialogs.Confirmar(
                $"Serão abertas {itens.Count} conversas no WhatsApp, uma por vez. " +
                "Em cada uma, você ainda precisa clicar em Enviar. Continuar?"))
            return;

        _loteCts = new CancellationTokenSource();
        LoteEmAndamento = true;
        Erro = null;

        try
        {
            var progresso = new Progress<LoteProgresso>(p => Progresso = $"Abrindo {p.Atual}/{p.Total}: {p.DevedorNome}");
            var cts = _loteCts;
            var r = await notificacaoService.EnviarLoteAsync(itens, progresso, cts.Token);

            eventos.MostrarToast(
                $"Lote {(r.Cancelado ? "cancelado" : "concluído")}: {r.Enviadas} aberta(s), {r.Puladas} pulada(s).");
        }
        catch (Exception ex)
        {
            Erro = ex.Message;
        }
        finally
        {
            LoteEmAndamento = false;
            Progresso = null;
            _loteCts?.Dispose();
            _loteCts = null;
            await ExecutarAsync(RecarregarAsync);
        }
    }

    [RelayCommand]
    private void CancelarLote() => _loteCts?.Cancel();
}
