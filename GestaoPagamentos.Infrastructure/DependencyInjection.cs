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

        // Login: "Nenhuma" (SQLite local, abre direto) ou "Supabase" (e-mail e senha). O app que usa
        // este método precisa registrar um ISessionStore para guardar a sessão.
        var modoAutenticacao = configuration["Autenticacao:Modo"] ?? "Nenhuma";
        var autenticaComSupabase = string.Equals(modoAutenticacao, "Supabase", StringComparison.OrdinalIgnoreCase);
        if (autenticaComSupabase)
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

        // Onde ficam os dados: "Sqlite" (arquivo local, padrão) ou "Supabase" (nuvem; exige o login Supabase,
        // porque é o login que libera as linhas do usuário).
        if (ArmazenaNoSupabase(configuration))
        {
            if (!autenticaComSupabase)
                throw new InvalidOperationException(
                    "Armazenamento:Modo = Supabase exige Autenticacao:Modo = Supabase.");

            services.AddSingleton<IApiSupabase>(provedor => new ApiSupabase(
                provedor.GetRequiredService<ClienteSupabase>(),
                provedor.GetRequiredService<SupabaseConfig>(),
                new HttpClient()));
            services.AddSingleton<IUnitOfWorkFactory, SupabaseUnitOfWorkFactory>();
        }
        else
        {
            services.AddSingleton<IUnitOfWorkFactory, EfUnitOfWorkFactory>();
        }

        // Registra serviço de automação WhatsApp
        var whatsappConfig = configuration.GetSection("WhatsAppAutomation").Get<PlaywrightWhatsAppConfig>()
            ?? new PlaywrightWhatsAppConfig();
        services.AddSingleton(whatsappConfig);
        services.AddScoped<IWhatsAppAutomationService, PlaywrightWhatsAppService>();

        return services;
    }

    private static bool ArmazenaNoSupabase(IConfiguration configuration) =>
        string.Equals(configuration["Armazenamento:Modo"], "Supabase", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Prepara o armazenamento na abertura do app: aplica as migrações do SQLite. No modo Supabase
    /// não há nada a preparar no computador (o esquema vive no banco da nuvem).
    /// </summary>
    public static async Task InicializarInfraestruturaAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var configuracao = services.GetService<IConfiguration>();
        if (configuracao is not null && ArmazenaNoSupabase(configuracao)) return;

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
