using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Dividas;

namespace GestaoPagamentos.Tests;

public class DividaTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 31);

    private static Divida Parcelada(decimal total = 100m, int parcelas = 3) =>
        Divida.Criar(Guid.NewGuid(), "Emprestimo", total, Inicio, TipoDivida.Parcelada, parcelas, null);

    [Fact]
    public void Criar_Aplica30PorCentoDeJurosNoValorAReceber()
    {
        var divida = Divida.Criar(Guid.NewGuid(), "Emprestimo", 1000m, Inicio, TipoDivida.Avulsa, 1, null);

        Assert.Equal(1000m, divida.ValorEmprestado);
        Assert.Equal(0.30m, divida.TaxaJuros);
        Assert.Equal(1300m, divida.ValorTotal);
        Assert.Equal(1300m, divida.Parcelas[0].ValorPrevisto);
        Assert.Equal(1300m, divida.ValorRestanteEm(Inicio));
    }

    [Fact]
    public void Multa_AcumulaDiariamenteSobreOValorOriginalDaParcela()
    {
        var divida = Divida.Criar(Guid.NewGuid(), "x", 1000m, Inicio, TipoDivida.Avulsa, 1, null);
        var parcela = divida.Parcelas[0];
        var venc = parcela.DataVencimento;

        Assert.Equal(0m, parcela.MultaEm(venc));
        Assert.Equal(195m, parcela.MultaEm(venc.AddDays(1)));
        Assert.Equal(390m, parcela.MultaEm(venc.AddDays(2)));
        Assert.Equal(1690m, parcela.ValorRestanteEm(venc.AddDays(2)));
        Assert.Equal(1885m, parcela.ValorRestanteEm(venc.AddDays(3)));
        Assert.Equal(7150m, parcela.ValorRestanteEm(venc.AddDays(30)));
    }

    [Fact]
    public void Multa_PagamentoParcialAntesDoVencimento_NaoReduzABaseDiaria()
    {
        var divida = Divida.Criar(Guid.NewGuid(), "x", 1000m, Inicio, TipoDivida.Avulsa, 1, null);
        var parcela = divida.Parcelas[0];

        divida.RegistrarPagamento(parcela.Id, 500m, Inicio.AddDays(-1));

        Assert.Equal(0m, parcela.MultaEm(Inicio));
        Assert.Equal(800m, parcela.ValorRestanteEm(Inicio));
        Assert.Equal(195m, parcela.MultaEm(Inicio.AddDays(1)));
        Assert.Equal(995m, parcela.ValorRestanteEm(Inicio.AddDays(1)));
    }

    [Fact]
    public void Multa_PagamentoParcialEmAtraso_NaoReduzABaseDiaria()
    {
        var divida = Divida.Criar(Guid.NewGuid(), "x", 1000m, Inicio, TipoDivida.Avulsa, 1, null);
        var parcela = divida.Parcelas[0];

        divida.RegistrarPagamento(parcela.Id, 695m, Inicio.AddDays(1));

        Assert.Equal(0m, parcela.MultaEm(Inicio.AddDays(1)));
        Assert.Equal(800m, parcela.ValorRestanteEm(Inicio.AddDays(1)));
        Assert.Equal(195m, parcela.MultaEm(Inicio.AddDays(2)));
        Assert.Equal(995m, parcela.ValorRestanteEm(Inicio.AddDays(2)));
        Assert.Equal(390m, parcela.MultaEm(Inicio.AddDays(3)));
        Assert.Equal(1190m, parcela.ValorRestanteEm(Inicio.AddDays(3)));
    }

    [Fact]
    public void Multa_QuitacaoEmAtraso_InterrompeOsAcrescimosDiarios()
    {
        var divida = Divida.Criar(Guid.NewGuid(), "x", 1000m, Inicio, TipoDivida.Avulsa, 1, null);
        var parcela = divida.Parcelas[0];

        divida.RegistrarPagamento(parcela.Id, 1690m, Inicio.AddDays(2));

        Assert.True(parcela.EstaQuitada);
        Assert.Equal(StatusDivida.Quitada, divida.Status);
        Assert.Equal(0m, parcela.MultaEm(Inicio.AddDays(30)));
        Assert.Equal(0m, parcela.ValorRestanteEm(Inicio.AddDays(30)));
    }

    [Fact]
    public void Multa_ArredondaOAcrescimoDiarioParaCentavos()
    {
        var divida = Divida.Criar(Guid.NewGuid(), "x", 1m, Inicio, TipoDivida.Avulsa, 1, null);
        var parcela = divida.Parcelas[0];

        Assert.Equal(0.20m, parcela.MultaEm(Inicio.AddDays(1)));
        Assert.Equal(0.60m, parcela.MultaEm(Inicio.AddDays(3)));
        Assert.Equal(1.90m, parcela.ValorRestanteEm(Inicio.AddDays(3)));
    }

    [Fact]
    public void Multa_ExcluirPagamento_RecalculaOsAcrescimosSimples()
    {
        var divida = Divida.Criar(Guid.NewGuid(), "x", 1000m, Inicio, TipoDivida.Avulsa, 1, null);
        var parcela = divida.Parcelas[0];
        var pagamento = divida.RegistrarPagamento(parcela.Id, 1495m, Inicio.AddDays(1));

        divida.ExcluirPagamento(pagamento.Id);

        Assert.Equal(StatusDivida.Ativa, divida.Status);
        Assert.Equal(585m, parcela.MultaEm(Inicio.AddDays(3)));
        Assert.Equal(1885m, parcela.ValorRestanteEm(Inicio.AddDays(3)));
    }

    [Fact]
    public void Pagamento_AbatePrimeiroAMultaDepoisOPrincipal()
    {
        var divida = Divida.Criar(Guid.NewGuid(), "x", 1000m, Inicio, TipoDivida.Avulsa, 1, null);
        var parcela = divida.Parcelas[0];
        var dia = parcela.DataVencimento.AddDays(1);

        divida.RegistrarPagamento(parcela.Id, 195m, dia);

        Assert.Equal(0m, parcela.MultaEm(dia));
        Assert.Equal(1300m, parcela.ValorRestanteEm(dia));
    }

    [Fact]
    public void Criar_Parcelada_GeraParcelasQueSomamOTotal()
    {
        var divida = Parcelada(100m, 3);

        Assert.Equal(3, divida.Parcelas.Count);
        Assert.Equal(130m, divida.Parcelas.Sum(p => p.ValorPrevisto));
        Assert.NotEqual(Guid.Empty, divida.Id);
    }

    [Fact]
    public void Criar_Avulsa_GeraUmaParcela()
    {
        var divida = Divida.Criar(Guid.NewGuid(), "Conta", 50m, Inicio, TipoDivida.Avulsa, 1, null);

        Assert.Single(divida.Parcelas);
        Assert.Equal(65m, divida.Parcelas[0].ValorPrevisto);
    }

    [Fact]
    public void Criar_ComValorInvalido_Lanca()
    {
        Assert.Throws<DomainException>(() =>
            Divida.Criar(Guid.NewGuid(), "x", 0m, Inicio, TipoDivida.Avulsa, 1, null));
    }

    [Fact]
    public void Pagamento_Parcial_MantemParcelaPagaParcial()
    {
        var divida = Parcelada(90m, 3);
        var parcela = divida.Parcelas[0];

        divida.RegistrarPagamento(parcela.Id, 10m, Inicio);

        Assert.Equal(StatusParcela.PagaParcial, parcela.StatusEm(Inicio));
        Assert.Equal(10m, divida.ValorPago);
    }

    [Fact]
    public void Pagamento_AcimaDoRestante_Lanca()
    {
        var divida = Parcelada(90m, 3);

        Assert.Throws<DomainException>(() =>
            divida.RegistrarPagamento(divida.Parcelas[0].Id, 1000m, Inicio));
    }

    [Fact]
    public void PagarTodasAsParcelas_QuitaADivida()
    {
        var divida = Parcelada(90m, 3);

        foreach (var p in divida.Parcelas.ToList())
            divida.RegistrarPagamento(p.Id, p.ValorRestanteEm(Inicio), Inicio);

        Assert.Equal(StatusDivida.Quitada, divida.Status);
        Assert.Equal(0m, divida.ValorRestanteEm(Inicio));
    }

    [Fact]
    public void ExcluirPagamento_ReabreADivida()
    {
        var divida = Parcelada(90m, 3);
        foreach (var p in divida.Parcelas.ToList())
            divida.RegistrarPagamento(p.Id, p.ValorRestanteEm(Inicio), Inicio);
        var pagamento = divida.Parcelas[0].Pagamentos[0];
        divida.ExcluirPagamento(pagamento.Id);

        Assert.Equal(StatusDivida.Ativa, divida.Status);
    }

    [Fact]
    public void Parcela_NaoPagaAposVencimento_FicaAtrasada()
    {
        var divida = Parcelada(90m, 3);
        var parcela = divida.Parcelas[0];

        Assert.Equal(StatusParcela.Pendente, parcela.StatusEm(parcela.DataVencimento));
        Assert.Equal(StatusParcela.Atrasada, parcela.StatusEm(parcela.DataVencimento.AddDays(1)));
    }

    [Fact]
    public void Cancelar_AlteraStatus()
    {
        var divida = Parcelada();

        divida.Cancelar();

        Assert.Equal(StatusDivida.Cancelada, divida.Status);
    }
}

public class WhatsAppLinkBuilderTests
{
    [Theory]
    [InlineData("(11) 91234-5678", "5511912345678")]
    [InlineData("11912345678", "5511912345678")]
    [InlineData("+55 11 91234-5678", "5511912345678")]
    public void NormalizarTelefone_AdicionaCodigoDoPais(string entrada, string esperado) =>
        Assert.Equal(esperado, WhatsAppLinkBuilder.NormalizarTelefone(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    public void NormalizarTelefone_Invalido_RetornaNulo(string? entrada) =>
        Assert.Null(WhatsAppLinkBuilder.NormalizarTelefone(entrada));

    [Fact]
    public void MontarLink_CodificaMensagem()
    {
        var link = WhatsAppLinkBuilder.MontarLink("11912345678", "Olá mundo");

        Assert.NotNull(link);
        Assert.StartsWith("https://wa.me/5511912345678?text=", link);
        Assert.DoesNotContain(" ", link);
    }

    [Fact]
    public void MontarLink_SemTelefone_RetornaNulo() =>
        Assert.Null(WhatsAppLinkBuilder.MontarLink(null, "oi"));

    [Fact]
    public void RenderizarTemplate_SubstituiPlaceholders()
    {
        var texto = WhatsAppLinkBuilder.RenderizarTemplate(
            "Oi {Nome}, {Descricao} {Valor} {Vencimento}", "Ana", 10m, new DateOnly(2026, 3, 5), "Conta");

        Assert.Contains("Ana", texto);
        Assert.Contains("Conta", texto);
        Assert.DoesNotContain("{", texto);
    }
}
