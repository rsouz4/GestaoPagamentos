using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Domain.Dividas;

public class Divida : Entity<Guid>
{
    private readonly List<Parcela> _parcelas = [];

    public Guid DevedorId { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public decimal ValorTotal { get; private set; }
    public DateOnly DataPrimeiroVencimento { get; private set; }
    public TipoDivida Tipo { get; private set; }
    public int QuantidadeParcelas { get; private set; }
    public StatusDivida Status { get; private set; }
    public string? Observacao { get; private set; }
    public IReadOnlyList<Parcela> Parcelas => _parcelas;

    public decimal ValorPago => _parcelas.Sum(p => p.ValorPago);
    public decimal ValorRestante => _parcelas.Sum(p => p.ValorRestante);

    private Divida() { }

    public static Divida Criar(
        Guid devedorId,
        string descricao,
        decimal valorTotal,
        DateOnly primeiroVencimento,
        TipoDivida tipo,
        int quantidadeParcelas,
        string? observacao)
    {
        if (devedorId == Guid.Empty)
            throw new DomainException("O devedor é obrigatório.");
        if (string.IsNullOrWhiteSpace(descricao))
            throw new DomainException("A descrição da dívida é obrigatória.");
        if (valorTotal <= 0m)
            throw new DomainException("O valor total deve ser maior que zero.");

        var quantidade = tipo == TipoDivida.Avulsa ? 1 : quantidadeParcelas;
        if (quantidade < 1)
            throw new DomainException("A quantidade de parcelas deve ser no mínimo 1.");

        var divida = new Divida
        {
            Id = Guid.CreateVersion7(),
            DevedorId = devedorId,
            Descricao = descricao.Trim(),
            ValorTotal = decimal.Round(valorTotal, 2),
            DataPrimeiroVencimento = primeiroVencimento,
            Tipo = tipo,
            QuantidadeParcelas = quantidade,
            Status = StatusDivida.Ativa,
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim()
        };

        divida.GerarParcelas();
        return divida;
    }

    public void AtualizarDados(string descricao, string? observacao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new DomainException("A descrição da dívida é obrigatória.");

        Descricao = descricao.Trim();
        Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
    }

    public Pagamento RegistrarPagamento(Guid parcelaId, decimal valor, DateOnly data, string? observacao = null)
    {
        if (Status == StatusDivida.Cancelada)
            throw new DomainException("Dívida cancelada não aceita pagamentos.");

        var parcela = ObterParcela(parcelaId);
        var pagamento = parcela.AdicionarPagamento(valor, data, observacao);
        RecalcularStatus();
        return pagamento;
    }

    public void ExcluirPagamento(Guid pagamentoId)
    {
        if (Status == StatusDivida.Cancelada)
            throw new DomainException("Dívida cancelada não permite alterar pagamentos.");

        var removido = _parcelas.Any(p => p.RemoverPagamento(pagamentoId));
        if (!removido)
            throw new DomainException("Pagamento não encontrado.");

        RecalcularStatus();
    }

    public void Cancelar()
    {
        if (Status == StatusDivida.Cancelada) return;
        Status = StatusDivida.Cancelada;
    }

    public Parcela? ObterParcelaOuNulo(Guid parcelaId) => _parcelas.FirstOrDefault(p => p.Id == parcelaId);

    private Parcela ObterParcela(Guid parcelaId) =>
        ObterParcelaOuNulo(parcelaId) ?? throw new DomainException("Parcela não encontrada.");

    private void RecalcularStatus()
    {
        if (Status == StatusDivida.Cancelada) return;
        Status = _parcelas.All(p => p.EstaQuitada) ? StatusDivida.Quitada : StatusDivida.Ativa;
    }

    private void GerarParcelas()
    {
        var valorBase = decimal.Round(ValorTotal / QuantidadeParcelas, 2, MidpointRounding.ToZero);
        var acumulado = 0m;

        for (var i = 1; i <= QuantidadeParcelas; i++)
        {
            var valor = i == QuantidadeParcelas ? ValorTotal - acumulado : valorBase;
            acumulado += valor;
            _parcelas.Add(Parcela.Criar(Id, i, valor, DataPrimeiroVencimento.AddMonths(i - 1)));
        }
    }
}
