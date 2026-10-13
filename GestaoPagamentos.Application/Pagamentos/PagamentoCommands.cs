namespace GestaoPagamentos.Application.Pagamentos;

public sealed record RegistrarPagamentoCommand(Guid ParcelaId, decimal Valor, DateOnly Data, string? Observacao);

public sealed record PagamentoRegistradoDto(
    Guid DividaId,
    string DevedorNome,
    int NumeroParcela,
    decimal Valor,
    bool ParcelaQuitada,
    bool DividaQuitada);
