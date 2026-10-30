using Microsoft.Extensions.Logging;

namespace GestaoPagamentos.Wpf.Services.Logging;

/// <summary>
/// Gerencia o armazenamento centralizado de logs em memória e persistência em arquivo.
/// Emite eventos para a UI atualizar em tempo real.
/// </summary>
public sealed class InMemoryLogStore
{
    private readonly object _lockObj = new();
    private readonly List<LogEvent> _events = new();
    private readonly int _maxInMemoryEvents = 1000;

    public event EventHandler<LogEvent>? LogEventAdded;

    public IReadOnlyList<LogEvent> GetEvents()
    {
        lock (_lockObj)
        {
            return _events.ToList().AsReadOnly();
        }
    }

    public void AddEvent(LogEvent logEvent)
    {
        if (logEvent is null) return;

        lock (_lockObj)
        {
            _events.Add(logEvent);

            // Manter apenas os últimos N eventos em memória
            if (_events.Count > _maxInMemoryEvents)
            {
                _events.RemoveRange(0, _events.Count - _maxInMemoryEvents);
            }
        }

        LogEventAdded?.Invoke(this, logEvent);
    }

    public void Clear()
    {
        lock (_lockObj)
        {
            _events.Clear();
        }
    }
}
