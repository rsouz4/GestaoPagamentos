using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Domain.Common;

namespace GestaoPagamentos.Application.Dividas;

public sealed class AtualizarDividaUseCase(IDividaRepository dividas, IUnitOfWork uow)
{
    public async Task<Result> ExecuteAsync(AtualizarDividaCommand cmd, CancellationToken ct = default)
    {
        try
        {
            var divida = await dividas.ObterPorIdAsync(cmd.DividaId, ct);
            if (divida is null) return Result.Falha("Dívida não encontrada.");

            divida.AtualizarDados(cmd.Descricao, cmd.Observacao);
            await uow.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }
}
