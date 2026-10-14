using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Dividas;

namespace GestaoPagamentos.Application.Dividas;

public sealed class CriarDividaUseCase(IDividaRepository dividas, IDevedorRepository devedores, IUnitOfWork uow)
{
    public async Task<Result<Guid>> ExecuteAsync(CriarDividaCommand cmd, CancellationToken ct = default)
    {
        try
        {
            if (await devedores.ObterPorIdAsync(cmd.DevedorId, ct) is null)
                return Result.Falha<Guid>("Devedor não encontrado.");

            var divida = Divida.Criar(
                cmd.DevedorId,
                cmd.Descricao,
                cmd.ValorEmprestado,
                cmd.PrimeiroVencimento,
                cmd.Tipo,
                cmd.QuantidadeParcelas,
                cmd.Observacao);

            await dividas.AdicionarAsync(divida, ct);
            await uow.SaveChangesAsync(ct);
            return Result.Ok(divida.Id);
        }
        catch (DomainException ex)
        {
            return Result.Falha<Guid>(ex.Message);
        }
    }
}
