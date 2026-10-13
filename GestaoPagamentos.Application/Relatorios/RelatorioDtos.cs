namespace GestaoPagamentos.Application.Relatorios;

public sealed record ResumoDto(
    decimal TotalARreceber,
    decimal TotalRecebido,
    decimal TotalEmAtraso,
    int DividasAtivas,
    int ParcelasPendentes,
    int ParcelasPagasParcialmente,
    int ParcelasAtrasadas);

public sealed record ParcelaAtrasadaDto(
    Guid DividaId,
    Guid ParcelaId,
    Guid DevedorId,
    string DevedorNome,
    string? Telefone,
    string Descricao,
    int NumeroParcela,
    decimal ValorRestante,
    DateOnly DataVencimento,
    int DiasAtraso);

public sealed record HistoricoPagamentoDto(
    Guid PagamentoId,
    Guid DividaId,
    string DevedorNome,
    string Descricao,
    int NumeroParcela,
    decimal Valor,
    DateOnly DataPagamento,
    string? Observacao);
