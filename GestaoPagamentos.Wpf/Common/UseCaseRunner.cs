using System.Globalization;
using Microsoft.Extensions.DependencyInjection;

namespace GestaoPagamentos.Wpf.Common;

public sealed class UseCaseRunner(IServiceScopeFactory scopeFactory)
{
    public async Task<TResult> ExecutarAsync<TUseCase, TResult>(Func<TUseCase, Task<TResult>> acao)
        where TUseCase : notnull
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await acao(scope.ServiceProvider.GetRequiredService<TUseCase>());
    }
}

public static class Parsing
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static bool TentarDecimal(string? texto, out decimal valor) =>
        decimal.TryParse(texto, NumberStyles.Number, PtBr, out valor);
}
