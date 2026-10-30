using Microsoft.Extensions.Logging;
using System.IO;

namespace GestaoPagamentos.Wpf.Services.Logging;

/// <summary>
/// Provider de logging customizado que mantém logs em memória e persiste em arquivo.
/// Implementa rotação diária e limpeza automática de arquivos com mais de 7 dias.
/// </summary>
public sealed class InMemoryLogProvider : ILoggerProvider
{
    private readonly InMemoryLogStore _store;
    private readonly string _logDirectory;
    private readonly LogLevel _minLevel;
    private readonly object _fileWriteLock = new();
    private string? _currentLogFile;

    public InMemoryLogProvider(
        InMemoryLogStore store,
        string? logDirectory = null,
        LogLevel minLevel = LogLevel.Debug)
    {
        _store = store;
        _minLevel = minLevel;
        _logDirectory = logDirectory ?? GetDefaultLogDirectory();
        EnsureLogDirectory();
        CleanupOldLogs();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new InMemoryLogger(
            categoryName,
            _store,
            _minLevel,
            WriteToFileAsync);
    }

    public void Dispose()
    {
        // Nada a descartar
    }

    private async Task WriteToFileAsync(string logText)
    {
        try
        {
            lock (_fileWriteLock)
            {
                var filePath = GetTodayLogFile();
                _currentLogFile = filePath;
            }

            await File.AppendAllTextAsync(_currentLogFile!, logText + Environment.NewLine);
        }
        catch
        {
            // Falhas de escrita em arquivo não devem interromper o aplicativo
        }
    }

    private string GetTodayLogFile()
    {
        var fileName = $"logs_{DateTime.Now:yyyy-MM-dd}.txt";
        return Path.Combine(_logDirectory, fileName);
    }

    private string GetDefaultLogDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "GestaoPagamentos", "Logs");
    }

    private void EnsureLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
        }
        catch
        {
            // Falha ao criar diretório não deve interromper
        }
    }

    private void CleanupOldLogs()
    {
        try
        {
            var directory = new DirectoryInfo(_logDirectory);
            var sevenDaysAgo = DateTime.Now.AddDays(-7);

            foreach (var file in directory.GetFiles("logs_*.txt"))
            {
                if (file.LastWriteTime < sevenDaysAgo)
                {
                    file.Delete();
                }
            }
        }
        catch
        {
            // Falhas de limpeza não devem interromper
        }
    }
}
