using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Configuracoes;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Application.Notificacoes;

public sealed class GerarNotificacoesUseCase(
    IDividaRepository dividas,
    IDevedorRepository devedores,
    INotificacaoRepository notificacoes,
    ObterConfiguracaoUseCase configuracao,
    IUnitOfWork uow,
    IClock clock)
{
    public async Task<int> ExecuteAsync(CancellationToken ct = default)
    {
        var config = await configuracao.ObterEntidadeAsync(ct);
        var hoje = clock.Hoje;
        var limiteLembrete = hoje.AddDays(config.DiasAntecedenciaLembrete);

        var ativas = await dividas.ListarAtivasAsync(ct);
        var nomes = (await devedores.ObterPorIdsAsync(ativas.Select(d => d.DevedorId).Distinct(), ct))
            .ToDictionary(d => d.Id);

        var atrasadasIds = ativas
            .SelectMany(d => d.Parcelas)
            .Where(p => !p.EstaQuitada && p.DataVencimento < hoje)
            .Select(p => p.Id)
            .ToList();

        foreach (var pendente in await notificacoes.ListarPendentesPorParcelasAsync(atrasadasIds, ct))
        {
            if (pendente.Tipo == TipoNotificacao.Lembrete) pendente.Descartar();
        }

        var geradas = 0;
        foreach (var divida in ativas)
        {
            if (!nomes.TryGetValue(divida.DevedorId, out var devedor)) continue;

            foreach (var parcela in divida.Parcelas.Where(p => !p.EstaQuitada))
            {
                TipoNotificacao? tipo = null;
                if (parcela.DataVencimento < hoje) tipo = TipoNotificacao.Atraso;
                else if (parcela.DataVencimento <= limiteLembrete) tipo = TipoNotificacao.Lembrete;

                if (tipo is null) continue;
                if (await notificacoes.ExisteAsync(parcela.Id, tipo.Value, ct)) continue;

                var template = tipo == TipoNotificacao.Atraso ? config.TemplateAtraso : config.TemplateLembrete;
                var mensagem = WhatsAppLinkBuilder.RenderizarTemplate(
                    template, devedor.Nome, parcela.ValorRestanteEm(hoje), parcela.DataVencimento, divida.Descricao);

                await notificacoes.AdicionarAsync(
                    Notificacao.Criar(devedor.Id, parcela.Id, tipo.Value, mensagem, clock.Agora), ct);
                geradas++;
            }
        }

        await uow.SaveChangesAsync(ct);
        return geradas;
    }
}
