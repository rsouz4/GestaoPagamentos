using System.Text.Json;
using GestaoPagamentos.Domain.Configuracoes;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;
using GestaoPagamentos.Infrastructure.Nuvem;

namespace GestaoPagamentos.Tests.Nuvem;

public sealed class MapeamentoTests
{
    private static readonly DateOnly Hoje = new(2026, 11, 25);

    private static DividaLinha Completa(Divida d) => Mapeamento.ParaLinha(d) with
    {
        Parcelas = d.Parcelas.Select(p => Mapeamento.ParaLinha(p) with
        {
            Pagamentos = p.Pagamentos.Select(Mapeamento.ParaLinha).ToList()
        }).ToList()
    };

    [Fact]
    public void Devedor_IdaEVolta_PreservaTodosOsCampos()
    {
        var original = Devedor.Criar("Maria", "11987654321", "m@x.com", "obs");

        var volta = Mapeamento.ParaEntidade(Mapeamento.ParaLinha(original));

        Assert.Equal(original.Id, volta.Id);
        Assert.Equal("Maria", volta.Nome);
        Assert.Equal("11987654321", volta.Telefone);
        Assert.Equal("m@x.com", volta.Email);
        Assert.Equal("obs", volta.Observacao);
    }

    [Fact]
    public void Divida_ComParcelasEPagamentos_VoltaComOMesmoComportamentoDeDominio()
    {
        var original = Divida.Criar(Guid.NewGuid(), "Moto", 3000m, Hoje.AddDays(-10), TipoDivida.Parcelada, 3, "obs");
        var primeira = original.Parcelas[0];
        original.RegistrarPagamento(primeira.Id, 500m, Hoje.AddDays(-9), "pix");
        original.RegistrarPagamento(primeira.Id, 100m, Hoje, null);

        var volta = Mapeamento.ParaEntidade(Completa(original));

        Assert.Equal(original.Id, volta.Id);
        Assert.Equal(original.DevedorId, volta.DevedorId);
        Assert.Equal(original.ValorTotal, volta.ValorTotal);
        Assert.Equal(original.TaxaJuros, volta.TaxaJuros);
        Assert.Equal(original.Tipo, volta.Tipo);
        Assert.Equal(original.Status, volta.Status);
        Assert.Equal(original.DataEmprestimo, volta.DataEmprestimo);
        Assert.Equal(original.Parcelas.Select(p => p.Id), volta.Parcelas.Select(p => p.Id));
        Assert.Equal(original.ValorPago, volta.ValorPago);
        Assert.Equal(original.ValorRestanteEm(Hoje), volta.ValorRestanteEm(Hoje));
        Assert.Equal(
            original.Parcelas.Select(p => p.StatusEm(Hoje)),
            volta.Parcelas.Select(p => p.StatusEm(Hoje)));
        Assert.Equal(2, volta.Parcelas[0].Pagamentos.Count);
        Assert.Equal("pix", volta.Parcelas[0].Pagamentos.First(p => p.Valor == 500m).Observacao);
    }

    [Fact]
    public void Divida_Reconstituida_AindaAceitaOperacoesDeDominio()
    {
        var original = Divida.Criar(Guid.NewGuid(), "x", 1000m, Hoje.AddDays(5), TipoDivida.Avulsa, 1, null);
        var volta = Mapeamento.ParaEntidade(Completa(original));

        volta.RegistrarPagamento(volta.Parcelas[0].Id, 1300m, Hoje, null);

        Assert.Equal(StatusDivida.Quitada, volta.Status);
        Assert.Single(volta.Parcelas[0].Pagamentos);
    }

    [Fact]
    public void Divida_ParcelasEmOrdemDeNumeroMesmoQuandoChegamEmOutraOrdem()
    {
        var original = Divida.Criar(Guid.NewGuid(), "x", 900m, Hoje, TipoDivida.Parcelada, 3, null);
        var linha = Completa(original);
        linha = linha with { Parcelas = linha.Parcelas!.AsEnumerable().Reverse().ToList() };

        var volta = Mapeamento.ParaEntidade(linha);

        Assert.Equal([1, 2, 3], volta.Parcelas.Select(p => p.Numero));
    }

    [Fact]
    public void Notificacao_IdaEVolta_PreservaEnumsEDatas()
    {
        var original = Notificacao.Criar(Guid.NewGuid(), Guid.NewGuid(), TipoNotificacao.Atraso, "msg", new DateTime(2026, 11, 25, 10, 30, 15, 123));
        original.MarcarComoEnviada(new DateTime(2026, 11, 25, 11, 0, 0));

        var volta = Mapeamento.ParaEntidade(Mapeamento.ParaLinha(original));

        Assert.Equal(original.ParcelaId, volta.ParcelaId);
        Assert.Equal(TipoNotificacao.Atraso, volta.Tipo);
        Assert.Equal(StatusNotificacao.Enviada, volta.Status);
        Assert.Equal(original.DataGeracao, volta.DataGeracao);
        Assert.Equal(original.DataEnvio, volta.DataEnvio);
    }

    [Fact]
    public void Notificacao_SemParcela_VoltaComParcelaNula()
    {
        var original = Notificacao.Criar(Guid.NewGuid(), null, TipoNotificacao.Lembrete, "m", DateTime.Now);

        Assert.Null(Mapeamento.ParaEntidade(Mapeamento.ParaLinha(original)).ParcelaId);
    }

    [Fact]
    public void Configuracao_IdaEVolta()
    {
        var original = Configuracao.CriarPadrao();
        original.Atualizar(7, 40000, "L {Nome}", "A {Nome}");

        var volta = Mapeamento.ParaEntidade(Mapeamento.ParaLinha(original));

        Assert.Equal(7, volta.DiasAntecedenciaLembrete);
        Assert.Equal(40000, volta.IntervaloEnvioLoteMs);
        Assert.Equal("L {Nome}", volta.TemplateLembrete);
        Assert.Equal("A {Nome}", volta.TemplateAtraso);
    }

    [Fact]
    public void Linha_LidaNoFormatoDaApiDoSupabase_EhInterpretadaCorretamente()
    {
        // Formato real do PostgREST: numeric com casas, date em ISO, timestamp sem fuso com microssegundos, null.
        const string json = """
            {"id":"01a15792-1a52-7723-bddc-d12f412ec2c1","user_id":"b61b341b-0209-42f5-b564-25863fd7e693",
             "devedor_id":"01a15791-978e-7c44-a72e-d1f1de15b719","descricao":"Moto",
             "valor_emprestado":10000.00,"taxa_juros":0.3000,"valor_total":13000.00,
             "data_primeiro_vencimento":"2026-11-19","data_emprestimo":"2026-10-20",
             "tipo":0,"quantidade_parcelas":1,"status":0,"observacao":null,
             "parcelas":[{"id":"01a15792-1a54-7b6f-98c7-e9e8e871a4c6","user_id":"b61b341b-0209-42f5-b564-25863fd7e693",
               "divida_id":"01a15792-1a52-7723-bddc-d12f412ec2c1","numero":1,"valor_previsto":13000.00,
               "data_vencimento":"2026-11-19",
               "pagamentos":[{"id":"01a15792-6a17-7b54-aab6-3ae158d20e99","parcela_id":"01a15792-1a54-7b6f-98c7-e9e8e871a4c6",
                 "valor":1000.00,"data_pagamento":"2026-10-20","observacao":null}]}]}
            """;

        var linha = JsonSerializer.Deserialize<DividaLinha>(json, ApiSupabaseExtensoes.Json)!;
        var divida = Mapeamento.ParaEntidade(linha);

        Assert.Equal(13000.00m, divida.ValorTotal);
        Assert.Equal(0.3m, divida.TaxaJuros);
        Assert.Equal(new DateOnly(2026, 11, 19), divida.DataPrimeiroVencimento);
        Assert.Equal(StatusDivida.Ativa, divida.Status);
        Assert.Equal(1000m, divida.ValorPago);
        Assert.Equal(1, divida.Parcelas.Count);
    }

    [Fact]
    public void Notificacao_LidaNoFormatoDaApi_AceitaTimestampComMicrossegundosENulo()
    {
        const string json = """
            {"id":"01a1f1b3-5492-7906-b4b9-cad550088a96","user_id":"b61b341b-0209-42f5-b564-25863fd7e693",
             "devedor_id":"01a15791-978e-7c44-a72e-d1f1de15b719","parcela_id":null,"tipo":1,
             "mensagem":"Olá","data_geracao":"2026-11-18T23:47:04.081789","data_envio":null,"status":0}
            """;

        var n = Mapeamento.ParaEntidade(JsonSerializer.Deserialize<NotificacaoLinha>(json, ApiSupabaseExtensoes.Json)!);

        Assert.Equal(TipoNotificacao.Atraso, n.Tipo);
        Assert.Null(n.DataEnvio);
        Assert.Null(n.ParcelaId);
        Assert.Equal(2026, n.DataGeracao.Year);
        Assert.Equal(47, n.DataGeracao.Minute);
    }

    [Fact]
    public void LinhaDeDivida_SemFilhos_NaoGravaAChaveParcelas()
    {
        var d = Divida.Criar(Guid.NewGuid(), "x", 100m, Hoje, TipoDivida.Avulsa, 1, null);

        var json = JsonSerializer.Serialize(Mapeamento.ParaLinha(d), ApiSupabaseExtensoes.Json);

        Assert.DoesNotContain("\"parcelas\"", json);
        Assert.Contains("\"valor_total\":130", json);
        Assert.Contains("\"devedor_id\"", json);
    }
}
