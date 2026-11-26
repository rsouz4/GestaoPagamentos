using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Persistence;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Devedores;

namespace GestaoPagamentos.Application.Devedores;

public sealed class DevedorService(IUnitOfWorkFactory uowFactory)
{
    public async Task<IReadOnlyList<DevedorDto>> ListarAsync(string? filtroNome = null, CancellationToken ct = default)
    {
        await using var uow = await uowFactory.CriarAsync(ct);

        var lista = await uow.Devedores.ListarAsync(filtroNome, ct);
        return lista.Select(DevedorDto.De).ToList();
    }

    public async Task<Result<DevedorDto>> SalvarAsync(SalvarDevedorCommand cmd, CancellationToken ct = default)
    {
        try
        {
            await using var uow = await uowFactory.CriarAsync(ct);

            Devedor devedor;
            if (cmd.Id is null)
            {
                devedor = Devedor.Criar(cmd.Nome, cmd.Telefone, cmd.Email, cmd.Observacao);
                await uow.Devedores.AdicionarAsync(devedor, ct);
            }
            else
            {
                var existente = await uow.Devedores.ObterAsync(cmd.Id.Value, ct);
                if (existente is null) return Result.Falha<DevedorDto>("Devedor não encontrado.");
                existente.Atualizar(cmd.Nome, cmd.Telefone, cmd.Email, cmd.Observacao);
                devedor = existente;
            }

            await uow.SalvarAsync(ct);
            return Result.Ok(DevedorDto.De(devedor));
        }
        catch (DomainException ex)
        {
            return Result.Falha<DevedorDto>(ex.Message);
        }
    }

    public async Task<Result> ExcluirAsync(Guid devedorId, CancellationToken ct = default)
    {
        await using var uow = await uowFactory.CriarAsync(ct);

        var devedor = await uow.Devedores.ObterAsync(devedorId, ct);
        if (devedor is null) return Result.Falha("Devedor não encontrado.");

        if (await uow.Dividas.ExisteAtivaDoDevedorAsync(devedorId, ct))
            return Result.Falha("Não é possível excluir um devedor com dívidas ativas.");

        uow.Devedores.Remover(devedor);
        await uow.SalvarAsync(ct);
        return Result.Ok();
    }
}
