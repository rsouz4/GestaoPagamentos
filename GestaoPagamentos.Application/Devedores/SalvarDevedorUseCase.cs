using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Devedores;

namespace GestaoPagamentos.Application.Devedores;

public sealed class SalvarDevedorUseCase(IDevedorRepository devedores, IUnitOfWork uow)
{
    public async Task<Result<DevedorDto>> ExecuteAsync(SalvarDevedorCommand cmd, CancellationToken ct = default)
    {
        try
        {
            Devedor devedor;
            if (cmd.Id is null)
            {
                devedor = Devedor.Criar(cmd.Nome, cmd.Telefone, cmd.Email, cmd.Observacao);
                await devedores.AdicionarAsync(devedor, ct);
            }
            else
            {
                var existente = await devedores.ObterPorIdAsync(cmd.Id.Value, ct);
                if (existente is null) return Result.Falha<DevedorDto>("Devedor não encontrado.");
                existente.Atualizar(cmd.Nome, cmd.Telefone, cmd.Email, cmd.Observacao);
                devedor = existente;
            }

            await uow.SaveChangesAsync(ct);
            return Result.Ok(DevedorDto.De(devedor));
        }
        catch (DomainException ex)
        {
            return Result.Falha<DevedorDto>(ex.Message);
        }
    }
}
