using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Domain.Dividas;

public class Parcela : Entity<Guid>
{
    private readonly List<Pagamento> _pagamentos = [];

    public Guid DividaId { get; private set; }
    public int Numero { get; private set; }
    public decimal ValorPrevisto { get; private set; }
    public DateOnly DataVencimento { get; private set; }
    public IReadOnlyList<Pagamento> Pagamentos => _pagamentos;

    public decimal ValorPago => _pagamentos.Sum(p => p.Valor);
    public decimal ValorRestante => Math.Max(0m, ValorPrevisto - ValorPago);
    public bool EstaQuitada => ValorPago >= ValorPrevisto;

    private Parcela() { }

    internal static Parcela Criar(Guid dividaId, int numero, decimal valorPrevisto, DateOnly vencimento) => new()
    {
        Id = Guid.CreateVersion7(),
        DividaId = dividaId,
        Numero = numero,
        ValorPrevisto = valorPrevisto,
        DataVencimento = vencimento
    };

    public StatusParcela StatusEm(DateOnly hoje)
    {
        if (EstaQuitada) return StatusParcela.Paga;
        if (DataVencimento < hoje) return StatusParcela.Atrasada;
        return ValorPago > 0m ? StatusParcela.PagaParcial : StatusParcela.Pendente;
    }

    internal Pagamento AdicionarPagamento(decimal valor, DateOnly data, string? observacao)
    {
        if (valor <= 0m)
            throw new DomainException("O valor do pagamento deve ser maior que zero.");
        if (valor > ValorRestante)
            throw new DomainException("O pagamento excede o valor restante da parcela.");

        var pagamento = Pagamento.Criar(Id, valor, data, observacao);
        _pagamentos.Add(pagamento);
        return pagamento;
    }

    internal bool RemoverPagamento(Guid pagamentoId)
    {
        var pagamento = _pagamentos.FirstOrDefault(p => p.Id == pagamentoId);
        return pagamento is not null && _pagamentos.Remove(pagamento);
    }
}
