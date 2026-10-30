using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Domain.Notificacoes;
using GestaoPagamentos.Wpf.Notificacoes;
using GestaoPagamentos.Wpf.Shell;
using Microsoft.Extensions.Logging;

namespace GestaoPagamentos.Wpf.Services;

public sealed class NotificacaoAutoEnvioService(
    NotificacaoService notificacaoService,
    IWhatsAppAutomationService automationService,
    AppEvents appEvents,
    ILogger<NotificacaoAutoEnvioService> logger)
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _executando;

    /// <summary>
    /// Executa envio automático de notificações pendentes de atraso ao iniciar a aplicação.
    /// </summary>
    public async Task ExecutarEnvioAutomaticoAoIniciarAsync(CancellationToken ct = default)
    {
        // Evita múltiplas execuções simultâneas
        if (_executando)
        {
            logger.LogWarning("Auto-envio já em execução, ignorando nova chamada");
            return;
        }

        if (!await _semaphore.WaitAsync(0))
        {
            logger.LogWarning("Aguardando conclusão do auto-envio anterior...");
            return;
        }

        try
        {
            _executando = true;
            logger.LogInformation("Iniciando auto-envio de notificações ao startup");

            await notificacaoService.GerarAsync(ct);

            // Busca notificações pendentes de atraso e de vencimento
            var notificacoes = (await notificacaoService.ListarAsync(
                    StatusNotificacao.Pendente,
                    null,
                    ct))
                .Where(n => n.Tipo is TipoNotificacao.Atraso or TipoNotificacao.Vencimento)
                .ToList();

            if (notificacoes.Count == 0)
            {
                logger.LogInformation("Nenhuma notificação de atraso pendente para enviar");
                return;
            }

            var comTelefonValido = notificacoes.ToList();

            if (comTelefonValido.Count == 0)
            {
                logger.LogInformation("Nenhuma notificação pendente para enviar");
                return;
            }

            logger.LogInformation(
                "Iniciando envio automático de {Quantidade} notificação(ões) de atraso",
                comTelefonValido.Count);

            appEvents.MostrarToast($"🤖 Enviando {comTelefonValido.Count} notificação(ões) automaticamente...");

            int enviadas = 0;
            int falhadas = 0;

            for (int i = 0; i < comTelefonValido.Count; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    logger.LogWarning("Auto-envio cancelado pelo usuário");
                    break;
                }

                var notificacao = comTelefonValido[i];

                try
                {
                    logger.LogInformation(
                        "Enviando notificação {Numero}/{Total} para {Devedor}",
                        i + 1,
                        comTelefonValido.Count,
                        notificacao.DevedorNome);

                    var resultado = await notificacaoService.EnviarViaWhatsAppWebAsync(
                        notificacao.Id,
                        automationService,
                        ct);

                    if (resultado)
                    {
                        enviadas++;
                        logger.LogInformation(
                            "✓ Notificação enviada com sucesso: {DevedorNome}",
                            notificacao.DevedorNome);
                    }
                    else
                    {
                        falhadas++;
                        logger.LogWarning(
                            "✗ Falha ao enviar notificação para {DevedorNome}",
                            notificacao.DevedorNome);
                    }
                }
                catch (Exception ex)
                {
                    falhadas++;
                    logger.LogError(
                        ex,
                        "Exceção ao enviar notificação para {DevedorNome}",
                        notificacao.DevedorNome);
                }

                // Intervalo entre envios para evitar bloqueio do WhatsApp
                if (i < comTelefonValido.Count - 1)
                {
                    await Task.Delay(2000, ct);
                }
            }

            // Toast final com resumo
            var mensagem = falhadas == 0
                ? $"✅ Auto-envio concluído: {enviadas} mensagem(ns) enviada(s)"
                : $"⚠️ Auto-envio concluído: {enviadas} enviada(s), {falhadas} falhada(s)";

            appEvents.MostrarToast(mensagem);

            logger.LogInformation(
                "Auto-envio finalizado: {Enviadas} enviadas, {Falhadas} falhadas",
                enviadas,
                falhadas);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Auto-envio foi cancelado");
            appEvents.MostrarToast("❌ Auto-envio foi cancelado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro fatal durante auto-envio de notificações");
            appEvents.MostrarToast("❌ Erro ao enviar notificações automaticamente");
        }
        finally
        {
            _executando = false;
            _semaphore.Release();
        }
    }
}
