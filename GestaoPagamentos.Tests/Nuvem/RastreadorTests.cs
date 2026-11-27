using System.Text.Json.Nodes;
using GestaoPagamentos.Domain.Configuracoes;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;
using GestaoPagamentos.Infrastructure.Nuvem;

namespace GestaoPagamentos.Tests.Nuvem;

public sealed class RastreadorTests
{
    private static readonly DateOnly Hoje = new(2026, 11, 25);

    private static int Quantas(JsonObject? mudancas, string tabela, string bloco) =>
        mudancas?[tabela]?[bloco]?.AsArray().Count ?? 0;

    [Fact]
    public void SemNadaRastreado_NaoHaMudancas()
    {
        Assert.Null(new RastreadorSupabase().MontarMudancas());
    }

    [Fact]
    public void EntidadeLidaESemAlteracao_NaoGeraMudancas()
    {
        var r = new RastreadorSupabase();
        r.Leu(Devedor.Criar("Maria", null, null, null));
        r.Leu(Divida.Criar(Guid.NewGuid(), "x", 100m, Hoje, TipoDivida.Avulsa, 1, null));
        r.Leu(Notificacao.Criar(Guid.NewGuid(), null, TipoNotificacao.Lembrete, "m", DateTime.Now));
        r.Leu(Configuracao.CriarPadrao());

        Assert.Null(r.MontarMudancas());
    }

    [Fact]
    public void DevedorAlterado_GravaSoEleComTodosOsCampos()
    {
        var r = new RastreadorSupabase();
        var devedor = Devedor.Criar("Maria", "11987654321", null, null);
        r.Leu(devedor);

        devedor.Atualizar("Maria Silva", "11987654321", "m@x.com", null);
        var m = r.MontarMudancas();

        Assert.Equal(1, Quantas(m, "devedores", "upsert"));
        Assert.Null(m!["dividas"]);
        var linha = m["devedores"]!["upsert"]![0]!;
        Assert.Equal("Maria Silva", (string?)linha["nome"]);
        Assert.Equal("m@x.com", (string?)linha["email"]);
        Assert.Equal(devedor.Id, (Guid)linha["id"]!);
    }

    [Fact]
    public void LimparUmCampo_GravaNuloEmVezDeIgnorar()
    {
        var r = new RastreadorSupabase();
        var devedor = Devedor.Criar("Maria", "11987654321", null, null);
        r.Leu(devedor);

        devedor.Atualizar("Maria", null, null, null);
        var linha = r.MontarMudancas()!["devedores"]!["upsert"]![0]!.AsObject();

        Assert.True(linha.ContainsKey("telefone"));
        Assert.Null(linha["telefone"]);
    }

    [Fact]
    public void DevedorNovo_EhGravadoComoUpsert()
    {
        var r = new RastreadorSupabase();
        r.Adicionou(Devedor.Criar("Novo", null, null, null));

        Assert.Equal(1, Quantas(r.MontarMudancas(), "devedores", "upsert"));
    }

    [Fact]
    public void DevedorRemovido_VaiParaAListaDeExclusao()
    {
        var r = new RastreadorSupabase();
        var devedor = Devedor.Criar("Maria", null, null, null);
        r.Leu(devedor);

        r.Removeu(devedor);
        var m = r.MontarMudancas();

        Assert.Equal(0, Quantas(m, "devedores", "upsert"));
        Assert.Equal(devedor.Id, (Guid)m!["devedores"]!["delete"]![0]!);
    }

    [Fact]
    public void DividaNova_GravaDividaEParcelasEmUmSoPacote()
    {
        var r = new RastreadorSupabase();
        r.Adicionou(Divida.Criar(Guid.NewGuid(), "x", 900m, Hoje, TipoDivida.Parcelada, 3, null));

        var m = r.MontarMudancas();

        Assert.Equal(1, Quantas(m, "dividas", "upsert"));
        Assert.Equal(3, Quantas(m, "parcelas", "upsert"));
        Assert.Equal(0, Quantas(m, "pagamentos", "upsert"));
    }

    [Fact]
    public void PagamentoNovoEmDividaLida_GravaSoOPagamentoEAMudancaDeStatus()
    {
        var r = new RastreadorSupabase();
        var divida = Divida.Criar(Guid.NewGuid(), "x", 1000m, Hoje.AddDays(5), TipoDivida.Avulsa, 1, null);
        r.Leu(divida);

        divida.RegistrarPagamento(divida.Parcelas[0].Id, 1300m, Hoje, null);
        var m = r.MontarMudancas();

        Assert.Equal(1, Quantas(m, "pagamentos", "upsert"));
        Assert.Equal(1, Quantas(m, "dividas", "upsert"));
        Assert.Equal((int)StatusDivida.Quitada, (int)m!["dividas"]!["upsert"]![0]!["status"]!);
        Assert.Equal(0, Quantas(m, "parcelas", "upsert"));
    }

    [Fact]
    public void PagamentoExcluido_VaiParaAListaDeExclusaoEReabreADivida()
    {
        var r = new RastreadorSupabase();
        var divida = Divida.Criar(Guid.NewGuid(), "x", 1000m, Hoje.AddDays(5), TipoDivida.Avulsa, 1, null);
        var pagamento = divida.RegistrarPagamento(divida.Parcelas[0].Id, 1300m, Hoje, null);
        r.Leu(divida);

        divida.ExcluirPagamento(pagamento.Id);
        var m = r.MontarMudancas();

        Assert.Equal(pagamento.Id, (Guid)m!["pagamentos"]!["delete"]![0]!);
        Assert.Equal((int)StatusDivida.Ativa, (int)m["dividas"]!["upsert"]![0]!["status"]!);
    }

    [Fact]
    public void RenovarComJuros_GravaNovaParcelaEPagamentoDeJuros()
    {
        var r = new RastreadorSupabase();
        var divida = Divida.Criar(Guid.NewGuid(), "x", 1000m, Hoje.AddDays(5), TipoDivida.Avulsa, 1, null);
        r.Leu(divida);

        divida.RenovarComJuros(divida.Parcelas[0].Id, Hoje);
        var m = r.MontarMudancas();

        Assert.Equal(1, Quantas(m, "parcelas", "upsert"));
        Assert.Equal(1, Quantas(m, "pagamentos", "upsert"));
    }

    [Fact]
    public void AlterarTaxaDeJuros_AtualizaDividaEParcelasSemPagamento()
    {
        var r = new RastreadorSupabase();
        var divida = Divida.Criar(Guid.NewGuid(), "x", 1000m, Hoje.AddDays(5), TipoDivida.Parcelada, 2, null);
        r.Leu(divida);

        divida.AlterarTaxaJuros(0.5m);
        var m = r.MontarMudancas();

        Assert.Equal(1, Quantas(m, "dividas", "upsert"));
        Assert.Equal(2, Quantas(m, "parcelas", "upsert"));
    }

    [Fact]
    public void NotificacaoDescartada_GravaSoEla()
    {
        var r = new RastreadorSupabase();
        var n = Notificacao.Criar(Guid.NewGuid(), Guid.NewGuid(), TipoNotificacao.Lembrete, "m", DateTime.Now);
        r.Leu(n);

        n.Descartar();
        var m = r.MontarMudancas();

        Assert.Equal(1, Quantas(m, "notificacoes", "upsert"));
        Assert.Equal((int)StatusNotificacao.Descartada, (int)m!["notificacoes"]!["upsert"]![0]!["status"]!);
    }

    [Fact]
    public void Aceitar_FazOEstadoAtualVirarOPontoDeComparacao()
    {
        var r = new RastreadorSupabase();
        var devedor = Devedor.Criar("Maria", null, null, null);
        r.Adicionou(devedor);
        r.Adicionou(Divida.Criar(devedor.Id, "x", 100m, Hoje, TipoDivida.Avulsa, 1, null));
        Assert.NotNull(r.MontarMudancas());

        r.Aceitar();

        Assert.Null(r.MontarMudancas());
        devedor.Atualizar("Maria 2", null, null, null);
        Assert.Equal(1, Quantas(r.MontarMudancas(), "devedores", "upsert"));
    }

    [Fact]
    public void Aceitar_DepoisDeExcluir_NaoExcluiDeNovo()
    {
        var r = new RastreadorSupabase();
        var devedor = Devedor.Criar("Maria", null, null, null);
        r.Leu(devedor);
        r.Removeu(devedor);
        Assert.NotNull(r.MontarMudancas());

        r.Aceitar();

        Assert.Null(r.MontarMudancas());
    }

    [Fact]
    public void EntidadeJaRastreada_VoltaComoOMesmoObjeto()
    {
        var r = new RastreadorSupabase();
        var devedor = Devedor.Criar("Maria", null, null, null);
        r.Leu(devedor);

        Assert.Same(devedor, r.Devedor(devedor.Id));
        Assert.Null(r.Devedor(Guid.NewGuid()));
    }

    [Fact]
    public void PacoteDeMudancas_ChegaNoFormatoQueAFuncaoSqlEspera()
    {
        var r = new RastreadorSupabase();
        var devedor = Devedor.Criar("Maria", "11987654321", null, null);
        r.Adicionou(devedor);

        var m = r.MontarMudancas()!;

        Assert.True(m["devedores"] is JsonObject);
        Assert.True(m["devedores"]!["upsert"] is JsonArray);
        Assert.Null(m["devedores"]!["delete"]);
        Assert.Equal(["devedores"], m.Select(x => x.Key));
    }
}
