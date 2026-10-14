using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Application.Notificacoes;

public sealed class ListarNotificacoesUseCase(
    INotificacaoRepository notificacoes,
    IDevedorRepository devedores,
    IDividaRepository dividas,
    GestaoPagamentos.Application.Common.IClock clock)
{
    public async Task<IReadOnlyList<NotificacaoDto>> ExecuteAsync(
        StatusNotificacao? status = null,
        TipoNotificacao? tipo = null,
        CancellationToken ct = default)
    {
        var lista = await notificacoes.ListarAsync(status, tipo, ct);
        if (lista.Count == 0) return [];

        var nomes = (await devedores.ObterPorIdsAsync(lista.Select(n => n.DevedorId).Distinct(), ct))
            .ToDictionary(d => d.Id);

        var parcelaIds = lista.Where(n => n.ParcelaId.HasValue).Select(n => n.ParcelaId!.Value).Distinct().ToList();
        var parcelas = (await dividas.ListarPorParcelasAsync(parcelaIds, ct))
            .SelectMany(d => d.Parcelas.Select(p => (Divida: d, Parcela: p)))
            .ToDictionary(x => x.Parcela.Id);

        return lista.Select(n =>
        {
            nomes.TryGetValue(n.DevedorId, out var devedor);
            (Domain.Dividas.Divida Divida, Domain.Dividas.Parcela Parcela)? info =
                n.ParcelaId.HasValue && parcelas.TryGetValue(n.ParcelaId.Value, out var x) ? x : null;

            return new NotificacaoDto(
                n.Id,
                n.DevedorId,
                devedor?.Nome ?? "-",
                devedor?.Telefone,
                WhatsAppLinkBuilder.TelefoneValido(devedor?.Telefone),
                n.Tipo,
                n.Status,
                n.Mensagem,
                info?.Divida.Descricao,
                info?.Parcela.Numero,
                info?.Parcela.ValorRestanteEm(clock.Hoje),
                info?.Parcela.DataVencimento,
                n.DataGeracao,
                n.DataEnvio);
        }).ToList();
    }
}
