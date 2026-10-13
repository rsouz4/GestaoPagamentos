using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;

namespace GestaoPagamentos.Application.Dividas;

internal static class DividaMapper
{
    public static DividaResumoDto ParaResumo(Divida d, string devedorNome, DateOnly hoje)
    {
        var abertas = d.Parcelas.Where(p => !p.EstaQuitada).OrderBy(p => p.DataVencimento).ToList();
        return new DividaResumoDto(
            d.Id,
            d.DevedorId,
            devedorNome,
            d.Descricao,
            d.Tipo,
            d.Status,
            d.ValorTotal,
            d.ValorPago,
            d.ValorRestante,
            d.QuantidadeParcelas,
            abertas.Count(p => p.StatusEm(hoje) == StatusParcela.Atrasada),
            abertas.Count > 0 ? abertas[0].DataVencimento : null);
    }

    public static DividaDetalheDto ParaDetalhe(Divida d, Devedor devedor, DateOnly hoje) => new(
        d.Id,
        d.DevedorId,
        devedor.Nome,
        devedor.Telefone,
        d.Descricao,
        d.Observacao,
        d.Tipo,
        d.Status,
        d.ValorTotal,
        d.ValorPago,
        d.ValorRestante,
        d.Parcelas.OrderBy(p => p.Numero).Select(p => ParaParcela(p, hoje)).ToList());

    private static ParcelaDto ParaParcela(Parcela p, DateOnly hoje) => new(
        p.Id,
        p.Numero,
        p.ValorPrevisto,
        p.ValorPago,
        p.ValorRestante,
        p.DataVencimento,
        p.StatusEm(hoje),
        p.Pagamentos
            .OrderBy(x => x.DataPagamento)
            .Select(x => new PagamentoDto(x.Id, x.Valor, x.DataPagamento, x.Observacao))
            .ToList());
}
