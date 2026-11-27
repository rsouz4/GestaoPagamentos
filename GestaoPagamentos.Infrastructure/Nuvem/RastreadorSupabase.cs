using System.Text.Json;
using System.Text.Json.Nodes;
using GestaoPagamentos.Domain.Configuracoes;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Infrastructure.Nuvem;

/// <summary>
/// Faz o que o EF Core fazia sozinho: lembra como cada entidade estava quando foi lida e, ao salvar,
/// descobre o que mudou. O cliente do Supabase não acompanha mudanças, então o retrato (a linha em
/// JSON) é guardado na leitura e comparado na gravação. O resultado é um único pacote de mudanças
/// para a função SQL <c>aplicar_mudancas</c>, que grava tudo na mesma transação.
/// Cada unidade de trabalho tem o seu rastreador, e entidades lidas duas vezes voltam como o mesmo objeto.
/// </summary>
internal sealed class RastreadorSupabase
{
    private sealed class Rastro<T>(T entidade, string? original) where T : class
    {
        public T Entidade { get; } = entidade;
        /// <summary>Linha como foi lida; <c>null</c> se a entidade é nova e ainda não foi gravada.</summary>
        public string? Original { get; set; } = original;
    }

    private sealed class RastroDivida(Divida entidade, LinhasDivida? original)
    {
        public Divida Entidade { get; } = entidade;
        public LinhasDivida? Original { get; set; } = original;
    }

    private sealed record LinhasDivida(
        string Divida,
        Dictionary<Guid, string> Parcelas,
        Dictionary<Guid, string> Pagamentos);

    private readonly List<Rastro<Devedor>> _devedores = [];
    private readonly List<Devedor> _devedoresRemovidos = [];
    private readonly List<RastroDivida> _dividas = [];
    private readonly List<Rastro<Notificacao>> _notificacoes = [];
    private readonly List<Rastro<Configuracao>> _configuracoes = [];

    private static string Json(object linha) => JsonSerializer.Serialize(linha, ApiSupabaseExtensoes.Json);

    // ---- Registro ---------------------------------------------------------------

    public Devedor? Devedor(Guid id) => _devedores.FirstOrDefault(r => r.Entidade.Id == id)?.Entidade;
    public Divida? Divida(Guid id) => _dividas.FirstOrDefault(r => r.Entidade.Id == id)?.Entidade;
    public Notificacao? Notificacao(Guid id) => _notificacoes.FirstOrDefault(r => r.Entidade.Id == id)?.Entidade;
    public Configuracao? Configuracao() => _configuracoes.FirstOrDefault()?.Entidade;

    public void Leu(Devedor d) => _devedores.Add(new Rastro<Devedor>(d, Json(Mapeamento.ParaLinha(d))));
    public void Adicionou(Devedor d) => _devedores.Add(new Rastro<Devedor>(d, null));
    public void Removeu(Devedor d)
    {
        _devedores.RemoveAll(r => r.Entidade.Id == d.Id);
        _devedoresRemovidos.Add(d);
    }

    public void Leu(Divida d) => _dividas.Add(new RastroDivida(d, Instantaneo(d)));
    public void Adicionou(Divida d) => _dividas.Add(new RastroDivida(d, null));

    public void Leu(Notificacao n) => _notificacoes.Add(new Rastro<Notificacao>(n, Json(Mapeamento.ParaLinha(n))));
    public void Adicionou(Notificacao n) => _notificacoes.Add(new Rastro<Notificacao>(n, null));

    public void Leu(Configuracao c) => _configuracoes.Add(new Rastro<Configuracao>(c, Json(Mapeamento.ParaLinha(c))));
    public void Adicionou(Configuracao c) => _configuracoes.Add(new Rastro<Configuracao>(c, null));

    // ---- Diferença ---------------------------------------------------------------

    /// <summary>Tudo o que mudou desde a leitura, no formato da função SQL; <c>null</c> se nada mudou.</summary>
    public JsonObject? MontarMudancas()
    {
        var mudancas = new Mudancas();

        foreach (var r in _devedores)
        {
            var linha = Mapeamento.ParaLinha(r.Entidade);
            if (Json(linha) != r.Original) mudancas.Gravar("devedores", linha);
        }

        foreach (var d in _devedoresRemovidos) mudancas.Excluir("devedores", d.Id);

        foreach (var r in _dividas)
        {
            var atual = Linhas(r.Entidade);
            var antes = r.Original;

            if (antes is null || antes.Divida != Json(atual.Divida)) mudancas.Gravar("dividas", atual.Divida);

            foreach (var (linha, json) in atual.LinhasParcelas)
                if (antes is null || !antes.Parcelas.TryGetValue(linha.Id, out var o) || o != json)
                    mudancas.Gravar("parcelas", linha);

            foreach (var (linha, json) in atual.LinhasPagamentos)
                if (antes is null || !antes.Pagamentos.TryGetValue(linha.Id, out var o) || o != json)
                    mudancas.Gravar("pagamentos", linha);

            if (antes is not null)
            {
                foreach (var id in antes.Pagamentos.Keys.Except(atual.LinhasPagamentos.Select(x => x.Linha.Id)))
                    mudancas.Excluir("pagamentos", id);
                foreach (var id in antes.Parcelas.Keys.Except(atual.LinhasParcelas.Select(x => x.Linha.Id)))
                    mudancas.Excluir("parcelas", id);
            }
        }

        foreach (var r in _notificacoes)
        {
            var linha = Mapeamento.ParaLinha(r.Entidade);
            if (Json(linha) != r.Original) mudancas.Gravar("notificacoes", linha);
        }

        foreach (var r in _configuracoes)
        {
            var linha = Mapeamento.ParaLinha(r.Entidade);
            if (Json(linha) != r.Original) mudancas.Gravar("configuracoes", linha);
        }

        return mudancas.Vazia ? null : mudancas.ParaJson();
    }

    /// <summary>Depois de gravar com sucesso, o estado atual passa a ser o ponto de comparação.</summary>
    public void Aceitar()
    {
        foreach (var r in _devedores) r.Original = Json(Mapeamento.ParaLinha(r.Entidade));
        _devedoresRemovidos.Clear();
        foreach (var r in _dividas) r.Original = Instantaneo(r.Entidade);
        foreach (var r in _notificacoes) r.Original = Json(Mapeamento.ParaLinha(r.Entidade));
        foreach (var r in _configuracoes) r.Original = Json(Mapeamento.ParaLinha(r.Entidade));
    }

    // ---- Auxiliares ---------------------------------------------------------------

    private sealed record LinhasAtuais(
        DividaLinha Divida,
        List<(ParcelaLinha Linha, string Json)> LinhasParcelas,
        List<(PagamentoLinha Linha, string Json)> LinhasPagamentos);

    private static LinhasAtuais Linhas(Divida d)
    {
        var parcelas = d.Parcelas.Select(p => Mapeamento.ParaLinha(p)).Select(l => (l, Json(l))).ToList();
        var pagamentos = d.Parcelas.SelectMany(p => p.Pagamentos)
            .Select(p => Mapeamento.ParaLinha(p)).Select(l => (l, Json(l))).ToList();
        return new LinhasAtuais(Mapeamento.ParaLinha(d), parcelas, pagamentos);
    }

    private static LinhasDivida Instantaneo(Divida d)
    {
        var atual = Linhas(d);
        return new LinhasDivida(
            Json(atual.Divida),
            atual.LinhasParcelas.ToDictionary(x => x.Linha.Id, x => x.Json),
            atual.LinhasPagamentos.ToDictionary(x => x.Linha.Id, x => x.Json));
    }

    private sealed class Mudancas
    {
        private readonly Dictionary<string, List<object>> _gravar = [];
        private readonly Dictionary<string, List<Guid>> _excluir = [];

        public bool Vazia => _gravar.Count == 0 && _excluir.Count == 0;

        public void Gravar(string tabela, object linha)
        {
            if (!_gravar.TryGetValue(tabela, out var lista)) _gravar[tabela] = lista = [];
            lista.Add(linha);
        }

        public void Excluir(string tabela, Guid id)
        {
            if (!_excluir.TryGetValue(tabela, out var lista)) _excluir[tabela] = lista = [];
            lista.Add(id);
        }

        public JsonObject ParaJson()
        {
            var raiz = new JsonObject();
            foreach (var tabela in _gravar.Keys.Concat(_excluir.Keys).Distinct())
            {
                var bloco = new JsonObject();
                if (_gravar.TryGetValue(tabela, out var gravar))
                    bloco["upsert"] = JsonSerializer.SerializeToNode(gravar, ApiSupabaseExtensoes.Json);
                if (_excluir.TryGetValue(tabela, out var excluir))
                    bloco["delete"] = JsonSerializer.SerializeToNode(excluir, ApiSupabaseExtensoes.Json);
                raiz[tabela] = bloco;
            }

            return raiz;
        }
    }
}
