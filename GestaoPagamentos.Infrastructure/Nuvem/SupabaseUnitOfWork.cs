using System.Text.Json.Nodes;
using GestaoPagamentos.Application.Persistence;
using GestaoPagamentos.Domain.Configuracoes;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Infrastructure.Nuvem;

public sealed class SupabaseUnitOfWorkFactory(IApiSupabase api) : IUnitOfWorkFactory
{
    public Task<IUnitOfWork> CriarAsync(CancellationToken ct = default) =>
        Task.FromResult<IUnitOfWork>(new SupabaseUnitOfWork(api));
}

internal sealed class SupabaseUnitOfWork : IUnitOfWork
{
    private readonly IApiSupabase _api;
    private readonly RastreadorSupabase _rastreador = new();

    public SupabaseUnitOfWork(IApiSupabase api)
    {
        _api = api;
        Devedores = new SupabaseDevedorRepository(api, _rastreador);
        Dividas = new SupabaseDividaRepository(api, _rastreador);
        Notificacoes = new SupabaseNotificacaoRepository(api, _rastreador);
        Configuracoes = new SupabaseConfiguracaoRepository(api, _rastreador);
    }

    public IDevedorRepository Devedores { get; }
    public IDividaRepository Dividas { get; }
    public INotificacaoRepository Notificacoes { get; }
    public IConfiguracaoRepository Configuracoes { get; }

    public async Task SalvarAsync(CancellationToken ct = default)
    {
        var mudancas = _rastreador.MontarMudancas();
        if (mudancas is null) return;

        // Uma única chamada: o banco grava tudo na mesma transação.
        await _api.ChamarFuncaoAsync("aplicar_mudancas", new JsonObject { ["p"] = mudancas }, ct);
        _rastreador.Aceitar();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Auxiliares para montar os filtros da API REST.</summary>
internal static class Consulta
{
    private const int TamanhoBloco = 50;

    public static IEnumerable<string> EmBlocos(IEnumerable<Guid> ids) =>
        ids.Distinct().Chunk(TamanhoBloco).Select(bloco => $"in.({string.Join(',', bloco)})");
}

internal sealed class SupabaseDevedorRepository(IApiSupabase api, RastreadorSupabase rastreador) : IDevedorRepository
{
    public async Task<IReadOnlyList<Devedor>> ListarAsync(string? filtroNome, CancellationToken ct = default)
    {
        var caminho = "devedores?select=*&order=nome.asc";
        if (!string.IsNullOrWhiteSpace(filtroNome))
        {
            // ilike não diferencia maiúsculas. Em filtros de valor único o PostgREST não aceita aspas (elas
            // virariam parte do texto); basta codificar para URL. Um "*" digitado funciona como curinga.
            caminho += "&nome=ilike." + Uri.EscapeDataString($"*{filtroNome.Trim()}*");
        }

        return (await api.LerAsync<DevedorLinha>(caminho, ct)).Select(Mapeamento.ParaEntidade).ToList();
    }

    public async Task<Devedor?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        var rastreado = rastreador.Devedor(id);
        if (rastreado is not null) return rastreado;

        var linha = (await api.LerAsync<DevedorLinha>($"devedores?select=*&id=eq.{id}", ct)).FirstOrDefault();
        if (linha is null) return null;

        var devedor = Mapeamento.ParaEntidade(linha);
        rastreador.Leu(devedor);
        return devedor;
    }

    public async Task<IReadOnlyDictionary<Guid, Devedor>> ObterPorIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var resultado = new Dictionary<Guid, Devedor>();
        foreach (var filtro in Consulta.EmBlocos(ids))
            foreach (var linha in await api.LerAsync<DevedorLinha>($"devedores?select=*&id={filtro}", ct))
                resultado[linha.Id] = Mapeamento.ParaEntidade(linha);

        return resultado;
    }

    public async Task<bool> ExisteAsync(Guid id, CancellationToken ct = default) =>
        (await api.LerAsync<IdLinha>($"devedores?select=id&id=eq.{id}", ct)).Count > 0;

    public Task AdicionarAsync(Devedor devedor, CancellationToken ct = default)
    {
        rastreador.Adicionou(devedor);
        return Task.CompletedTask;
    }

    public void Remover(Devedor devedor) => rastreador.Removeu(devedor);
}

internal sealed class SupabaseDividaRepository(IApiSupabase api, RastreadorSupabase rastreador) : IDividaRepository
{
    private const string Completa = "select=*,parcelas(*,pagamentos(*))";

    public async Task<IReadOnlyList<Divida>> ListarAsync(
        StatusDivida? status, Guid? devedorId, CancellationToken ct = default)
    {
        var caminho = $"dividas?{Completa}";
        if (status.HasValue) caminho += $"&status=eq.{(int)status.Value}";
        if (devedorId.HasValue) caminho += $"&devedor_id=eq.{devedorId.Value}";

        return (await api.LerAsync<DividaLinha>(caminho, ct)).Select(Mapeamento.ParaEntidade).ToList();
    }

    public async Task<IReadOnlyList<Divida>> ListarPorParcelasAsync(
        IReadOnlyCollection<Guid> parcelaIds, CancellationToken ct = default)
    {
        var dividaIds = new HashSet<Guid>();
        foreach (var filtro in Consulta.EmBlocos(parcelaIds))
            foreach (var linha in await api.LerAsync<IdDividaLinha>($"parcelas?select=divida_id&id={filtro}", ct))
                dividaIds.Add(linha.DividaId);

        var resultado = new List<Divida>();
        foreach (var filtro in Consulta.EmBlocos(dividaIds))
            resultado.AddRange((await api.LerAsync<DividaLinha>($"dividas?{Completa}&id={filtro}", ct))
                .Select(Mapeamento.ParaEntidade));

        return resultado;
    }

    public async Task<Divida?> ObterAsync(Guid dividaId, CancellationToken ct = default)
    {
        var rastreada = rastreador.Divida(dividaId);
        if (rastreada is not null) return rastreada;

        var linha = (await api.LerAsync<DividaLinha>($"dividas?{Completa}&id=eq.{dividaId}", ct)).FirstOrDefault();
        if (linha is null) return null;

        var divida = Mapeamento.ParaEntidade(linha);
        rastreador.Leu(divida);
        return divida;
    }

    public async Task<Divida?> ObterPorParcelaAsync(Guid parcelaId, CancellationToken ct = default)
    {
        var dona = (await api.LerAsync<IdDividaLinha>($"parcelas?select=divida_id&id=eq.{parcelaId}", ct)).FirstOrDefault();
        return dona is null ? null : await ObterAsync(dona.DividaId, ct);
    }

    public async Task<Divida?> ObterPorPagamentoAsync(Guid pagamentoId, CancellationToken ct = default)
    {
        var dona = (await api.LerAsync<IdParcelaLinha>($"pagamentos?select=parcela_id&id=eq.{pagamentoId}", ct)).FirstOrDefault();
        return dona is null ? null : await ObterPorParcelaAsync(dona.ParcelaId, ct);
    }

    public async Task<bool> ExisteAtivaDoDevedorAsync(Guid devedorId, CancellationToken ct = default) =>
        (await api.LerAsync<IdLinha>(
            $"dividas?select=id&devedor_id=eq.{devedorId}&status=eq.{(int)StatusDivida.Ativa}", ct)).Count > 0;

    public Task AdicionarAsync(Divida divida, CancellationToken ct = default)
    {
        rastreador.Adicionou(divida);
        return Task.CompletedTask;
    }
}

internal sealed class SupabaseNotificacaoRepository(IApiSupabase api, RastreadorSupabase rastreador) : INotificacaoRepository
{
    public async Task<IReadOnlyList<Notificacao>> ListarAsync(
        StatusNotificacao? status, TipoNotificacao? tipo, CancellationToken ct = default)
    {
        var caminho = "notificacoes?select=*";
        if (status.HasValue) caminho += $"&status=eq.{(int)status.Value}";
        if (tipo.HasValue) caminho += $"&tipo=eq.{(int)tipo.Value}";

        return (await api.LerAsync<NotificacaoLinha>(caminho, ct)).Select(Mapeamento.ParaEntidade).ToList();
    }

    public async Task<int> ContarPendentesAsync(CancellationToken ct = default) =>
        (int)await api.ContarAsync($"notificacoes?select=id&status=eq.{(int)StatusNotificacao.Pendente}", ct);

    public async Task<Notificacao?> ObterAsync(Guid id, CancellationToken ct = default)
    {
        var rastreada = rastreador.Notificacao(id);
        if (rastreada is not null) return rastreada;

        var linha = (await api.LerAsync<NotificacaoLinha>($"notificacoes?select=*&id=eq.{id}", ct)).FirstOrDefault();
        return linha is null ? null : Rastrear(linha);
    }

    public async Task<IReadOnlyList<Notificacao>> ListarPendentesPorParcelasAsync(
        IReadOnlyCollection<Guid> parcelaIds, TipoNotificacao? tipo, CancellationToken ct = default)
    {
        var resultado = new List<Notificacao>();
        foreach (var filtro in Consulta.EmBlocos(parcelaIds))
        {
            var caminho = $"notificacoes?select=*&status=eq.{(int)StatusNotificacao.Pendente}&parcela_id={filtro}";
            if (tipo.HasValue) caminho += $"&tipo=eq.{(int)tipo.Value}";

            foreach (var linha in await api.LerAsync<NotificacaoLinha>(caminho, ct))
                resultado.Add(rastreador.Notificacao(linha.Id) ?? Rastrear(linha));
        }

        return resultado;
    }

    public async Task<IReadOnlyList<(Guid ParcelaId, TipoNotificacao Tipo)>> ListarChavesExistentesAsync(
        CancellationToken ct = default)
    {
        var chaves = await api.LerAsync<ChaveNotificacaoLinha>(
            "notificacoes?select=parcela_id,tipo&parcela_id=not.is.null", ct);
        return chaves.Select(c => (c.ParcelaId, (TipoNotificacao)c.Tipo)).ToList();
    }

    public Task AdicionarAsync(Notificacao notificacao, CancellationToken ct = default)
    {
        rastreador.Adicionou(notificacao);
        return Task.CompletedTask;
    }

    private Notificacao Rastrear(NotificacaoLinha linha)
    {
        var notificacao = Mapeamento.ParaEntidade(linha);
        rastreador.Leu(notificacao);
        return notificacao;
    }
}

internal sealed class SupabaseConfiguracaoRepository(IApiSupabase api, RastreadorSupabase rastreador) : IConfiguracaoRepository
{
    public async Task<Configuracao?> ObterAsync(CancellationToken ct = default)
    {
        var rastreada = rastreador.Configuracao();
        if (rastreada is not null) return rastreada;

        var linha = (await api.LerAsync<ConfiguracaoLinha>("configuracoes?select=*", ct)).FirstOrDefault();
        if (linha is null) return null;

        var configuracao = Mapeamento.ParaEntidade(linha);
        rastreador.Leu(configuracao);
        return configuracao;
    }

    public Task AdicionarAsync(Configuracao configuracao, CancellationToken ct = default)
    {
        rastreador.Adicionou(configuracao);
        return Task.CompletedTask;
    }
}
