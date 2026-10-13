using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Domain.Notificacoes;

public class Notificacao : Entity<Guid>
{
    public Guid DevedorId { get; private set; }
    public Guid? ParcelaId { get; private set; }
    public TipoNotificacao Tipo { get; private set; }
    public string Mensagem { get; private set; } = string.Empty;
    public DateTime DataGeracao { get; private set; }
    public DateTime? DataEnvio { get; private set; }
    public StatusNotificacao Status { get; private set; }

    private Notificacao() { }

    public static Notificacao Criar(Guid devedorId, Guid? parcelaId, TipoNotificacao tipo, string mensagem, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
            throw new DomainException("A mensagem da notificação é obrigatória.");

        return new Notificacao
        {
            Id = Guid.CreateVersion7(),
            DevedorId = devedorId,
            ParcelaId = parcelaId,
            Tipo = tipo,
            Mensagem = mensagem.Trim(),
            DataGeracao = agora,
            Status = StatusNotificacao.Pendente
        };
    }

    public void AtualizarMensagem(string mensagem)
    {
        if (Status != StatusNotificacao.Pendente)
            throw new DomainException("Apenas notificações pendentes podem ser editadas.");
        if (string.IsNullOrWhiteSpace(mensagem))
            throw new DomainException("A mensagem da notificação é obrigatória.");

        Mensagem = mensagem.Trim();
    }

    public void MarcarComoEnviada(DateTime agora)
    {
        if (Status != StatusNotificacao.Pendente) return;
        Status = StatusNotificacao.Enviada;
        DataEnvio = agora;
    }

    public void Ignorar()
    {
        if (Status == StatusNotificacao.Pendente) Status = StatusNotificacao.Ignorada;
    }

    public void Descartar()
    {
        if (Status == StatusNotificacao.Pendente) Status = StatusNotificacao.Descartada;
    }
}
