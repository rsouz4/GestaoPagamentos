using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Domain.Notificacoes;

public class AutomacaoWhatsApp : Entity<Guid>
{
    private readonly List<LogExecucao> _logs = [];

    public Guid NotificacaoId { get; private set; }
    public Guid DevedorId { get; private set; }
    public string Telefone { get; private set; } = string.Empty;
    public string Mensagem { get; private set; } = string.Empty;
    public StatusExecucao Status { get; private set; }
    public DateTime DataInicio { get; private set; }
    public DateTime? DataFim { get; private set; }
    public string? Erro { get; private set; }
    public IReadOnlyList<LogExecucao> Logs => _logs;

    private AutomacaoWhatsApp() { }

    public static AutomacaoWhatsApp Criar(
        Guid notificacaoId,
        Guid devedorId,
        string telefone,
        string mensagem)
    {
        if (notificacaoId == Guid.Empty)
            throw new DomainException("A notificação é obrigatória.");

        if (devedorId == Guid.Empty)
            throw new DomainException("O devedor é obrigatório.");

        if (string.IsNullOrWhiteSpace(telefone))
            throw new DomainException("O telefone é obrigatório.");

        if (string.IsNullOrWhiteSpace(mensagem))
            throw new DomainException("A mensagem é obrigatória.");

        return new AutomacaoWhatsApp
        {
            Id = Guid.NewGuid(),
            NotificacaoId = notificacaoId,
            DevedorId = devedorId,
            Telefone = telefone,
            Mensagem = mensagem,
            Status = StatusExecucao.Pendente,
            DataInicio = DateTime.UtcNow
        };
    }

    public void Iniciar()
    {
        if (Status != StatusExecucao.Pendente)
            throw new DomainException("Apenas automações pendentes podem ser iniciadas.");

        Status = StatusExecucao.EmAndamento;
        AdicionarLog("Iniciada automação de envio via WhatsApp Web");
    }

    public void Concluir()
    {
        if (Status != StatusExecucao.EmAndamento)
            throw new DomainException("Apenas automações em andamento podem ser concluídas.");

        Status = StatusExecucao.Concluida;
        DataFim = DateTime.UtcNow;
        AdicionarLog("Mensagem enviada com sucesso");
    }

    public void Falhar(string mensagemErro)
    {
        if (Status == StatusExecucao.Concluida)
            throw new DomainException("Uma automação concluída não pode falhar.");

        Status = StatusExecucao.Falhada;
        DataFim = DateTime.UtcNow;
        Erro = mensagemErro;
        AdicionarLog($"Erro: {mensagemErro}");
    }

    public void AdicionarLog(string mensagem)
    {
        _logs.Add(new LogExecucao(
            Id: Guid.NewGuid(),
            AutomacaoWhatsAppId: Id,
            Mensagem: mensagem,
            DataCriacao: DateTime.UtcNow));
    }
}

public enum StatusExecucao
{
    Pendente = 0,
    EmAndamento = 1,
    Concluida = 2,
    Falhada = 3
}

public sealed record LogExecucao(
    Guid Id,
    Guid AutomacaoWhatsAppId,
    string Mensagem,
    DateTime DataCriacao);
