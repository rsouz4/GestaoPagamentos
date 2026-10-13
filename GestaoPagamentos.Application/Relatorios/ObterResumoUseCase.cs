using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Domain.Dividas;

namespace GestaoPagamentos.Application.Relatorios;

public sealed class ObterResumoUseCase(IDividaRepository dividas, IClock clock)
{
    public async Task<ResumoDto> ExecuteAsync(CancellationToken ct = default)
    {
        var hoje = clock.Hoje;
        var todas = await dividas.ListarAsync(null, null, ct);
        var validas = todas.Where(d => d.Status != StatusDivida.Cancelada).ToList();
        var ativas = validas.Where(d => d.Status == StatusDivida.Ativa).ToList();

        var abertas = ativas.SelectMany(d => d.Parcelas).Where(p => !p.EstaQuitada).ToList();
        var atrasadas = abertas.Where(p => p.StatusEm(hoje) == StatusParcela.Atrasada).ToList();

        return new ResumoDto(
            TotalARreceber: abertas.Sum(p => p.ValorRestante),
            TotalRecebido: validas.Sum(d => d.ValorPago),
            TotalEmAtraso: atrasadas.Sum(p => p.ValorRestante),
            DividasAtivas: ativas.Count,
            ParcelasPendentes: abertas.Count(p => p.StatusEm(hoje) == StatusParcela.Pendente),
            ParcelasPagasParcialmente: abertas.Count(p => p.StatusEm(hoje) == StatusParcela.PagaParcial),
            ParcelasAtrasadas: atrasadas.Count);
    }
}
