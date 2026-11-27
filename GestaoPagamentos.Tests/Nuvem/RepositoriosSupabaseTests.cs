using System.Text.Json;
using System.Text.Json.Nodes;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;
using GestaoPagamentos.Infrastructure.Nuvem;

namespace GestaoPagamentos.Tests.Nuvem;

/// <summary>API do Supabase de mentira: guarda o que foi pedido e devolve respostas prontas, sem rede.</summary>
internal sealed class ApiFalsa : IApiSupabase
{
    public List<string> Leituras { get; } = [];
    public List<(string Funcao, JsonObject Parametros)> Funcoes { get; } = [];
    public List<string> Exclusoes { get; } = [];
    public List<(string Prefixo, string Json)> Respostas { get; } = [];
    public long Contagem { get; set; }

    public Task<IReadOnlyList<JsonElement>> ListarAsync(string caminho, CancellationToken ct = default)
    {
        Leituras.Add(caminho);
        var json = Respostas.FirstOrDefault(r => caminho.StartsWith(r.Prefixo, StringComparison.Ordinal)).Json ?? "[]";
        using var documento = JsonDocument.Parse(json);
        IReadOnlyList<JsonElement> linhas = documento.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
        return Task.FromResult(linhas);
    }

    public Task<long> ContarAsync(string caminho, CancellationToken ct = default)
    {
        Leituras.Add(caminho);
        return Task.FromResult(Contagem);
    }

    public Task ChamarFuncaoAsync(string funcao, JsonObject parametros, CancellationToken ct = default)
    {
        Funcoes.Add((funcao, JsonNode.Parse(parametros.ToJsonString())!.AsObject()));
        return Task.CompletedTask;
    }

    public Task ExcluirAsync(string caminhoComFiltro, CancellationToken ct = default)
    {
        Exclusoes.Add(caminhoComFiltro);
        return Task.CompletedTask;
    }
}

public sealed class RepositoriosSupabaseTests
{
    private static readonly Guid IdDevedor = Guid.Parse("01a15791-978e-7c44-a72e-d1f1de15b719");

    private static string LinhaDevedor(string nome = "Maria") =>
        $$"""[{"id":"{{IdDevedor}}","user_id":"b61b341b-0209-42f5-b564-25863fd7e693","nome":"{{nome}}","telefone":"11987654321","email":null,"observacao":null}]""";

    [Fact]
    public async Task ListarDevedores_PedeOrdemPorNomeEFiltroSemDiferenciarMaiusculas()
    {
        var api = new ApiFalsa { Respostas = { ("devedores?", LinhaDevedor()) } };
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();

        var lista = await uow.Devedores.ListarAsync("  ari  ");

        var pedido = Assert.Single(api.Leituras);
        Assert.StartsWith("devedores?select=*&order=nome.asc&nome=ilike.", pedido);
        Assert.Contains("nome=ilike." + Uri.EscapeDataString("*ari*"), pedido);
        Assert.Equal("Maria", Assert.Single(lista).Nome);
    }

    [Fact]
    public async Task ListarDevedores_SemFiltro_NaoMandaFiltroDeNome()
    {
        var api = new ApiFalsa();
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();

        await uow.Devedores.ListarAsync(null);

        Assert.DoesNotContain("ilike", Assert.Single(api.Leituras));
    }

    [Fact]
    public async Task ListarDevedores_CaracteresEspeciaisNoFiltroVaoCodificadosESemAspas()
    {
        var api = new ApiFalsa();
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();

        await uow.Devedores.ListarAsync("a,b(c)\"d");

        var pedido = Assert.Single(api.Leituras);
        Assert.Contains("nome=ilike." + Uri.EscapeDataString("*a,b(c)\"d*"), pedido);
        Assert.DoesNotContain("ilike.\"", pedido);
        Assert.DoesNotContain("ilike.%22", pedido);
    }

    [Fact]
    public async Task ObterDevedor_Inexistente_RetornaNulo()
    {
        await using var uow = await new SupabaseUnitOfWorkFactory(new ApiFalsa()).CriarAsync();

        Assert.Null(await uow.Devedores.ObterAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ObterDevedorDuasVezes_DevolveOMesmoObjetoComUmaSoLeitura()
    {
        var api = new ApiFalsa { Respostas = { ("devedores?", LinhaDevedor()) } };
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();

        var primeiro = await uow.Devedores.ObterAsync(IdDevedor);
        var segundo = await uow.Devedores.ObterAsync(IdDevedor);

        Assert.Same(primeiro, segundo);
        Assert.Single(api.Leituras);
    }

    [Fact]
    public async Task Salvar_SemMudancas_NaoChamaOBanco()
    {
        var api = new ApiFalsa { Respostas = { ("devedores?", LinhaDevedor()) } };
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();
        await uow.Devedores.ObterAsync(IdDevedor);

        await uow.SalvarAsync();

        Assert.Empty(api.Funcoes);
    }

    [Fact]
    public async Task Salvar_ComMudanca_ChamaAFuncaoUmaVezEDepoisNaoRepete()
    {
        var api = new ApiFalsa { Respostas = { ("devedores?", LinhaDevedor()) } };
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();
        var devedor = (await uow.Devedores.ObterAsync(IdDevedor))!;

        devedor.Atualizar("Maria Silva", "11987654321", null, null);
        await uow.SalvarAsync();
        await uow.SalvarAsync();

        var chamada = Assert.Single(api.Funcoes);
        Assert.Equal("aplicar_mudancas", chamada.Funcao);
        var linha = chamada.Parametros["p"]!["devedores"]!["upsert"]![0]!;
        Assert.Equal("Maria Silva", (string?)linha["nome"]);
        Assert.Equal(IdDevedor, (Guid)linha["id"]!);
    }

    [Fact]
    public async Task Salvar_VariasTabelasDeUmaOperacao_VaoNaMesmaChamada()
    {
        var api = new ApiFalsa();
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();
        var divida = Divida.Criar(IdDevedor, "x", 1000m, new DateOnly(2026, 12, 1), TipoDivida.Avulsa, 1, null);
        await uow.Dividas.AdicionarAsync(divida);
        await uow.Notificacoes.AdicionarAsync(Notificacao.Criar(IdDevedor, divida.Parcelas[0].Id, TipoNotificacao.Lembrete, "m", DateTime.Now));

        await uow.SalvarAsync();

        var p = Assert.Single(api.Funcoes).Parametros["p"]!.AsObject();
        Assert.Equal(["dividas", "parcelas", "notificacoes"], p.Select(x => x.Key));
    }

    [Fact]
    public async Task ObterDividaPorPagamento_SegueOCaminhoPagamentoParcelaDivida()
    {
        var idParcela = Guid.NewGuid();
        var idDivida = Guid.NewGuid();
        var api = new ApiFalsa
        {
            Respostas =
            {
                ("pagamentos?select=parcela_id", $$"""[{"parcela_id":"{{idParcela}}"}]"""),
                ("parcelas?select=divida_id", $$"""[{"divida_id":"{{idDivida}}"}]"""),
            }
        };
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();

        var divida = await uow.Dividas.ObterPorPagamentoAsync(Guid.NewGuid());

        Assert.Null(divida); // a última consulta (a da dívida) não tem resposta pronta
        Assert.Equal(3, api.Leituras.Count);
        Assert.StartsWith("pagamentos?select=parcela_id&id=eq.", api.Leituras[0]);
        Assert.Equal($"parcelas?select=divida_id&id=eq.{idParcela}", api.Leituras[1]);
        Assert.Equal($"dividas?select=*,parcelas(*,pagamentos(*))&id=eq.{idDivida}", api.Leituras[2]);
    }

    [Fact]
    public async Task ListarDividas_AplicaFiltrosDeStatusEDevedor()
    {
        var api = new ApiFalsa();
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();

        await uow.Dividas.ListarAsync(StatusDivida.Cancelada, IdDevedor);

        Assert.Equal(
            $"dividas?select=*,parcelas(*,pagamentos(*))&status=eq.2&devedor_id=eq.{IdDevedor}",
            Assert.Single(api.Leituras));
    }

    [Fact]
    public async Task ObterPorIds_QuebraListasGrandesEmBlocos()
    {
        var api = new ApiFalsa();
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();
        var ids = Enumerable.Range(0, 120).Select(_ => Guid.NewGuid()).ToList();

        await uow.Devedores.ObterPorIdsAsync(ids);

        Assert.Equal(3, api.Leituras.Count);
        Assert.All(api.Leituras, l => Assert.Contains("id=in.(", l));
    }

    [Fact]
    public async Task ObterPorIds_SemIds_NaoFazConsulta()
    {
        var api = new ApiFalsa();
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();

        var resultado = await uow.Devedores.ObterPorIdsAsync([]);

        Assert.Empty(resultado);
        Assert.Empty(api.Leituras);
    }

    [Fact]
    public async Task ContarPendentes_UsaAContagemDoServidor()
    {
        var api = new ApiFalsa { Contagem = 7 };
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();

        Assert.Equal(7, await uow.Notificacoes.ContarPendentesAsync());
        Assert.Equal("notificacoes?select=id&status=eq.0", Assert.Single(api.Leituras));
    }

    [Fact]
    public async Task ChavesExistentes_SaoLidasComoParDeParcelaETipo()
    {
        var parcela = Guid.NewGuid();
        var api = new ApiFalsa { Respostas = { ("notificacoes?select=parcela_id,tipo", $$"""[{"parcela_id":"{{parcela}}","tipo":1}]""") } };
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();

        var chaves = await uow.Notificacoes.ListarChavesExistentesAsync();

        Assert.Equal((parcela, TipoNotificacao.Atraso), Assert.Single(chaves));
    }

    [Fact]
    public async Task ExisteAtivaDoDevedor_ConsultaSoDividasAtivasDoDevedor()
    {
        var api = new ApiFalsa { Respostas = { ("dividas?select=id", $$"""[{"id":"{{Guid.NewGuid()}}"}]""") } };
        await using var uow = await new SupabaseUnitOfWorkFactory(api).CriarAsync();

        Assert.True(await uow.Dividas.ExisteAtivaDoDevedorAsync(IdDevedor));
        Assert.Equal($"dividas?select=id&devedor_id=eq.{IdDevedor}&status=eq.0", Assert.Single(api.Leituras));
    }
}
