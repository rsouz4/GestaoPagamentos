using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Application.Pagamentos;

public sealed class ExcluirPagamentoUseCase(IDividaRepository dividas, IUnitOfWork uow)
{
    public async Task<Result> ExecuteAsync(Guid pagamentoId, CancellationToken ct = default)
    {
        try
        {
            var divida = await dividas.ObterPorPagamentoAsync(pagamentoId, ct);
            if (divida is null) return Result.Falha("Pagamento não encontrado.");

            divida.ExcluirPagamento(pagamentoId);
            await uow.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }
}
