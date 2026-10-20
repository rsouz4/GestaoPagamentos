using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GestaoPagamentos.Infrastructure.Persistence;

namespace GestaoPagamentos.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"A connection string '{ConnectionStringName}' não foi configurada.");

        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(connectionString));

        return services;
    }
}
