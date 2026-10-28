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
    IWhatsAppAutomationService automationService,
    IDialogService dialogs,
    AppEvents eventos) : ViewModelBase
{
    private CancellationTokenSource? _loteCts;

    public ObservableCollection<NotificacaoDto> Itens { get; } = [];

    [ObservableProperty] private NotificacaoDto? _selecionada;
    [ObservableProperty] private string _mensagem = string.Empty;
    [ObservableProperty] private string? _progresso;
    [ObservableProperty] private bool _loteEmAndamento;
    [ObservableProperty] private bool _envioAutomaticoEmAndamento;

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

    [RelayCommand]
    private Task EnviarViaWhatsAppWebAsync() => ExecutarAsync(async () =>
    {
        if (Selecionada is not { } n)
        {
            Erro = "Nenhuma notificação selecionada.";
            return;
        }

        if (!n.TelefoneValido)
        {
            Erro = "Telefone inválido para este devedor.";
            return;
        }

        EnvioAutomaticoEmAndamento = true;
        Progresso = $"Enviando mensagem para {n.DevedorNome}...";
        Erro = null;

        try
        {
            var resultado = await notificacaoService.EnviarViaWhatsAppWebAsync(n.Id, automationService, CancellationToken.None);
            if (resultado)
            {
                eventos.MostrarToast("Mensagem enviada com sucesso via WhatsApp Web!");
                await RecarregarAsync();
            }
            else
            {
                Erro = "Falha ao enviar mensagem. Verifique os logs e tente novamente.";
            }
        }
        catch (Exception ex)
        {
            Erro = $"Erro ao enviar mensagem: {ex.Message}";
        }
        finally
        {
            EnvioAutomaticoEmAndamento = false;
            Progresso = null;
        }
    });

    [RelayCommand]
    private Task EnviarAtrasadosViaAutomacaoAsync() => ExecutarAsync(async () =>
    {
        var itens = Itens.Where(n => n.Tipo == TipoNotificacao.Atraso && n.TelefoneValido).ToList();
        if (itens.Count == 0)
        {
            dialogs.Aviso("Não há notificações de atraso com telefone válido.");
            return;
        }

        if (!dialogs.Confirmar(
                $"Serão enviadas {itens.Count} mensagens via WhatsApp Web (automático). " +
                "Este processo pode levar alguns minutos. Continuar?"))
            return;

        _loteCts = new CancellationTokenSource();
        LoteEmAndamento = true;
        EnvioAutomaticoEmAndamento = true;
        Erro = null;

        try
        {
            int enviadas = 0;
            int falhadas = 0;

            for (int i = 0; i < itens.Count; i++)
            {
                if (_loteCts.Token.IsCancellationRequested)
                    break;

                var item = itens[i];
                Progresso = $"Enviando {i + 1}/{itens.Count}: {item.DevedorNome}";

                try
                {
                    var resultado = await notificacaoService.EnviarViaWhatsAppWebAsync(
                        item.Id, automationService, _loteCts.Token);
                    if (resultado) enviadas++;
                    else falhadas++;
                }
                catch
                {
                    falhadas++;
                }

                // Delay entre envios para evitar bloqueio
                if (i < itens.Count - 1)
                {
                    await Task.Delay(2000, _loteCts.Token);
                }
            }

            var cancelado = _loteCts.Token.IsCancellationRequested;
            eventos.MostrarToast(
                $"Automação {(cancelado ? "cancelada" : "concluída")}: {enviadas} enviada(s), {falhadas} falhada(s).");
        }
        catch (OperationCanceledException)
        {
            eventos.MostrarToast("Automação cancelada pelo usuário.");
        }
        catch (Exception ex)
        {
            Erro = ex.Message;
        }
        finally
        {
            LoteEmAndamento = false;
            EnvioAutomaticoEmAndamento = false;
            Progresso = null;
            _loteCts?.Dispose();
            _loteCts = null;
            await ExecutarAsync(RecarregarAsync);
        }
    });
}

public sealed record LoteProgresso(int Atual, int Total, string DevedorNome);
