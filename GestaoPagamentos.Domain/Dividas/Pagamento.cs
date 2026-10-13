using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Domain.Dividas;

public class Pagamento : Entity<Guid>
{
    public Guid ParcelaId { get; private set; }
    public decimal Valor { get; private set; }
    public DateOnly DataPagamento { get; private set; }
    public string? Observacao { get; private set; }

    private Pagamento() { }

    internal static Pagamento Criar(Guid parcelaId, decimal valor, DateOnly data, string? observacao) => new()
    {
        Id = Guid.CreateVersion7(),
        ParcelaId = parcelaId,
        Valor = valor,
        DataPagamento = data,
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim()
    };
}
