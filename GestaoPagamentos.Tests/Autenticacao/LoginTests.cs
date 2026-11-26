using GestaoPagamentos.Application.Autenticacao;
using GestaoPagamentos.Infrastructure;
using GestaoPagamentos.Infrastructure.Nuvem;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supabase.Gotrue;

namespace GestaoPagamentos.Tests.Autenticacao;

internal sealed class ArmazenamentoEmMemoria : ISessionStore
{
    public string? Conteudo { get; set; }
    public Task<string?> LerAsync(CancellationToken ct = default) => Task.FromResult(Conteudo);

    public Task GravarAsync(string conteudo, CancellationToken ct = default)
    {
        Conteudo = conteudo;
        return Task.CompletedTask;
    }

    public Task ApagarAsync(CancellationToken ct = default)
    {
        Conteudo = null;
        return Task.CompletedTask;
    }
}

public sealed class SessaoPersistenteSupabaseTests
{
    private static Session NovaSessao() => new()
    {
        AccessToken = "acesso-123",
        RefreshToken = "renova-456",
        ExpiresIn = 3600,
        TokenType = "bearer",
        User = new User { Id = "u1", Email = "eu@exemplo.com" }
    };

    [Fact]
    public void Salvar_ComPersistirLigado_GuardaESessaoVoltaIgual()
    {
        var store = new ArmazenamentoEmMemoria();
        var sessao = new SessaoPersistenteSupabase(store);

        sessao.SaveSession(NovaSessao());
        var lida = sessao.LoadSession();

        Assert.NotNull(store.Conteudo);
        Assert.NotNull(lida);
        Assert.Equal("acesso-123", lida.AccessToken);
        Assert.Equal("renova-456", lida.RefreshToken);
        Assert.Equal("eu@exemplo.com", lida.User?.Email);
    }

    [Fact]
    public void Salvar_ComPersistirDesligado_NaoGravaNada()
    {
        var store = new ArmazenamentoEmMemoria();
        var sessao = new SessaoPersistenteSupabase(store) { Persistir = false };

        sessao.SaveSession(NovaSessao());

        Assert.Null(store.Conteudo);
        Assert.Null(sessao.LoadSession());
    }

    [Fact]
    public void Carregar_SemNadaGuardado_RetornaNulo()
    {
        Assert.Null(new SessaoPersistenteSupabase(new ArmazenamentoEmMemoria()).LoadSession());
    }

    [Fact]
    public void Carregar_ConteudoCorrompido_RetornaNuloEmVezDeLancar()
    {
        var store = new ArmazenamentoEmMemoria { Conteudo = "isto não é json {{{" };

        Assert.Null(new SessaoPersistenteSupabase(store).LoadSession());
    }

    [Fact]
    public async Task Destruir_ApagaOQueEstavaGuardado()
    {
        var store = new ArmazenamentoEmMemoria();
        var sessao = new SessaoPersistenteSupabase(store);
        sessao.SaveSession(NovaSessao());

        await sessao.DestroySessionAsync();

        Assert.Null(store.Conteudo);
    }

    [Fact]
    public async Task VersaoAssincrona_GravaELeIgual()
    {
        var sessao = new SessaoPersistenteSupabase(new ArmazenamentoEmMemoria());

        await sessao.SaveSessionAsync(NovaSessao());
        var lida = await sessao.LoadSessionAsync();

        Assert.Equal("acesso-123", lida?.AccessToken);
    }
}

public sealed class SupabaseConfigTests
{
    [Theory]
    [InlineData(null, "chave")]
    [InlineData("", "chave")]
    [InlineData("https://abc.supabase.co", null)]
    [InlineData("https://abc.supabase.co", "  ")]
    public void Validar_SemUrlOuChave_ExplicaOQueConfigurar(string? url, string? chave)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new SupabaseConfig { Url = url, AnonKey = chave }.Validar());

        Assert.Contains("Supabase:Url", ex.Message);
        Assert.Contains("Supabase:AnonKey", ex.Message);
        Assert.Contains("appsettings.Local.json", ex.Message);
    }

    [Theory]
    [InlineData("http://abc.supabase.co")]
    [InlineData("abc.supabase.co")]
    [InlineData("ftp://abc.supabase.co")]
    public void Validar_UrlSemHttps_Falha(string url)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new SupabaseConfig { Url = url, AnonKey = "chave" }.Validar());

        Assert.Contains("https", ex.Message);
    }

    [Fact]
    public void Validar_ConfiguracaoCompleta_Passa()
    {
        new SupabaseConfig { Url = "https://abc.supabase.co", AnonKey = "chave" }.Validar();
    }
}

public sealed class EscolhaDoModoDeLoginTests
{
    private static ServiceProvider Montar(string? modo)
    {
        var valores = new Dictionary<string, string?>
        {
            // Caminho absoluto: a infraestrutura não cria pastas no perfil do usuário durante o teste.
            ["ConnectionStrings:DefaultConnection"] = "Data Source=/teste-nao-usado/x.db",
            ["Supabase:Url"] = "https://abc.supabase.co",
            ["Supabase:AnonKey"] = "chave"
        };
        if (modo is not null) valores["Autenticacao:Modo"] = modo;

        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddSingleton<ISessionStore, ArmazenamentoEmMemoria>();
        servicos.AddInfrastructure(configuracao);
        return servicos.BuildServiceProvider();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Nenhuma")]
    [InlineData("qualquer-outro-valor")]
    public void SemModoSupabase_AbreDiretoSemLogin(string? modo)
    {
        using var provedor = Montar(modo);

        var auth = provedor.GetRequiredService<IAuthService>();

        Assert.IsType<SemAutenticacao>(auth);
        Assert.False(auth.ExigeLogin);
    }

    [Theory]
    [InlineData("Supabase")]
    [InlineData("supabase")]
    public void ModoSupabase_ExigeLogin(string modo)
    {
        using var provedor = Montar(modo);

        var auth = provedor.GetRequiredService<IAuthService>();

        Assert.IsType<SupabaseAuthService>(auth);
        Assert.True(auth.ExigeLogin);
        Assert.Null(auth.EmailAtual);
    }

    [Fact]
    public async Task SemAutenticacao_RestauraEEntraSempre()
    {
        var auth = new SemAutenticacao();

        Assert.True(await auth.RestaurarSessaoAsync());
        Assert.True((await auth.EntrarAsync("", "", false)).Sucesso);
    }

    [Fact]
    public async Task Supabase_EntrarSemEmailOuSenha_FalhaSemChamarARede()
    {
        using var provedor = Montar("Supabase");
        var auth = provedor.GetRequiredService<IAuthService>();

        var r = await auth.EntrarAsync(" ", "", true);

        Assert.False(r.Sucesso);
        Assert.Equal("Informe o e-mail e a senha.", r.Erro);
    }

    [Fact]
    public async Task Supabase_ComConfiguracaoAusente_EntrarExplicaOQueFazer()
    {
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Data Source=/teste-nao-usado/x.db",
            ["Autenticacao:Modo"] = "Supabase"
        }).Build();
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddSingleton<ISessionStore, ArmazenamentoEmMemoria>();
        servicos.AddInfrastructure(configuracao);
        using var provedor = servicos.BuildServiceProvider();
        var auth = provedor.GetRequiredService<IAuthService>();

        var r = await auth.EntrarAsync("eu@exemplo.com", "senha", true);

        Assert.False(r.Sucesso);
        Assert.Contains("Supabase:Url", r.Erro);
        Assert.False(await auth.RestaurarSessaoAsync());
    }
}
