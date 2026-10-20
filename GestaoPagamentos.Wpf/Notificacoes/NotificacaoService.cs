using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;
using GestaoPagamentos.Infrastructure.Persistence;
using GestaoPagamentos.Wpf.Configuracoes;
using GestaoPagamentos.Wpf.Dividas;
using Microsoft.EntityFrameworkCore;

namespace GestaoPagamentos.Wpf.Notificacoes;

public sealed class NotificacaoService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILauncher launcher,
    IClock clock)
{
    public async Task<IReadOnlyList<NotificacaoDto>> ListarAsync(
        StatusNotificacao? status = null,
        TipoNotificacao? tipo = null,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = db.Notificacoes.AsNoTracking();
        if (status.HasValue) query = query.Where(n => n.Status == status.Value);
        if (tipo.HasValue) query = query.Where(n => n.Tipo == tipo.Value);
        var lista = await query.OrderByDescending(n => n.DataGeracao).ToListAsync(ct);
        if (lista.Count == 0) return [];

        var devedorIds = lista.Select(n => n.DevedorId).Distinct().ToList();
        var devedores = await db.Devedores.AsNoTracking()
            .Where(d => devedorIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);

        var parcelaIds = lista.Where(n => n.ParcelaId.HasValue).Select(n => n.ParcelaId!.Value).Distinct().ToList();
        var parcelas = parcelaIds.Count == 0
            ? []
            : (await DividaService.ComParcelas(db).AsNoTracking()
                    .Where(d => d.Parcelas.Any(p => parcelaIds.Contains(p.Id)))
                    .ToListAsync(ct))
                .SelectMany(d => d.Parcelas.Select(p => (Divida: d, Parcela: p)))
                .ToDictionary(x => x.Parcela.Id);

        var hoje = clock.Hoje;
        return lista.Select(n =>
        {
            devedores.TryGetValue(n.DevedorId, out var devedor);
            (Divida Divida, Parcela Parcela)? info =
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
                info?.Parcela.ValorRestanteEm(hoje),
                info?.Parcela.DataVencimento,
                n.DataGeracao,
                n.DataEnvio);
        }).ToList();
    }

    public async Task<int> ContarPendentesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Notificacoes.CountAsync(n => n.Status == StatusNotificacao.Pendente, ct);
    }

    public async Task<int> GerarAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var config = await ConfiguracaoService.ObterEntidadeAsync(db, ct);
        var hoje = clock.Hoje;
        var limiteLembrete = hoje.AddDays(config.DiasAntecedenciaLembrete);

        var ativas = await DividaService.ComParcelas(db).Where(d => d.Status == StatusDivida.Ativa).ToListAsync(ct);
        var devedorIds = ativas.Select(d => d.DevedorId).Distinct().ToList();
        var devedores = await db.Devedores.Where(d => devedorIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);

        var atrasadasIds = ativas
            .SelectMany(d => d.Parcelas)
            .Where(p => !p.EstaQuitada && p.DataVencimento < hoje)
            .Select(p => (Guid?)p.Id)
            .ToList();

        if (atrasadasIds.Count > 0)
        {
            var lembretesPendentes = await db.Notificacoes
                .Where(n => n.Status == StatusNotificacao.Pendente
                            && n.Tipo == TipoNotificacao.Lembrete
                            && atrasadasIds.Contains(n.ParcelaId))
                .ToListAsync(ct);
            foreach (var n in lembretesPendentes) n.Descartar();
        }

        var existentes = (await db.Notificacoes
                .Where(n => n.ParcelaId != null)
                .Select(n => new { n.ParcelaId, n.Tipo })
                .ToListAsync(ct))
            .Select(x => (x.ParcelaId!.Value, x.Tipo))
            .ToHashSet();

        var geradas = 0;
        foreach (var divida in ativas)
        {
            if (!devedores.TryGetValue(divida.DevedorId, out var devedor)) continue;

            foreach (var parcela in divida.Parcelas.Where(p => !p.EstaQuitada))
            {
                TipoNotificacao? tipo = null;
                if (parcela.DataVencimento < hoje) tipo = TipoNotificacao.Atraso;
                else if (parcela.DataVencimento <= limiteLembrete) tipo = TipoNotificacao.Lembrete;

                if (tipo is null) continue;
                if (!existentes.Add((parcela.Id, tipo.Value))) continue;

                var template = tipo == TipoNotificacao.Atraso ? config.TemplateAtraso : config.TemplateLembrete;
                var mensagem = WhatsAppLinkBuilder.RenderizarTemplate(
                    template, devedor.Nome, parcela.ValorRestanteEm(hoje), parcela.DataVencimento, divida.Descricao);

                await db.Notificacoes.AddAsync(
                    Notificacao.Criar(devedor.Id, parcela.Id, tipo.Value, mensagem, clock.Agora), ct);
                geradas++;
            }
        }

        await db.SaveChangesAsync(ct);
        return geradas;
    }

    public async Task<Result> EnviarAsync(Guid notificacaoId, string? mensagemEditada = null, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var notificacao = await db.Notificacoes.FirstOrDefaultAsync(n => n.Id == notificacaoId, ct);
            if (notificacao is null) return Result.Falha("Notificação não encontrada.");
            if (notificacao.Status != StatusNotificacao.Pendente) return Result.Falha("A notificação não está pendente.");

            var devedor = await db.Devedores.AsNoTracking().FirstOrDefaultAsync(d => d.Id == notificacao.DevedorId, ct);
            if (devedor is null) return Result.Falha("Devedor não encontrado.");

            if (!string.IsNullOrWhiteSpace(mensagemEditada) && mensagemEditada.Trim() != notificacao.Mensagem)
                notificacao.AtualizarMensagem(mensagemEditada);

            var link = WhatsAppLinkBuilder.MontarLink(devedor.Telefone, notificacao.Mensagem);
            if (link is null) return Result.Falha($"Telefone inválido ou ausente para {devedor.Nome}.");

            launcher.Abrir(link);
            notificacao.MarcarComoEnviada(clock.Agora);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }

    public async Task<Result> IgnorarAsync(Guid notificacaoId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var notificacao = await db.Notificacoes.FirstOrDefaultAsync(n => n.Id == notificacaoId, ct);
        if (notificacao is null) return Result.Falha("Notificação não encontrada.");

        notificacao.Ignorar();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<IReadOnlyList<NotificacaoDto>> PrepararLoteAtrasadosAsync(CancellationToken ct = default) =>
        ListarAsync(StatusNotificacao.Pendente, TipoNotificacao.Atraso, ct);

    public async Task<LoteResultado> EnviarLoteAsync(
        IReadOnlyList<NotificacaoDto> itens,
        IProgress<LoteProgresso>? progresso = null,
        CancellationToken ct = default)
    {
        int intervalo;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
            intervalo = (await ConfiguracaoService.ObterEntidadeAsync(db, ct)).IntervaloEnvioLoteMs;

        var enviadas = 0;
        var puladas = 0;

        for (var i = 0; i < itens.Count; i++)
        {
            if (ct.IsCancellationRequested) return new LoteResultado(enviadas, puladas, true);

            var item = itens[i];
            progresso?.Report(new LoteProgresso(i + 1, itens.Count, item.DevedorNome));

            if (!item.TelefoneValido)
            {
                puladas++;
                continue;
            }

            var resultado = await EnviarAsync(item.Id, null, CancellationToken.None);
            if (resultado.Sucesso) enviadas++;
            else puladas++;

            if (i < itens.Count - 1 && intervalo > 0)
            {
                try
                {
                    await Task.Delay(intervalo, ct);
                }
                catch (OperationCanceledException)
                {
                    return new LoteResultado(enviadas, puladas, true);
                }
            }
        }

        return new LoteResultado(enviadas, puladas, false);
    }
}
