using System.Reflection;
using GestaoPagamentos.Domain.Configuracoes;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Infrastructure.Nuvem;

/// <summary>
/// Recria entidades do Domain a partir do banco. Elas têm construtor e setters privados (só o Domain
/// as cria e altera), então a reconstituição usa reflexão, como o EF Core faz. Fica restrita a este arquivo.
/// </summary>
internal static class Reflexao
{
    private const BindingFlags Todos = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static T Criar<T>() where T : class => (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    public static void Definir(object entidade, string propriedade, object? valor)
    {
        // A propriedade pode ser herdada (Id fica em Entity<T>): procura o setter subindo pela hierarquia.
        for (var tipo = entidade.GetType(); tipo is not null; tipo = tipo.BaseType)
        {
            var setter = tipo.GetProperty(propriedade, Todos | BindingFlags.DeclaredOnly)?.GetSetMethod(nonPublic: true);
            if (setter is null) continue;

            setter.Invoke(entidade, [valor]);
            return;
        }

        throw new InvalidOperationException($"Propriedade {propriedade} não encontrada em {entidade.GetType().Name}.");
    }

    public static void AdicionarNaLista<TItem>(object dono, string campo, TItem item)
    {
        var lista = (List<TItem>)dono.GetType().GetField(campo, Todos)!.GetValue(dono)!;
        lista.Add(item);
    }
}

internal static class Mapeamento
{
    // ---- Devedor ----------------------------------------------------------------

    public static DevedorLinha ParaLinha(Devedor d) => new(d.Id, d.Nome, d.Telefone, d.Email, d.Observacao);

    public static Devedor ParaEntidade(DevedorLinha l)
    {
        var d = Reflexao.Criar<Devedor>();
        Reflexao.Definir(d, nameof(Devedor.Id), l.Id);
        Reflexao.Definir(d, nameof(Devedor.Nome), l.Nome);
        Reflexao.Definir(d, nameof(Devedor.Telefone), l.Telefone);
        Reflexao.Definir(d, nameof(Devedor.Email), l.Email);
        Reflexao.Definir(d, nameof(Devedor.Observacao), l.Observacao);
        return d;
    }

    // ---- Dívida, parcelas e pagamentos -------------------------------------------

    public static DividaLinha ParaLinha(Divida d) => new(
        d.Id, d.DevedorId, d.Descricao, d.ValorEmprestado, d.TaxaJuros, d.ValorTotal,
        d.DataPrimeiroVencimento, d.DataEmprestimo, (int)d.Tipo, d.QuantidadeParcelas, (int)d.Status, d.Observacao);

    public static ParcelaLinha ParaLinha(Parcela p) =>
        new(p.Id, p.DividaId, p.Numero, p.ValorPrevisto, p.DataVencimento);

    public static PagamentoLinha ParaLinha(Pagamento p) =>
        new(p.Id, p.ParcelaId, p.Valor, p.DataPagamento, p.Observacao);

    public static Divida ParaEntidade(DividaLinha l)
    {
        var d = Reflexao.Criar<Divida>();
        Reflexao.Definir(d, nameof(Divida.Id), l.Id);
        Reflexao.Definir(d, nameof(Divida.DevedorId), l.DevedorId);
        Reflexao.Definir(d, nameof(Divida.Descricao), l.Descricao);
        Reflexao.Definir(d, nameof(Divida.ValorEmprestado), l.ValorEmprestado);
        Reflexao.Definir(d, nameof(Divida.TaxaJuros), l.TaxaJuros);
        Reflexao.Definir(d, nameof(Divida.ValorTotal), l.ValorTotal);
        Reflexao.Definir(d, nameof(Divida.DataPrimeiroVencimento), l.DataPrimeiroVencimento);
        Reflexao.Definir(d, nameof(Divida.DataEmprestimo), l.DataEmprestimo);
        Reflexao.Definir(d, nameof(Divida.Tipo), (TipoDivida)l.Tipo);
        Reflexao.Definir(d, nameof(Divida.QuantidadeParcelas), l.QuantidadeParcelas);
        Reflexao.Definir(d, nameof(Divida.Status), (StatusDivida)l.Status);
        Reflexao.Definir(d, nameof(Divida.Observacao), l.Observacao);

        foreach (var parcela in (l.Parcelas ?? []).OrderBy(p => p.Numero))
            Reflexao.AdicionarNaLista(d, "_parcelas", ParaEntidade(parcela));

        return d;
    }

    private static Parcela ParaEntidade(ParcelaLinha l)
    {
        var p = Reflexao.Criar<Parcela>();
        Reflexao.Definir(p, nameof(Parcela.Id), l.Id);
        Reflexao.Definir(p, nameof(Parcela.DividaId), l.DividaId);
        Reflexao.Definir(p, nameof(Parcela.Numero), l.Numero);
        Reflexao.Definir(p, nameof(Parcela.ValorPrevisto), l.ValorPrevisto);
        Reflexao.Definir(p, nameof(Parcela.DataVencimento), l.DataVencimento);

        foreach (var pagamento in (l.Pagamentos ?? []).OrderBy(x => x.DataPagamento).ThenBy(x => x.Id))
            Reflexao.AdicionarNaLista(p, "_pagamentos", ParaEntidade(pagamento));

        return p;
    }

    private static Pagamento ParaEntidade(PagamentoLinha l)
    {
        var p = Reflexao.Criar<Pagamento>();
        Reflexao.Definir(p, nameof(Pagamento.Id), l.Id);
        Reflexao.Definir(p, nameof(Pagamento.ParcelaId), l.ParcelaId);
        Reflexao.Definir(p, nameof(Pagamento.Valor), l.Valor);
        Reflexao.Definir(p, nameof(Pagamento.DataPagamento), l.DataPagamento);
        Reflexao.Definir(p, nameof(Pagamento.Observacao), l.Observacao);
        return p;
    }

    // ---- Notificação e configuração -----------------------------------------------

    public static NotificacaoLinha ParaLinha(Notificacao n) => new(
        n.Id, n.DevedorId, n.ParcelaId, (int)n.Tipo, n.Mensagem, n.DataGeracao, n.DataEnvio, (int)n.Status);

    public static Notificacao ParaEntidade(NotificacaoLinha l)
    {
        var n = Reflexao.Criar<Notificacao>();
        Reflexao.Definir(n, nameof(Notificacao.Id), l.Id);
        Reflexao.Definir(n, nameof(Notificacao.DevedorId), l.DevedorId);
        Reflexao.Definir(n, nameof(Notificacao.ParcelaId), l.ParcelaId);
        Reflexao.Definir(n, nameof(Notificacao.Tipo), (TipoNotificacao)l.Tipo);
        Reflexao.Definir(n, nameof(Notificacao.Mensagem), l.Mensagem);
        Reflexao.Definir(n, nameof(Notificacao.DataGeracao), l.DataGeracao);
        Reflexao.Definir(n, nameof(Notificacao.DataEnvio), l.DataEnvio);
        Reflexao.Definir(n, nameof(Notificacao.Status), (StatusNotificacao)l.Status);
        return n;
    }

    public static ConfiguracaoLinha ParaLinha(Configuracao c) => new(
        c.Id, c.DiasAntecedenciaLembrete, c.IntervaloEnvioLoteMs, c.TemplateLembrete, c.TemplateAtraso);

    public static Configuracao ParaEntidade(ConfiguracaoLinha l)
    {
        var c = Reflexao.Criar<Configuracao>();
        Reflexao.Definir(c, nameof(Configuracao.Id), l.Id);
        Reflexao.Definir(c, nameof(Configuracao.DiasAntecedenciaLembrete), l.DiasAntecedenciaLembrete);
        Reflexao.Definir(c, nameof(Configuracao.IntervaloEnvioLoteMs), l.IntervaloEnvioLoteMs);
        Reflexao.Definir(c, nameof(Configuracao.TemplateLembrete), l.TemplateLembrete);
        Reflexao.Definir(c, nameof(Configuracao.TemplateAtraso), l.TemplateAtraso);
        return c;
    }
}
