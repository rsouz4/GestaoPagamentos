using System.Globalization;
using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Persistence;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Application.Pagamentos;

public sealed class PagamentoService(IUnitOfWorkFactory uowFactory, IClock clock)
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public async Task<Result<PagamentoRegistradoDto>> RegistrarAsync(RegistrarPagamentoCommand cmd, CancellationToken ct = default)
    {
        try
        {
            await using var uow = await uowFactory.CriarAsync(ct);

            var divida = await uow.Dividas.ObterPorParcelaAsync(cmd.ParcelaId, ct);
            if (divida is null) return Result.Falha<PagamentoRegistradoDto>("Parcela não encontrada.");

            var devedor = await uow.Devedores.ObterAsync(divida.DevedorId, ct);
            if (devedor is null) return Result.Falha<PagamentoRegistradoDto>("Devedor da dívida não encontrado.");

            divida.RegistrarPagamento(cmd.ParcelaId, cmd.Valor, cmd.Data, cmd.Observacao);
            var parcela = divida.ObterParcelaOuNulo(cmd.ParcelaId)!;

            if (parcela.EstaQuitada)
            {
                var pendentes = await uow.Notificacoes.ListarPendentesPorParcelasAsync([parcela.Id], null, ct);
                foreach (var n in pendentes) n.Descartar();
            }

            var mensagem = $"Pagamento de {cmd.Valor.ToString("C", PtBr)} recebido de {devedor.Nome} " +
                           $"({divida.Descricao}, parcela {parcela.Numero}).";
            var aviso = Notificacao.Criar(devedor.Id, parcela.Id, TipoNotificacao.PagamentoRecebido, mensagem, clock.Agora);
            aviso.MarcarComoEnviada(clock.Agora);
            await uow.Notificacoes.AdicionarAsync(aviso, ct);

            await uow.SalvarAsync(ct);

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
            await using var uow = await uowFactory.CriarAsync(ct);

            var divida = await uow.Dividas.ObterPorPagamentoAsync(pagamentoId, ct);
            if (divida is null) return Result.Falha("Pagamento não encontrado.");

            divida.ExcluirPagamento(pagamentoId);
            await uow.SalvarAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }

    public async Task<Result<PagamentoRegistradoDto>> RenovarComJurosAsync(RenovarParcelaComJurosCommand cmd, CancellationToken ct = default)
    {
        try
        {
            await using var uow = await uowFactory.CriarAsync(ct);

            var divida = await uow.Dividas.ObterPorParcelaAsync(cmd.ParcelaId, ct);
            if (divida is null) return Result.Falha<PagamentoRegistradoDto>("Parcela não encontrada.");

            var devedor = await uow.Devedores.ObterAsync(divida.DevedorId, ct);
            if (devedor is null) return Result.Falha<PagamentoRegistradoDto>("Devedor da dívida não encontrado.");

            var pagamento = divida.RenovarComJuros(cmd.ParcelaId, cmd.Data);
            var parcela = divida.ObterParcelaOuNulo(cmd.ParcelaId)!;

            var mensagem = $"Juros de {pagamento.Valor.ToString("C", PtBr)} cobrados de {devedor.Nome} " +
                           $"({divida.Descricao}, parcela {parcela.Numero}) - Dívida renovada.";
            var aviso = Notificacao.Criar(devedor.Id, parcela.Id, TipoNotificacao.PagamentoRecebido, mensagem, clock.Agora);
            aviso.MarcarComoEnviada(clock.Agora);
            await uow.Notificacoes.AdicionarAsync(aviso, ct);

            await uow.SalvarAsync(ct);

            return Result.Ok(new PagamentoRegistradoDto(
                divida.Id,
                devedor.Nome,
                parcela.Numero,
                pagamento.Valor,
                false,
                false));
        }
        catch (DomainException ex)
        {
            return Result.Falha<PagamentoRegistradoDto>(ex.Message);
        }
    }
}
