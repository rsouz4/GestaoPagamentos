using System.Globalization;
using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Pagamentos;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;
using GestaoPagamentos.Infrastructure.Persistence;
using GestaoPagamentos.Wpf.Dividas;
using Microsoft.EntityFrameworkCore;

namespace GestaoPagamentos.Wpf.Services;

public sealed class PagamentoService(IDbContextFactory<AppDbContext> dbFactory, IClock clock)
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public async Task<Result<PagamentoRegistradoDto>> RegistrarAsync(RegistrarPagamentoCommand cmd, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var divida = await DividaService.ComParcelas(db)
                .FirstOrDefaultAsync(d => d.Parcelas.Any(p => p.Id == cmd.ParcelaId), ct);
            if (divida is null) return Result.Falha<PagamentoRegistradoDto>("Parcela não encontrada.");

            var devedor = await db.Devedores.FirstOrDefaultAsync(d => d.Id == divida.DevedorId, ct);
            if (devedor is null) return Result.Falha<PagamentoRegistradoDto>("Devedor da dívida não encontrado.");

            divida.RegistrarPagamento(cmd.ParcelaId, cmd.Valor, cmd.Data, cmd.Observacao);
            var parcela = divida.ObterParcelaOuNulo(cmd.ParcelaId)!;

            if (parcela.EstaQuitada)
            {
                var pendentes = await db.Notificacoes
                    .Where(n => n.Status == StatusNotificacao.Pendente && n.ParcelaId == parcela.Id)
                    .ToListAsync(ct);
                foreach (var n in pendentes) n.Descartar();
            }

            var mensagem = $"Pagamento de {cmd.Valor.ToString("C", PtBr)} recebido de {devedor.Nome} " +
                           $"({divida.Descricao}, parcela {parcela.Numero}).";
            var aviso = Notificacao.Criar(devedor.Id, parcela.Id, TipoNotificacao.PagamentoRecebido, mensagem, clock.Agora);
            aviso.MarcarComoEnviada(clock.Agora);
            await db.Notificacoes.AddAsync(aviso, ct);

            await db.SaveChangesAsync(ct);

            return Result.Ok(new PagamentoRegistradoDto(
                divida.Id,
                devedor.Nome,
                parcela.Numero,
                cmd.Valor,
                parcela.EstaQuitada,
                divida.Status == StatusDivida.Quitada));
        }
        catch (DomainException ex)
        {
            return Result.Falha<PagamentoRegistradoDto>(ex.Message);
        }
    }

    public async Task<Result> ExcluirAsync(Guid pagamentoId, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var divida = await DividaService.ComParcelas(db)
                .FirstOrDefaultAsync(d => d.Parcelas.Any(p => p.Pagamentos.Any(x => x.Id == pagamentoId)), ct);
            if (divida is null) return Result.Falha("Pagamento não encontrado.");

            divida.ExcluirPagamento(pagamentoId);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }
}
