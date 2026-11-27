using System.Text.Json.Serialization;

namespace GestaoPagamentos.Infrastructure.Nuvem;

// Uma linha de cada tabela do Supabase, com os nomes de coluna em snake_case.
// Os campos user_id ficam de fora: o banco os preenche com o usuário logado.

internal sealed record DevedorLinha(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("telefone")] string? Telefone,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("observacao")] string? Observacao);

internal sealed record DividaLinha(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("devedor_id")] Guid DevedorId,
    [property: JsonPropertyName("descricao")] string Descricao,
    [property: JsonPropertyName("valor_emprestado")] decimal ValorEmprestado,
    [property: JsonPropertyName("taxa_juros")] decimal TaxaJuros,
    [property: JsonPropertyName("valor_total")] decimal ValorTotal,
    [property: JsonPropertyName("data_primeiro_vencimento")] DateOnly DataPrimeiroVencimento,
    [property: JsonPropertyName("data_emprestimo")] DateOnly DataEmprestimo,
    [property: JsonPropertyName("tipo")] int Tipo,
    [property: JsonPropertyName("quantidade_parcelas")] int QuantidadeParcelas,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("observacao")] string? Observacao)
{
    /// <summary>Só vem preenchido nas leituras com <c>select=*,parcelas(*,pagamentos(*))</c>; nunca é gravado.</summary>
    [JsonPropertyName("parcelas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ParcelaLinha>? Parcelas { get; init; }
}

internal sealed record ParcelaLinha(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("divida_id")] Guid DividaId,
    [property: JsonPropertyName("numero")] int Numero,
    [property: JsonPropertyName("valor_previsto")] decimal ValorPrevisto,
    [property: JsonPropertyName("data_vencimento")] DateOnly DataVencimento)
{
    [JsonPropertyName("pagamentos")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PagamentoLinha>? Pagamentos { get; init; }
}

internal sealed record PagamentoLinha(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("parcela_id")] Guid ParcelaId,
    [property: JsonPropertyName("valor")] decimal Valor,
    [property: JsonPropertyName("data_pagamento")] DateOnly DataPagamento,
    [property: JsonPropertyName("observacao")] string? Observacao);

internal sealed record NotificacaoLinha(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("devedor_id")] Guid DevedorId,
    [property: JsonPropertyName("parcela_id")] Guid? ParcelaId,
    [property: JsonPropertyName("tipo")] int Tipo,
    [property: JsonPropertyName("mensagem")] string Mensagem,
    [property: JsonPropertyName("data_geracao")] DateTime DataGeracao,
    [property: JsonPropertyName("data_envio")] DateTime? DataEnvio,
    [property: JsonPropertyName("status")] int Status);

internal sealed record ConfiguracaoLinha(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("dias_antecedencia_lembrete")] int DiasAntecedenciaLembrete,
    [property: JsonPropertyName("intervalo_envio_lote_ms")] int IntervaloEnvioLoteMs,
    [property: JsonPropertyName("template_lembrete")] string TemplateLembrete,
    [property: JsonPropertyName("template_atraso")] string TemplateAtraso);

internal sealed record ChaveNotificacaoLinha(
    [property: JsonPropertyName("parcela_id")] Guid ParcelaId,
    [property: JsonPropertyName("tipo")] int Tipo);

internal sealed record IdDividaLinha([property: JsonPropertyName("divida_id")] Guid DividaId);

internal sealed record IdParcelaLinha([property: JsonPropertyName("parcela_id")] Guid ParcelaId);

internal sealed record IdLinha([property: JsonPropertyName("id")] Guid Id);
