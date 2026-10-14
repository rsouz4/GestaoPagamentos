using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Configuracoes;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Infrastructure.Configuracoes;
using GestaoPagamentos.Infrastructure.Devedores;
using GestaoPagamentos.Infrastructure.Dividas;
using GestaoPagamentos.Infrastructure.Notificacoes;
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

        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDevedorRepository, DevedorRepository>();
        services.AddScoped<IDividaRepository, DividaRepository>();
        services.AddScoped<INotificacaoRepository, NotificacaoRepository>();
        services.AddScoped<IConfiguracaoRepository, ConfiguracaoRepository>();

        return services;
    }
}
