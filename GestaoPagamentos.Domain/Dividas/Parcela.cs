using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Domain.Dividas;

public class Parcela : Entity<Guid>
{
    public const decimal TaxaMultaDiaria = 0.15m;
    private const decimal LimiteSaldo = 1_000_000_000_000m;

    private readonly List<Pagamento> _pagamentos = [];

    public Guid DividaId { get; private set; }
    public int Numero { get; private set; }
    public decimal ValorPrevisto { get; private set; }
    public DateOnly DataVencimento { get; private set; }
    public IReadOnlyList<Pagamento> Pagamentos => _pagamentos;

    public decimal ValorPago => _pagamentos.Sum(p => p.Valor);

    public bool EstaQuitada
    {
        get
        {
            if (_pagamentos.Count == 0) return false;
            var saldo = Simular(_pagamentos.Max(p => p.DataPagamento));
            return saldo.Principal == 0m && saldo.Multa == 0m;
        }
    }

    private Parcela() { }

    internal static Parcela Criar(Guid dividaId, int numero, decimal valorPrevisto, DateOnly vencimento) => new()
    {
        Id = Guid.CreateVersion7(),
        DividaId = dividaId,
        Numero = numero,
        ValorPrevisto = valorPrevisto,
        DataVencimento = vencimento
    };

    public decimal MultaEm(DateOnly data) => Simular(data).Multa;

    public decimal ValorRestanteEm(DateOnly data)
    {
        var saldo = Simular(data);
        return saldo.Principal + saldo.Multa;
    }

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
        if (valor > ValorRestanteEm(data))
            throw new DomainException("O pagamento excede o valor restante da parcela (incluindo a multa).");

        var pagamento = Pagamento.Criar(Id, valor, data, observacao);
        _pagamentos.Add(pagamento);
        return pagamento;
    }

    internal bool RemoverPagamento(Guid pagamentoId)
    {
        var pagamento = _pagamentos.FirstOrDefault(p => p.Id == pagamentoId);
        return pagamento is not null && _pagamentos.Remove(pagamento);
    }

    private (decimal Principal, decimal Multa) Simular(DateOnly ate)
    {
        var pagamentos = _pagamentos
            .Where(p => p.DataPagamento <= ate)
            .OrderBy(p => p.DataPagamento)
            .ThenBy(p => p.Id)
            .ToList();

        var principal = ValorPrevisto;
        var multa = 0m;
        var indice = 0;

        void AplicarPagamentosAte(DateOnly dia)
        {
            while (indice < pagamentos.Count && pagamentos[indice].DataPagamento <= dia)
            {
                var valor = pagamentos[indice++].Valor;
                var abatidoMulta = Math.Min(valor, multa);
                multa -= abatidoMulta;
                principal = Math.Max(0m, principal - (valor - abatidoMulta));
            }
        }

        AplicarPagamentosAte(DataVencimento);

        var multaDiaria = decimal.Round(ValorPrevisto * TaxaMultaDiaria, 2, MidpointRounding.AwayFromZero);

        for (var dia = DataVencimento.AddDays(1); dia <= ate && principal + multa > 0m; dia = dia.AddDays(1))
        {
            var saldo = principal + multa;
            if (saldo < LimiteSaldo)
                multa += multaDiaria;

            AplicarPagamentosAte(dia);
        }

        return (principal, multa);
    }
}
