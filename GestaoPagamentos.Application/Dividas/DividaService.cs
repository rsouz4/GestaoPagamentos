using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Persistence;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Dividas;

namespace GestaoPagamentos.Application.Dividas;

public sealed class DividaService(
    IUnitOfWorkFactory uowFactory,
    IClock clock)
{
    public async Task<IReadOnlyList<DividaResumoDto>> ListarAsync(
        StatusDivida? status = null,
        Guid? devedorId = null,
        CancellationToken ct = default)
    {
        await using var uow = await uowFactory.CriarAsync(ct);

        var lista = (await uow.Dividas.ListarAsync(status, devedorId, ct))
            .OrderByDescending(d => d.DataPrimeiroVencimento)
            .ToList();

        var ids = lista
            .Select(d => d.DevedorId)
            .Distinct()
            .ToList();

        var devedores = await uow.Devedores.ObterPorIdsAsync(ids, ct);

        return lista
            .Select(d => DividaMapper.ParaResumo(
                d,
                devedores.TryGetValue(d.DevedorId, out var devedor) ? devedor.Nome : "-",
                clock.Hoje))
            .ToList();
    }

    public async Task<Result<DividaDetalheDto>> ObterAsync(Guid dividaId, CancellationToken ct = default)
    {
        await using var uow = await uowFactory.CriarAsync(ct);

        var divida = await uow.Dividas.ObterAsync(dividaId, ct);
        if (divida is null) return Result.Falha<DividaDetalheDto>("Dívida não encontrada.");

        var devedor = await uow.Devedores.ObterAsync(divida.DevedorId, ct);
        if (devedor is null) return Result.Falha<DividaDetalheDto>("Devedor da dívida não encontrado.");

        return Result.Ok(DividaMapper.ParaDetalhe(divida, devedor, clock.Hoje));
    }

    public async Task<Result<Guid>> CriarAsync(CriarDividaCommand cmd, CancellationToken ct = default)
    {
        try
        {
            await using var uow = await uowFactory.CriarAsync(ct);

            if (!await uow.Devedores.ExisteAsync(cmd.DevedorId, ct))
                return Result.Falha<Guid>("Devedor não encontrado.");

            var divida = Divida.Criar(
                cmd.DevedorId,
                cmd.Descricao,
                cmd.ValorEmprestado,
                cmd.PrimeiroVencimento,
                cmd.Tipo,
                cmd.QuantidadeParcelas,
                cmd.Observacao);

            await uow.Dividas.AdicionarAsync(divida, ct);
            await uow.SalvarAsync(ct);
            return Result.Ok(divida.Id);
        }
        catch (DomainException ex)
        {
            return Result.Falha<Guid>(ex.Message);
        }
    }

    public async Task<Result> AtualizarAsync(AtualizarDividaCommand cmd, CancellationToken ct = default)
    {
        try
        {
            await using var uow = await uowFactory.CriarAsync(ct);

            var divida = await uow.Dividas.ObterAsync(cmd.DividaId, ct);
            if (divida is null) return Result.Falha("Dívida não encontrada.");

            divida.AtualizarDados(cmd.Descricao, cmd.Observacao);
            await uow.SalvarAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }

    public async Task<Result> AlterarTaxaJurosAsync(Guid dividaId, decimal taxaJuros, CancellationToken ct = default)
    {
        try
        {
            await using var uow = await uowFactory.CriarAsync(ct);

            var divida = await uow.Dividas.ObterAsync(dividaId, ct);
            if (divida is null) return Result.Falha("Dívida não encontrada.");

            divida.AlterarTaxaJuros(taxaJuros);
            await uow.SalvarAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }

    public async Task<Result> CancelarAsync(Guid dividaId, CancellationToken ct = default)
    {
        try
        {
            await using var uow = await uowFactory.CriarAsync(ct);

            var divida = await uow.Dividas.ObterAsync(dividaId, ct);
            if (divida is null) return Result.Falha("Dívida não encontrada.");

            divida.Cancelar();

            var parcelaIds = divida.Parcelas.Select(p => p.Id).ToList();
            var pendentes = await uow.Notificacoes.ListarPendentesPorParcelasAsync(parcelaIds, null, ct);
            foreach (var n in pendentes) n.Descartar();

            await uow.SalvarAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }
}
