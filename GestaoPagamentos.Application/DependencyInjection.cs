using GestaoPagamentos.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace GestaoPagamentos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();

        var useCases = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("UseCase", StringComparison.Ordinal));

        foreach (var useCase in useCases)
            services.AddTransient(useCase);

        return services;
    }
}
