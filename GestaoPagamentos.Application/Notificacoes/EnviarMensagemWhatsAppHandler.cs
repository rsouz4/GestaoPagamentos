using Microsoft.Extensions.Logging;

namespace GestaoPagamentos.Application.Notificacoes;

public sealed record EnviarMensagemWhatsAppCommand(
    Guid NotificacaoId,
    string Telefone,
    string Mensagem);

public sealed record EnviarMensagemWhatsAppResult(
    bool Sucesso,
    string? Mensagem = null);

public sealed class EnviarMensagemWhatsAppHandler(
    IWhatsAppAutomationService automationService,
    ILogger<EnviarMensagemWhatsAppHandler> logger)
{
    private readonly ILogger _logger = logger;
    public async Task<EnviarMensagemWhatsAppResult> Handle(
        EnviarMensagemWhatsAppCommand command,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(command.Telefone))
            {
                var msg = $"Telefone inválido para notificação {command.NotificacaoId}";
                logger.LogWarning(msg);
                return new EnviarMensagemWhatsAppResult(false, msg);
            }

            if (string.IsNullOrWhiteSpace(command.Mensagem))
            {
                var msg = $"Mensagem vazia para notificação {command.NotificacaoId}";
                logger.LogWarning(msg);
                return new EnviarMensagemWhatsAppResult(false, msg);
            }

            _logger.LogInformation(
                "Iniciando envio WhatsApp para notificação {NotificacaoId}, telefone: {Telefone}",
                command.NotificacaoId,
                command.Telefone);

            var enviado = await automationService.EnviarMensagemAsync(
                command.Telefone,
                command.Mensagem,
                ct);

            if (enviado)
            {
                logger.LogInformation(
                    "Mensagem WhatsApp enviada com sucesso para notificação {NotificacaoId}",
                    command.NotificacaoId);
                return new EnviarMensagemWhatsAppResult(true);
            }

            logger.LogWarning(
                "Falha ao enviar mensagem WhatsApp para notificação {NotificacaoId}",
                command.NotificacaoId);
            return new EnviarMensagemWhatsAppResult(false, "Falha ao enviar mensagem via WhatsApp Web");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Envio WhatsApp cancelado para notificação {NotificacaoId}", command.NotificacaoId);
            return new EnviarMensagemWhatsAppResult(false, "Envio cancelado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao enviar mensagem WhatsApp para notificação {NotificacaoId}", command.NotificacaoId);
            return new EnviarMensagemWhatsAppResult(false, $"Erro ao enviar mensagem: {ex.Message}");
        }
    }
}
