using GestaoPagamentos.Domain.Dividas;

namespace GestaoPagamentos.Application.Dividas;

public sealed record PagamentoDto(Guid Id, decimal Valor, DateOnly DataPagamento, string? Observacao);

public sealed record ParcelaDto(
    Guid Id,
    int Numero,
    decimal ValorPrevisto,
    decimal ValorPago,
    decimal ValorRestante,
    DateOnly DataVencimento,
    StatusParcela Status,
    IReadOnlyList<PagamentoDto> Pagamentos);

public sealed record DividaResumoDto(
    Guid Id,
    Guid DevedorId,
    string DevedorNome,
    string Descricao,
    TipoDivida Tipo,
    StatusDivida Status,
    decimal ValorEmprestado,
    decimal ValorTotal,
    decimal ValorPago,
    decimal ValorRestante,
    int QuantidadeParcelas,
    int ParcelasAtrasadas,
    DateOnly? ProximoVencimento,
    DateOnly DataEmprestimo,
    DateOnly DataVencimento);

public sealed record DividaDetalheDto(
    Guid Id,
    Guid DevedorId,
    string DevedorNome,
    string? DevedorTelefone,
    string Descricao,
    string? Observacao,
    TipoDivida Tipo,
    StatusDivida Status,
    decimal ValorEmprestado,
    decimal TaxaJuros,
    decimal ValorTotal,
    decimal ValorPago,
    decimal ValorRestante,
    IReadOnlyList<ParcelaDto> Parcelas);

public sealed record CriarDividaCommand(
    Guid DevedorId,
    string Descricao,
    decimal ValorEmprestado,
    DateOnly PrimeiroVencimento,
    TipoDivida Tipo,
    int QuantidadeParcelas,
    string? Observacao);

public sealed record AtualizarDividaCommand(Guid DividaId, string Descricao, string? Observacao);
