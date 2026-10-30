using Microsoft.Extensions.Logging;
using System.IO;

namespace GestaoPagamentos.Wpf.Services.Logging;

/// <summary>
/// Logger individual que captura mensagens e as envia para o store centralizado e arquivo.
/// </summary>
internal sealed class InMemoryLogger : ILogger
{
    private readonly InMemoryLogStore _store;
    private readonly string _categoryName;
    private readonly LogLevel _minLevel;
    private readonly Func<string, Task> _fileWriteAction;

    public InMemoryLogger(
        string categoryName,
        InMemoryLogStore store,
        LogLevel minLevel,
        Func<string, Task> fileWriteAction)
    {
        _categoryName = categoryName;
        _store = store;
        _minLevel = minLevel;
        _fileWriteAction = fileWriteAction;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel && logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        try
        {
            var message = formatter(state, exception);
            if (exception is not null)
            {
                message += Environment.NewLine + exception;
            }

            var logEvent = new LogEvent
            {
                Timestamp = DateTime.Now,
                Level = logLevel,
                Category = _categoryName,
                Message = message
            };

            _store.AddEvent(logEvent);

            // Escrever em arquivo de forma assíncrona sem bloquear o logger
            _ = Task.Run(() => _fileWriteAction(logEvent.ToString()));
        }
        catch
        {
            // Falhas em logging não devem interromper a execução
        }
    }
}
