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
    public void Criar_Parcelada_GeraParcelasQueSomamOTotal()
    {
        var divida = Parcelada(100m, 3);

        Assert.Equal(3, divida.Parcelas.Count);
        Assert.Equal(100m, divida.Parcelas.Sum(p => p.ValorPrevisto));
        Assert.NotEqual(Guid.Empty, divida.Id);
    }

    [Fact]
    public void Criar_Avulsa_GeraUmaParcela()
    {
        var divida = Divida.Criar(Guid.NewGuid(), "Conta", 50m, Inicio, TipoDivida.Avulsa, 1, null);

        Assert.Single(divida.Parcelas);
        Assert.Equal(50m, divida.Parcelas[0].ValorPrevisto);
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
            divida.RegistrarPagamento(p.Id, p.ValorRestante, Inicio);

        Assert.Equal(StatusDivida.Quitada, divida.Status);
        Assert.Equal(0m, divida.ValorRestante);
    }

    [Fact]
    public void ExcluirPagamento_ReabreADivida()
    {
        var divida = Parcelada(90m, 3);
        foreach (var p in divida.Parcelas.ToList())
            divida.RegistrarPagamento(p.Id, p.ValorRestante, Inicio);
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
