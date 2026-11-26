using GestaoPagamentos.Application.Autenticacao;
using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Application.Persistence;
using GestaoPagamentos.Infrastructure.Notificacoes;
using GestaoPagamentos.Infrastructure.Nuvem;
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
        services.AddSingleton<IUnitOfWorkFactory, EfUnitOfWorkFactory>();

        // Login: "Nenhuma" (SQLite local, abre direto) ou "Supabase" (e-mail e senha). O app que usa
        // este método precisa registrar um ISessionStore para guardar a sessão.
        var modoAutenticacao = configuration["Autenticacao:Modo"] ?? "Nenhuma";
        if (string.Equals(modoAutenticacao, "Supabase", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(configuration.GetSection("Supabase").Get<SupabaseConfig>() ?? new SupabaseConfig());
            services.AddSingleton<SessaoPersistenteSupabase>();
            services.AddSingleton<ClienteSupabase>();
            services.AddSingleton<IAuthService, SupabaseAuthService>();
        }
        else
        {
            services.AddSingleton<IAuthService, SemAutenticacao>();
        }

        // Registra serviço de automação WhatsApp
        var whatsappConfig = configuration.GetSection("WhatsAppAutomation").Get<PlaywrightWhatsAppConfig>()
            ?? new PlaywrightWhatsAppConfig();
        services.AddSingleton(whatsappConfig);
        services.AddScoped<IWhatsAppAutomationService, PlaywrightWhatsAppService>();

        return services;
    }

    /// <summary>Prepara o armazenamento na abertura do app (hoje: aplica as migrações do SQLite).</summary>
    public static async Task InicializarInfraestruturaAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
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
