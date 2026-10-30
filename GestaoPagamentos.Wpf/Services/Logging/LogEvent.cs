using Microsoft.Extensions.Logging;

namespace GestaoPagamentos.Wpf.Services.Logging;

/// <summary>
/// Representa um evento de log capturado para exibição e persistência.
/// </summary>
public sealed class LogEvent
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public string LevelDisplay => Level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT ",
        _ => "NONE "
    };

    public string LevelSymbol => Level switch
    {
        LogLevel.Trace => "🔍",
        LogLevel.Debug => "🐛",
        LogLevel.Information => "ℹ️",
        LogLevel.Warning => "⚠️",
        LogLevel.Error => "❌",
        LogLevel.Critical => "🔴",
        _ => "❓"
    };

    public override string ToString() => $"[{Timestamp:HH:mm:ss.fff}] {LevelDisplay} [{Category}] {Message}";
}
