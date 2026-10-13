using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Application.Notificacoes;

public sealed record NotificacaoDto(
    Guid Id,
    Guid DevedorId,
    string DevedorNome,
    string? Telefone,
    bool TelefoneValido,
    TipoNotificacao Tipo,
    StatusNotificacao Status,
    string Mensagem,
    string? Descricao,
    int? NumeroParcela,
    decimal? Valor,
    DateOnly? Vencimento,
    DateTime DataGeracao,
    DateTime? DataEnvio);

public sealed record LoteProgresso(int Atual, int Total, string DevedorNome);

public sealed record LoteResultado(int Enviadas, int Puladas, bool Cancelado);
