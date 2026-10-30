namespace GestaoPagamentos.Application.Notificacoes;

public interface IWhatsAppAutomationService
{
    /// <summary>
    /// Envia uma mensagem via WhatsApp Web usando automação com Playwright.
    /// </summary>
    /// <param name="telefone">Telefone normalizado (ex: 5511987654321)</param>
    /// <param name="mensagem">Texto da mensagem a ser enviada</param>
    /// <param name="ct">Token de cancelamento</param>
    /// <returns>True se a mensagem foi enviada com sucesso, False caso contrário</returns>
    Task<bool> EnviarMensagemAsync(string telefone, string mensagem, CancellationToken ct = default);

    /// <summary>
    /// Verifica se o serviço está autenticado e pronto para usar.
    /// Na primeira execução, pode exigir autenticação manual (QR Code).
    /// </summary>
    /// <param name="ct">Token de cancelamento</param>
    /// <returns>True se autenticado, False caso contrário</returns>
    Task<bool> VerificarAutenticacaoAsync(CancellationToken ct = default);

    /// <summary>
    /// Fecha o navegador e limpa recursos.
    /// </summary>
    /// <param name="ct">Token de cancelamento</param>
    Task FecharAsync(CancellationToken ct = default);
}
