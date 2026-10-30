using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Infrastructure;
using GestaoPagamentos.Infrastructure.Persistence;
using GestaoPagamentos.Wpf.Common;
using GestaoPagamentos.Wpf.Configuracoes;
using GestaoPagamentos.Wpf.Dashboard;
using GestaoPagamentos.Wpf.Devedores;
using GestaoPagamentos.Wpf.Dividas;
using GestaoPagamentos.Wpf.Notificacoes;
using GestaoPagamentos.Wpf.Services;
using GestaoPagamentos.Wpf.Services.Logging;
using GestaoPagamentos.Wpf.Shell;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace GestaoPagamentos.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private ILogger<App>? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigurarCultura();

        try
        {
            _host = CriarHost(e.Args);
            _logger = _host.Services.GetRequiredService<ILogger<App>>();

            using (var scope = _host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
            }

            await _host.StartAsync();

            var janela = _host.Services.GetRequiredService<MainWindow>();
            janela.Show();
            await _host.Services.GetRequiredService<MainViewModel>().InicializarAsync();

            // Dispara auto-envio de notificações em background após UI carregar
            _ = DispararAutoEnvioEmBackgroundAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Não foi possível iniciar o aplicativo.\n\nVerifique se o arquivo de banco de dados está acessível e se a connection string em appsettings.json está correta.\n\nDetalhes: {ex.Message}",
                "Gestão de Pagamentos",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task DispararAutoEnvioEmBackgroundAsync()
    {
        try
        {
            if (_host is null)
                return;

            _logger?.LogInformation("Iniciando auto-envio de notificações em background");

            // Aguarda um pouco para garantir que UI está completamente carregada
            await Task.Delay(1000);

            // Usar await using para descartar corretamente serviços IAsyncDisposable
            await using (var scope = _host.Services.CreateAsyncScope())
            {
                var autoEnvioService = scope.ServiceProvider
                    .GetRequiredService<NotificacaoAutoEnvioService>();

                // Executa em background sem bloquear UI
                await autoEnvioService.ExecutarEnvioAutomaticoAoIniciarAsync();
            }

            _logger?.LogInformation("Auto-envio de notificações concluído");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro no auto-envio de notificações em background");
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static IHost CriarHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        // Configurar logging centralizado
        var logStore = new InMemoryLogStore();
        builder.Services.AddSingleton(logStore);
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new InMemoryLogProvider(logStore, minLevel: LogLevel.Debug));

        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddInfrastructure(builder.Configuration);

        builder.Services.AddSingleton<AppEvents>();
        builder.Services.AddSingleton<INavigator, Navigator>();
        builder.Services.AddSingleton<ILauncher, ProcessLauncher>();
        builder.Services.AddSingleton<IDialogService, WpfDialogService>();

        builder.Services.AddSingleton<DevedorService>();
        builder.Services.AddSingleton<DividaService>();
        builder.Services.AddSingleton<PagamentoService>();
        builder.Services.AddSingleton<NotificacaoService>();
        builder.Services.AddSingleton<ConfiguracaoService>();
        builder.Services.AddSingleton<RelatorioService>();
        builder.Services.AddTransient<NotificacaoAutoEnvioService>();

        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<NotificacoesViewModel>();
        builder.Services.AddTransient<ConfiguracoesViewModel>();
        builder.Services.AddTransient<DevedoresViewModel>();
        builder.Services.AddTransient<DividasViewModel>();
        builder.Services.AddTransient<DividaFormViewModel>();
        builder.Services.AddTransient<DividaDetalheViewModel>();
        builder.Services.AddTransient<GestaoPagamentos.Wpf.Logs.LogsViewModel>();

        builder.Services.AddSingleton(new NavItem("dashboard", "Dashboard", "📊", typeof(DashboardViewModel)));
        builder.Services.AddSingleton(new NavItem(MainViewModel.ChaveNotificacoes, "Notificações", "🔔", typeof(NotificacoesViewModel)));
        builder.Services.AddSingleton(new NavItem("devedores", "Devedores", "👤", typeof(DevedoresViewModel)));
        builder.Services.AddSingleton(new NavItem("dividas", "Dívidas", "💳", typeof(DividasViewModel)));
        builder.Services.AddSingleton(new NavItem("logs", "Logs", "📺", typeof(GestaoPagamentos.Wpf.Logs.LogsViewModel)));
        builder.Services.AddSingleton(new NavItem("configuracoes", "Configurações", "⚙", typeof(ConfiguracoesViewModel)));
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }

    private static void ConfigurarCultura()
    {
        var cultura = new CultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;
        Thread.CurrentThread.CurrentUICulture = cultura;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(cultura.IetfLanguageTag)));
    }
}
