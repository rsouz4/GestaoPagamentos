using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using GestaoPagamentos.Infrastructure.Nuvem;

namespace GestaoPagamentos.Tests.Nuvem;

/// <summary>Servidor HTTP de mentira: guarda cada requisição e responde o que o teste mandar.</summary>
internal sealed class ServidorFalso : HttpMessageHandler
{
    public sealed record Pedido(HttpMethod Metodo, Uri Uri, HttpRequestMessage Mensagem, string Corpo);

    public List<Pedido> Pedidos { get; } = [];
    public Queue<Func<HttpResponseMessage>> Respostas { get; } = new();

    public void Responder(HttpStatusCode status, string json, params (string Nome, string Valor)[] cabecalhos) =>
        Respostas.Enqueue(() =>
        {
            var resposta = new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            foreach (var (nome, valor) in cabecalhos)
            {
                if (!resposta.Headers.TryAddWithoutValidation(nome, valor))
                    resposta.Content.Headers.TryAddWithoutValidation(nome, valor);
            }

            return resposta;
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var corpo = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        Pedidos.Add(new Pedido(request.Method, request.RequestUri!, request, corpo));
        return Respostas.Count > 0 ? Respostas.Dequeue()() : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
    }
}

public sealed class ApiSupabaseTests
{
    private const string Url = "https://abc.supabase.co";

    private static (ApiSupabase Api, ServidorFalso Servidor) Criar(string? token = "token-do-usuario")
    {
        var servidor = new ServidorFalso();
        var api = new ApiSupabase(_ => Task.FromResult(token), Url + "/", "chave-publica", new HttpClient(servidor));
        return (api, servidor);
    }

    private static string Linhas(int quantas) =>
        "[" + string.Join(",", Enumerable.Range(0, quantas).Select(i => $$"""{"id":"{{Guid.NewGuid()}}","n":{{i}}}""")) + "]";

    [Fact]
    public async Task Contar_EnviaOsCabecalhosDeContagemSemLancarEInterpretaOTotal()
    {
        // Regressão: o .NET recusava "Range: 0-0" quando o cabeçalho era adicionado com validação.
        var (api, servidor) = Criar();
        servidor.Responder(HttpStatusCode.PartialContent, "[]", ("Content-Range", "0-0/42"));

        var total = await api.ContarAsync("notificacoes?select=id&status=eq.0");

        Assert.Equal(42, total);
        var pedido = Assert.Single(servidor.Pedidos);
        Assert.Equal(HttpMethod.Get, pedido.Metodo);
        Assert.Equal("count=exact", string.Join(",", pedido.Mensagem.Headers.GetValues("Prefer")));
        Assert.Equal("items", string.Join(",", pedido.Mensagem.Headers.GetValues("Range-Unit")));
        Assert.Equal("0-0", string.Join(",", pedido.Mensagem.Headers.GetValues("Range")));
    }

    [Fact]
    public async Task Contar_SemLinhas_RetornaZero()
    {
        var (api, servidor) = Criar();
        servidor.Responder(HttpStatusCode.OK, "[]", ("Content-Range", "*/0"));

        Assert.Equal(0, await api.ContarAsync("notificacoes?select=id"));
    }

    [Fact]
    public async Task Contar_SemCabecalhoDeFaixa_RetornaZeroEmVezDeLancar()
    {
        var (api, _) = Criar();

        Assert.Equal(0, await api.ContarAsync("notificacoes?select=id"));
    }

    [Fact]
    public async Task Listar_MandaChaveETokenNoFormatoDoSupabase()
    {
        var (api, servidor) = Criar("meu-token");

        await api.ListarAsync("devedores?select=*");

        var pedido = Assert.Single(servidor.Pedidos);
        Assert.Equal("chave-publica", string.Join(",", pedido.Mensagem.Headers.GetValues("apikey")));
        Assert.Equal("Bearer", pedido.Mensagem.Headers.Authorization?.Scheme);
        Assert.Equal("meu-token", pedido.Mensagem.Headers.Authorization?.Parameter);
        Assert.Contains("application/json", pedido.Mensagem.Headers.Accept.ToString());
    }

    [Fact]
    public async Task Listar_MontaOEnderecoComOrdemELimiteDaPagina()
    {
        var (api, servidor) = Criar();

        await api.ListarAsync("devedores?select=*&nome=ilike.*ana*");

        Assert.Equal(
            "https://abc.supabase.co/rest/v1/devedores?select=*&nome=ilike.*ana*&order=id.asc&limit=1000&offset=0",
            Assert.Single(servidor.Pedidos).Uri.ToString());
    }

    [Fact]
    public async Task Listar_RespeitaAOrdemJaPedida()
    {
        var (api, servidor) = Criar();

        await api.ListarAsync("devedores?select=*&order=nome.asc");

        var uri = Assert.Single(servidor.Pedidos).Uri.ToString();
        Assert.Contains("order=nome.asc", uri);
        Assert.DoesNotContain("order=id.asc", uri);
    }

    [Fact]
    public async Task Listar_ConsultaSemParametros_UsaPontoDeInterrogacaoParaAOrdem()
    {
        var (api, servidor) = Criar();

        await api.ListarAsync("devedores");

        Assert.Equal("https://abc.supabase.co/rest/v1/devedores?order=id.asc&limit=1000&offset=0",
            Assert.Single(servidor.Pedidos).Uri.ToString());
    }

    [Fact]
    public async Task Listar_BuscaAsPaginasSeguintesEnquantoVierPaginaCheia()
    {
        var (api, servidor) = Criar();
        servidor.Responder(HttpStatusCode.OK, Linhas(1000));
        servidor.Responder(HttpStatusCode.OK, Linhas(1000));
        servidor.Responder(HttpStatusCode.OK, Linhas(7));

        var linhas = await api.ListarAsync("parcelas?select=*");

        Assert.Equal(2007, linhas.Count);
        Assert.Equal(3, servidor.Pedidos.Count);
        Assert.EndsWith("limit=1000&offset=0", servidor.Pedidos[0].Uri.ToString());
        Assert.EndsWith("limit=1000&offset=1000", servidor.Pedidos[1].Uri.ToString());
        Assert.EndsWith("limit=1000&offset=2000", servidor.Pedidos[2].Uri.ToString());
    }

    [Fact]
    public async Task Listar_PaginaCheiaSeguidaDeVazia_ParaSemLoopInfinito()
    {
        var (api, servidor) = Criar();
        servidor.Responder(HttpStatusCode.OK, Linhas(1000));
        servidor.Responder(HttpStatusCode.OK, "[]");

        var linhas = await api.ListarAsync("parcelas?select=*");

        Assert.Equal(1000, linhas.Count);
        Assert.Equal(2, servidor.Pedidos.Count);
    }

    [Fact]
    public async Task Listar_DevolveCopiasIndependentesDoDocumento()
    {
        var (api, servidor) = Criar();
        var id = Guid.NewGuid();
        servidor.Responder(HttpStatusCode.OK, $$"""[{"id":"{{id}}","nome":"Maria"}]""");

        var linhas = await api.ListarAsync("devedores?select=*");

        // Se os elementos dependessem do JsonDocument já descartado, ler aqui lançaria exceção.
        Assert.Equal("Maria", linhas[0].GetProperty("nome").GetString());
        Assert.Equal(id, linhas[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ChamarFuncao_FazPostEmRpcComOCorpoEmJson()
    {
        var (api, servidor) = Criar();

        await api.ChamarFuncaoAsync("aplicar_mudancas", new JsonObject { ["p"] = new JsonObject { ["devedores"] = new JsonObject() } });

        var pedido = Assert.Single(servidor.Pedidos);
        Assert.Equal(HttpMethod.Post, pedido.Metodo);
        Assert.Equal("https://abc.supabase.co/rest/v1/rpc/aplicar_mudancas", pedido.Uri.ToString());
        Assert.Equal("""{"p":{"devedores":{}}}""", pedido.Corpo);
        Assert.Equal("application/json", pedido.Mensagem.Content!.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Excluir_FazDeleteSemPedirOCorpoDeVolta()
    {
        var (api, servidor) = Criar();

        await api.ExcluirAsync("devedores?id=not.is.null");

        var pedido = Assert.Single(servidor.Pedidos);
        Assert.Equal(HttpMethod.Delete, pedido.Metodo);
        Assert.Equal("https://abc.supabase.co/rest/v1/devedores?id=not.is.null", pedido.Uri.ToString());
        Assert.Equal("return=minimal", string.Join(",", pedido.Mensagem.Headers.GetValues("Prefer")));
    }

    [Fact]
    public async Task ErroDoServidor_ViraExcecaoComCodigoEMensagemDoPostgrest()
    {
        var (api, servidor) = Criar();
        servidor.Responder(HttpStatusCode.BadRequest, """{"code":"23503","message":"violates foreign key constraint","details":null}""");

        var ex = await Assert.ThrowsAsync<ApiSupabaseException>(
            () => api.ChamarFuncaoAsync("aplicar_mudancas", new JsonObject()));

        Assert.Equal(400, ex.StatusHttp);
        Assert.Equal("23503", ex.Codigo);
        Assert.Contains("violates foreign key constraint", ex.Message);
    }

    [Fact]
    public async Task ErroSemJson_UsaOTextoDaRespostaComoMensagem()
    {
        var (api, servidor) = Criar();
        servidor.Responder(HttpStatusCode.BadGateway, "Bad Gateway");

        var ex = await Assert.ThrowsAsync<ApiSupabaseException>(() => api.ListarAsync("devedores?select=*"));

        Assert.Equal(502, ex.StatusHttp);
        Assert.Null(ex.Codigo);
        Assert.Contains("Bad Gateway", ex.Message);
    }

    [Fact]
    public async Task ErroDeLeitura_LancaEmVezDeDevolverListaVazia()
    {
        var (api, servidor) = Criar();
        servidor.Responder(HttpStatusCode.Unauthorized, """{"message":"JWT expired","code":"PGRST301"}""");

        var ex = await Assert.ThrowsAsync<ApiSupabaseException>(() => api.ListarAsync("devedores?select=*"));

        Assert.Equal("PGRST301", ex.Codigo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SemToken_FalhaComMensagemClaraESemChamarARede(string? token)
    {
        var (api, servidor) = Criar(token);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => api.ListarAsync("devedores?select=*"));

        Assert.Contains("sessão expirou", ex.Message);
        Assert.Empty(servidor.Pedidos);
    }
}
