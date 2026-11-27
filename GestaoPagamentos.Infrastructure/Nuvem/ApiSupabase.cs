using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GestaoPagamentos.Infrastructure.Nuvem;

/// <summary>
/// Acesso à API REST que o Supabase gera para as tabelas (PostgREST). Os caminhos são relativos a
/// <c>/rest/v1/</c>, por exemplo <c>devedores?select=*&amp;nome=ilike.*ana*</c>, e os valores dos filtros
/// já chegam codificados para URL.
/// </summary>
public interface IApiSupabase
{
    /// <summary>Lê as linhas de uma consulta, buscando as páginas seguintes sozinho.</summary>
    Task<IReadOnlyList<JsonElement>> ListarAsync(string caminho, CancellationToken ct = default);

    /// <summary>Conta as linhas de uma consulta sem trazê-las.</summary>
    Task<long> ContarAsync(string caminho, CancellationToken ct = default);

    /// <summary>Chama uma função SQL do banco (RPC) com os parâmetros nomeados.</summary>
    Task ChamarFuncaoAsync(string funcao, JsonObject parametros, CancellationToken ct = default);

    /// <summary>Apaga as linhas que casam com o filtro (só as do usuário logado, pelo RLS).</summary>
    Task ExcluirAsync(string caminhoComFiltro, CancellationToken ct = default);
}

public sealed class ApiSupabaseException(int statusHttp, string? codigo, string mensagem)
    : Exception($"Supabase respondeu {statusHttp}{(codigo is null ? "" : $" ({codigo})")}: {mensagem}")
{
    public int StatusHttp { get; } = statusHttp;
    public string? Codigo { get; } = codigo;
}

public static class ApiSupabaseExtensoes
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static async Task<IReadOnlyList<T>> LerAsync<T>(this IApiSupabase api, string caminho, CancellationToken ct = default)
    {
        var elementos = await api.ListarAsync(caminho, ct);
        return elementos.Select(e => e.Deserialize<T>(Json)!).ToList();
    }
}

public sealed class ApiSupabase : IApiSupabase
{
    private const int TamanhoPagina = 1000;

    private readonly Func<CancellationToken, Task<string?>> _obterToken;
    private readonly string _chavePublica;
    private readonly HttpClient _http;
    private readonly Uri _baseRest;

    /// <param name="obterToken">Devolve o token de acesso do usuário logado (já renovado, se preciso).</param>
    public ApiSupabase(Func<CancellationToken, Task<string?>> obterToken, string url, string chavePublica, HttpClient http)
    {
        _obterToken = obterToken;
        _chavePublica = chavePublica;
        _http = http;
        _baseRest = new Uri(url.TrimEnd('/') + "/rest/v1/");
    }

    /// <summary>Uso normal: o token vem da sessão do cliente do Supabase, que o renova sozinho.</summary>
    public ApiSupabase(ClienteSupabase cliente, SupabaseConfig config, HttpClient http)
        : this(async ct =>
            {
                var c = await cliente.ObterAsync(ct);
                return (await c.Auth.RetrieveSessionAsync())?.AccessToken;
            },
            Validada(config).Url!, config.AnonKey!, http)
    {
    }

    private static SupabaseConfig Validada(SupabaseConfig config)
    {
        config.Validar();
        return config;
    }

    public async Task<IReadOnlyList<JsonElement>> ListarAsync(string caminho, CancellationToken ct = default)
    {
        var resultado = new List<JsonElement>();
        // Páginas estáveis exigem ordem fixa; sem pedido de ordem, ordena pelo id.
        var ordenado = caminho.Contains("order=", StringComparison.Ordinal)
            ? caminho
            : caminho + (caminho.Contains('?') ? "&" : "?") + "order=id.asc";

        for (var deslocamento = 0; ; deslocamento += TamanhoPagina)
        {
            using var requisicao = await NovaAsync(HttpMethod.Get, $"{ordenado}&limit={TamanhoPagina}&offset={deslocamento}", ct);
            using var resposta = await _http.SendAsync(requisicao, ct);
            var corpo = await resposta.Content.ReadAsStringAsync(ct);
            if (!resposta.IsSuccessStatusCode) throw Erro((int)resposta.StatusCode, corpo);

            using var documento = JsonDocument.Parse(corpo);
            var linhas = documento.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
            resultado.AddRange(linhas);
            if (linhas.Count < TamanhoPagina) return resultado;
        }
    }

    public async Task<long> ContarAsync(string caminho, CancellationToken ct = default)
    {
        using var requisicao = await NovaAsync(HttpMethod.Get, caminho, ct);
        requisicao.Headers.Add("Prefer", "count=exact");
        requisicao.Headers.Add("Range-Unit", "items");
        // O PostgREST usa "Range: 0-0" (itens), fora do formato "bytes=..." que o .NET valida no cabeçalho Range.
        requisicao.Headers.TryAddWithoutValidation("Range", "0-0");

        using var resposta = await _http.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
            throw Erro((int)resposta.StatusCode, await resposta.Content.ReadAsStringAsync(ct));

        // Content-Range: "0-0/42" ou "*/0"
        var faixa = resposta.Content.Headers.TryGetValues("Content-Range", out var valores) ? valores.FirstOrDefault() : null;
        var total = faixa?.Split('/').LastOrDefault();
        return long.TryParse(total, out var n) ? n : 0;
    }

    public async Task ChamarFuncaoAsync(string funcao, JsonObject parametros, CancellationToken ct = default)
    {
        using var requisicao = await NovaAsync(HttpMethod.Post, $"rpc/{funcao}", ct);
        requisicao.Content = new StringContent(parametros.ToJsonString(), Encoding.UTF8, "application/json");

        using var resposta = await _http.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
            throw Erro((int)resposta.StatusCode, await resposta.Content.ReadAsStringAsync(ct));
    }

    public async Task ExcluirAsync(string caminhoComFiltro, CancellationToken ct = default)
    {
        using var requisicao = await NovaAsync(HttpMethod.Delete, caminhoComFiltro, ct);
        requisicao.Headers.Add("Prefer", "return=minimal");

        using var resposta = await _http.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
            throw Erro((int)resposta.StatusCode, await resposta.Content.ReadAsStringAsync(ct));
    }

    private async Task<HttpRequestMessage> NovaAsync(HttpMethod metodo, string caminho, CancellationToken ct)
    {
        var token = await _obterToken(ct);
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("A sessão expirou. Feche o app e entre novamente.");

        var requisicao = new HttpRequestMessage(metodo, new Uri(_baseRest, caminho));
        requisicao.Headers.Add("apikey", _chavePublica);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return requisicao;
    }

    private static ApiSupabaseException Erro(int status, string corpo)
    {
        string? codigo = null;
        var mensagem = corpo;
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            if (doc.RootElement.TryGetProperty("message", out var m)) mensagem = m.GetString() ?? corpo;
            if (doc.RootElement.TryGetProperty("code", out var c)) codigo = c.GetString();
        }
        catch (JsonException)
        {
            // Resposta que não é JSON: usa o texto como veio.
        }

        return new ApiSupabaseException(status, codigo, mensagem);
    }
}
