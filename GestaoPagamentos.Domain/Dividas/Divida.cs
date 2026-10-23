using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Domain.Dividas;

public class Divida : Entity<Guid>
{
    public const decimal TaxaJurosPadrao = 0.30m;

    private readonly List<Parcela> _parcelas = [];

    public Guid DevedorId { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public decimal ValorEmprestado { get; private set; }
    public decimal TaxaJuros { get; private set; }
    public decimal ValorTotal { get; private set; }
    public DateOnly DataPrimeiroVencimento { get; private set; }
    public DateOnly DataEmprestimo { get; private set; }
    public TipoDivida Tipo { get; private set; }
    public int QuantidadeParcelas { get; private set; }
    public StatusDivida Status { get; private set; }
    public string? Observacao { get; private set; }
    public IReadOnlyList<Parcela> Parcelas => _parcelas;

    public decimal ValorPago => _parcelas.Sum(p => p.ValorPago);
    public decimal ValorRestanteEm(DateOnly data) => _parcelas.Sum(p => p.ValorRestanteEm(data));

    private Divida() { }

    public static Divida Criar(
        Guid devedorId,
        string descricao,
        decimal valorEmprestado,
        DateOnly primeiroVencimento,
        TipoDivida tipo,
        int quantidadeParcelas,
        string? observacao,
        decimal taxaJuros = TaxaJurosPadrao)
    {
        if (devedorId == Guid.Empty)
            throw new DomainException("O devedor é obrigatório.");

        if (string.IsNullOrWhiteSpace(descricao))
            throw new DomainException("A descrição da dívida é obrigatória.");

        if (valorEmprestado <= 0m)
            throw new DomainException("O valor emprestado deve ser maior que zero.");

        if (taxaJuros < 0m)
            throw new DomainException("A taxa de juros não pode ser negativa.");

        var quantidade = tipo == TipoDivida.Avulsa ? 1 : quantidadeParcelas;

        if (quantidade < 1)
            throw new DomainException("A quantidade de parcelas deve ser no mínimo 1.");

        var divida = new Divida
        {
            Id = Guid.CreateVersion7(),
            DevedorId = devedorId,
            Descricao = descricao.Trim(),
            ValorEmprestado = decimal.Round(valorEmprestado, 2),
            TaxaJuros = taxaJuros,
            ValorTotal = decimal.Round(valorEmprestado * (1m + taxaJuros), 2, MidpointRounding.AwayFromZero),
            DataPrimeiroVencimento = primeiroVencimento,
            DataEmprestimo = DateOnly.FromDateTime(DateTime.Today),
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

    public Pagamento RenovarComJuros(Guid parcelaId, DateOnly data)
    {
        if (Status == StatusDivida.Cancelada)
            throw new DomainException("Dívida cancelada não pode ser renovada.");

        var parcelaAtual = ObterParcela(parcelaId);

        // O ValorPrevisto já inclui juros. Calcular o principal
        var valorPrincipal = decimal.Round(parcelaAtual.ValorPrevisto / (1m + TaxaJuros), 2, MidpointRounding.AwayFromZero);

        // Os juros são a diferença
        var jurosCalculados = parcelaAtual.ValorPrevisto - valorPrincipal;

        // Registrar pagamento dos juros
        var pagamentoJuros = parcelaAtual.AdicionarPagamento(jurosCalculados, data, "Pagamento de juros - Renovação");

        // Criar nova parcela com o mesmo valor (principal + juros novamente)
        var proximoNumero = _parcelas.Max(p => p.Numero) + 1;
        var novaDataVencimento = parcelaAtual.DataVencimento.AddMonths(1);

        _parcelas.Add(Parcela.Criar(Id, proximoNumero, parcelaAtual.ValorPrevisto, novaDataVencimento));

        RecalcularStatus();
        return pagamentoJuros;
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
