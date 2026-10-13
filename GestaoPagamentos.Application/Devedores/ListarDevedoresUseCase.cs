namespace GestaoPagamentos.Application.Devedores;

public sealed class ListarDevedoresUseCase(IDevedorRepository devedores)
{
    public async Task<IReadOnlyList<DevedorDto>> ExecuteAsync(string? filtroNome = null, CancellationToken ct = default)
    {
        var lista = await devedores.ListarAsync(filtroNome, ct);
        return lista.Select(DevedorDto.De).ToList();
    }
}
