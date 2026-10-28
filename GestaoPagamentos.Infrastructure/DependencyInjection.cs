using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Infrastructure.Notificacoes;
using GestaoPagamentos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestaoPagamentos.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"A connection string '{ConnectionStringName}' não foi configurada.");

        var dataSource = GetDataSourcePath(connectionString);
        if (dataSource is not null)
        {
            var directory = Path.GetDirectoryName(dataSource);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            connectionString = $"Data Source={dataSource}";
        }

        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(connectionString));

        // Registra serviço de automação WhatsApp
        var whatsappConfig = configuration.GetSection("WhatsAppAutomation").Get<PlaywrightWhatsAppConfig>()
            ?? new PlaywrightWhatsAppConfig();
        services.AddSingleton(whatsappConfig);
        services.AddScoped<IWhatsAppAutomationService, PlaywrightWhatsAppService>();

        return services;
    }

    private static string? GetDataSourcePath(string connectionString)
    {
        const string prefix = "Data Source=";
        if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = connectionString[prefix.Length..].Trim();
        if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path))
        {
            return null;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "GestaoPagamentos", path);
    }
}
