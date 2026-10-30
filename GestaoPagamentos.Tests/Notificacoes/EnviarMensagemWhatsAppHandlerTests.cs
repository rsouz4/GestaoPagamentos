using GestaoPagamentos.Application.Notificacoes;
using Microsoft.Extensions.Logging;
using Moq;

namespace GestaoPagamentos.Tests;

public sealed class EnviarMensagemWhatsAppHandlerTests
{
    [Fact]
    public async Task Handle_ComTelefoneValido_DeveEnviarMensagem()
    {
        // Arrange
        var automationServiceMock = new Mock<IWhatsAppAutomationService>();
        automationServiceMock
            .Setup(s => s.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var loggerMock = new Mock<ILogger<EnviarMensagemWhatsAppHandler>>();

        var handler = new EnviarMensagemWhatsAppHandler(automationServiceMock.Object, loggerMock.Object);
        var command = new EnviarMensagemWhatsAppCommand(
            Guid.NewGuid(),
            "5511987654321",
            "Teste de mensagem");

        // Act
        var resultado = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(resultado.Sucesso);
        Assert.Null(resultado.Mensagem);
        automationServiceMock.Verify(
            s => s.EnviarMensagemAsync("5511987654321", "Teste de mensagem", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ComTelefoneVazio_DeveRetornarFalha()
    {
        // Arrange
        var automationServiceMock = new Mock<IWhatsAppAutomationService>();
        var loggerMock = new Mock<ILogger<EnviarMensagemWhatsAppHandler>>();

        var handler = new EnviarMensagemWhatsAppHandler(automationServiceMock.Object, loggerMock.Object);
        var command = new EnviarMensagemWhatsAppCommand(
            Guid.NewGuid(),
            string.Empty,
            "Teste de mensagem");

        // Act
        var resultado = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.NotNull(resultado.Mensagem);
        automationServiceMock.Verify(
            s => s.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ComMensagemVazia_DeveRetornarFalha()
    {
        // Arrange
        var automationServiceMock = new Mock<IWhatsAppAutomationService>();
        var loggerMock = new Mock<ILogger<EnviarMensagemWhatsAppHandler>>();

        var handler = new EnviarMensagemWhatsAppHandler(automationServiceMock.Object, loggerMock.Object);
        var command = new EnviarMensagemWhatsAppCommand(
            Guid.NewGuid(),
            "5511987654321",
            string.Empty);

        // Act
        var resultado = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.NotNull(resultado.Mensagem);
        automationServiceMock.Verify(
            s => s.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_QuandoServicoFalha_DeveRetornarFalha()
    {
        // Arrange
        var automationServiceMock = new Mock<IWhatsAppAutomationService>();
        automationServiceMock
            .Setup(s => s.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var loggerMock = new Mock<ILogger<EnviarMensagemWhatsAppHandler>>();

        var handler = new EnviarMensagemWhatsAppHandler(automationServiceMock.Object, loggerMock.Object);
        var command = new EnviarMensagemWhatsAppCommand(
            Guid.NewGuid(),
            "5511987654321",
            "Teste de mensagem");

        // Act
        var resultado = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.NotNull(resultado.Mensagem);
    }

    [Fact]
    public async Task Handle_QuandoServicoLancaExcecao_DeveRetornarFalha()
    {
        // Arrange
        var automationServiceMock = new Mock<IWhatsAppAutomationService>();
        automationServiceMock
            .Setup(s => s.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Erro teste"));

        var loggerMock = new Mock<ILogger<EnviarMensagemWhatsAppHandler>>();

        var handler = new EnviarMensagemWhatsAppHandler(automationServiceMock.Object, loggerMock.Object);
        var command = new EnviarMensagemWhatsAppCommand(
            Guid.NewGuid(),
            "5511987654321",
            "Teste de mensagem");

        // Act
        var resultado = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(resultado.Sucesso);
        Assert.NotNull(resultado.Mensagem);
        Assert.Contains("Erro teste", resultado.Mensagem);
    }
}
