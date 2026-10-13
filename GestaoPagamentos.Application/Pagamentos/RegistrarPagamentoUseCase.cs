using System.Globalization;
using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Application.Pagamentos;

public sealed class RegistrarPagamentoUseCase(
    IDividaRepository dividas,
    IDevedorRepository devedores,
    INotificacaoRepository notificacoes,
    IUnitOfWork uow,
    IClock clock)
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public async Task<Result<PagamentoRegistradoDto>> ExecuteAsync(RegistrarPagamentoCommand cmd, CancellationToken ct = default)
    {
        try
        {
            var divida = await dividas.ObterPorParcelaAsync(cmd.ParcelaId, ct);
            if (divida is null) return Result.Falha<PagamentoRegistradoDto>("Parcela não encontrada.");

            var devedor = await devedores.ObterPorIdAsync(divida.DevedorId, ct);
            if (devedor is null) return Result.Falha<PagamentoRegistradoDto>("Devedor da dívida não encontrado.");

            divida.RegistrarPagamento(cmd.ParcelaId, cmd.Valor, cmd.Data, cmd.Observacao);
            var parcela = divida.ObterParcelaOuNulo(cmd.ParcelaId)!;

            if (parcela.EstaQuitada)
            {
                var pendentes = await notificacoes.ListarPendentesPorParcelasAsync([parcela.Id], ct);
                foreach (var n in pendentes) n.Descartar();
            }

            var mensagem = $"Pagamento de {cmd.Valor.ToString("C", PtBr)} recebido de {devedor.Nome} " +
                           $"({divida.Descricao}, parcela {parcela.Numero}).";
            var aviso = Notificacao.Criar(devedor.Id, parcela.Id, TipoNotificacao.PagamentoRecebido, mensagem, clock.Agora);
            aviso.MarcarComoEnviada(clock.Agora);
            await notificacoes.AdicionarAsync(aviso, ct);

            await uow.SaveChangesAsync(ct);

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
}
