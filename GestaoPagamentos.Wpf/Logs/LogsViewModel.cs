using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestaoPagamentos.Wpf.Common;
using GestaoPagamentos.Wpf.Services.Logging;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace GestaoPagamentos.Wpf.Logs;

public sealed partial class LogsViewModel : ViewModelBase
{
    private readonly InMemoryLogStore _logStore;
    private readonly ILogger<LogsViewModel> _logger;
    private CancellationTokenSource? _scrollCts;

    [ObservableProperty]
    private ObservableCollection<LogEvent> eventos = new();

    [ObservableProperty]
    private bool autoScroll = true;

    [ObservableProperty]
    private string caminhoLogs = string.Empty;

    [ObservableProperty]
    private string informacaoDiagnostico = string.Empty;

    public LogsViewModel(
        InMemoryLogStore logStore,
        ILogger<LogsViewModel> logger)
    {
        _logStore = logStore;
        _logger = logger;

        // Criar a coleção observável com os eventos iniciais
        foreach (var evento in _logStore.GetEvents())
        {
            Eventos.Add(evento);
        }

        // Inscrever-se em novos eventos do log
        _logStore.LogEventAdded += (s, e) =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Eventos.Add(e);

                // Manter no máximo 1000 eventos exibidos
                while (Eventos.Count > 1000)
                {
                    Eventos.RemoveAt(0);
                }
            });
        };

        AtualizarInformacoesDiagnostico();
    }

    public override async Task InicializarAsync()
    {
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void LimparLogs()
    {
        Eventos.Clear();
        _logStore.Clear();
        _logger.LogInformation("Logs foram limpos pelo usuário");
    }

    [RelayCommand]
    private async Task AbrirArquivoLogAsync()
    {
        try
        {
            var hoje = DateTime.Now.ToString("yyyy-MM-dd");
            var caminhoLogs = ObterCaminhoLogs();
            var caminhoArquivo = Path.Combine(caminhoLogs, $"logs_{hoje}.txt");

            if (!File.Exists(caminhoArquivo))
            {
                MessageBox.Show(
                    "Arquivo de log de hoje ainda não foi criado ou está vazio.",
                    "Arquivo não encontrado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Abrir o arquivo com o aplicativo padrão
            var psi = new System.Diagnostics.ProcessStartInfo(caminhoArquivo) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);

            _logger.LogInformation("Arquivo de log aberto: {CaminhoArquivo}", caminhoArquivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao abrir arquivo de log");
            MessageBox.Show(
                $"Erro ao abrir arquivo de log: {ex.Message}",
                "Erro",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task AbrirPastaLogAsync()
    {
        try
        {
            var caminho = ObterCaminhoLogs();
            Directory.CreateDirectory(caminho);

            var psi = new System.Diagnostics.ProcessStartInfo("explorer.exe", caminho) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);

            _logger.LogInformation("Pasta de logs aberta: {Caminho}", caminho);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao abrir pasta de logs");
            MessageBox.Show(
                $"Erro ao abrir pasta de logs: {ex.Message}",
                "Erro",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        await Task.CompletedTask;
    }

    private void AtualizarInformacoesDiagnostico()
    {
        try
        {
            var caminhoLogs = ObterCaminhoLogs();
            CaminhoLogs = caminhoLogs;

            var infoBuilder = new System.Text.StringBuilder();
            infoBuilder.AppendLine("📋 Informações de Diagnóstico");
            infoBuilder.AppendLine("═══════════════════════════════════");
            infoBuilder.AppendLine($"Caminho dos logs: {caminhoLogs}");
            infoBuilder.AppendLine();

            // Informações do Playwright
            infoBuilder.AppendLine("🎭 Playwright & Chrome");
            infoBuilder.AppendLine("───────────────────────────────────");
            try
            {
                var caminhoChrome = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ms-playwright", "chromium-1248", "chrome-win64", "chrome.exe");

                var existeChrome = File.Exists(caminhoChrome);
                var status = existeChrome ? "✅ Instalado" : "❌ Não instalado";
                infoBuilder.AppendLine($"Chrome: {status}");
                infoBuilder.AppendLine($"Caminho esperado: {caminhoChrome}");
                infoBuilder.AppendLine();

                if (!existeChrome)
                {
                    infoBuilder.AppendLine("⚠️  ATENÇÃO: Chrome não está instalado");
                    infoBuilder.AppendLine("Execute no terminal PowerShell:");
                    infoBuilder.AppendLine();
                    infoBuilder.AppendLine("./bin/Debug/net10.0-windows/playwright.ps1 install chromium");
                    infoBuilder.AppendLine();
                    infoBuilder.AppendLine("Ou em Release:");
                    infoBuilder.AppendLine();
                    infoBuilder.AppendLine("./playwright.ps1 install chromium");
                }
            }
            catch (Exception ex)
            {
                infoBuilder.AppendLine($"Erro ao verificar Chrome: {ex.Message}");
            }

            InformacaoDiagnostico = infoBuilder.ToString();
        }
        catch (Exception ex)
        {
            InformacaoDiagnostico = $"Erro ao carregar diagnóstico: {ex.Message}";
        }
    }

    private string ObterCaminhoLogs()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "GestaoPagamentos", "Logs");
    }
}
