using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Tests;

public sealed class AutomacaoWhatsAppTests
{
    [Fact]
    public void Criar_ComParametrosValidos_DeveRetornarEntidade()
    {
        // Arrange
        var notificacaoId = Guid.NewGuid();
        var devedorId = Guid.NewGuid();
        var telefone = "5511987654321";
        var mensagem = "Teste";

        // Act
        var automacao = AutomacaoWhatsApp.Criar(notificacaoId, devedorId, telefone, mensagem);

        // Assert
        Assert.NotEqual(Guid.Empty, automacao.Id);
        Assert.Equal(notificacaoId, automacao.NotificacaoId);
        Assert.Equal(devedorId, automacao.DevedorId);
        Assert.Equal(telefone, automacao.Telefone);
        Assert.Equal(mensagem, automacao.Mensagem);
        Assert.Equal(StatusExecucao.Pendente, automacao.Status);
        Assert.Empty(automacao.Logs);
    }

    [Fact]
    public void Criar_ComNotificacaoIdVazio_DeveAlancarExcecao()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() =>
            AutomacaoWhatsApp.Criar(Guid.Empty, Guid.NewGuid(), "5511987654321", "Teste"));
    }

    [Fact]
    public void Criar_ComDevedorIdVazio_DeveAlancarExcecao()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() =>
            AutomacaoWhatsApp.Criar(Guid.NewGuid(), Guid.Empty, "5511987654321", "Teste"));
    }

    [Fact]
    public void Criar_ComTelefoneVazio_DeveAlancarExcecao()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() =>
            AutomacaoWhatsApp.Criar(Guid.NewGuid(), Guid.NewGuid(), string.Empty, "Teste"));
    }

    [Fact]
    public void Criar_ComMensagemVazia_DeveAlancarExcecao()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() =>
            AutomacaoWhatsApp.Criar(Guid.NewGuid(), Guid.NewGuid(), "5511987654321", string.Empty));
    }

    [Fact]
    public void Iniciar_ComStatusPendente_DeveAlterarParaEmAndamento()
    {
        // Arrange
        var automacao = AutomacaoWhatsApp.Criar(Guid.NewGuid(), Guid.NewGuid(), "5511987654321", "Teste");

        // Act
        automacao.Iniciar();

        // Assert
        Assert.Equal(StatusExecucao.EmAndamento, automacao.Status);
        Assert.Single(automacao.Logs);
    }

    [Fact]
    public void Concluir_ComStatusEmAndamento_DeveAlterarParaConcluida()
    {
        // Arrange
        var automacao = AutomacaoWhatsApp.Criar(Guid.NewGuid(), Guid.NewGuid(), "5511987654321", "Teste");
        automacao.Iniciar();

        // Act
        automacao.Concluir();

        // Assert
        Assert.Equal(StatusExecucao.Concluida, automacao.Status);
        Assert.NotNull(automacao.DataFim);
    }

    [Fact]
    public void Falhar_ComStatusEmAndamento_DeveAlterarParaFalhada()
    {
        // Arrange
        var automacao = AutomacaoWhatsApp.Criar(Guid.NewGuid(), Guid.NewGuid(), "5511987654321", "Teste");
        automacao.Iniciar();
        var mensagemErro = "Erro teste";

        // Act
        automacao.Falhar(mensagemErro);

        // Assert
        Assert.Equal(StatusExecucao.Falhada, automacao.Status);
        Assert.Equal(mensagemErro, automacao.Erro);
        Assert.NotNull(automacao.DataFim);
    }

    [Fact]
    public void AdicionarLog_DeveAdicionarAoLista()
    {
        // Arrange
        var automacao = AutomacaoWhatsApp.Criar(Guid.NewGuid(), Guid.NewGuid(), "5511987654321", "Teste");
        var mensagemLog = "Log teste";

        // Act
        automacao.AdicionarLog(mensagemLog);

        // Assert
        Assert.Single(automacao.Logs);
        Assert.Equal(mensagemLog, automacao.Logs.First().Mensagem);
    }
}
