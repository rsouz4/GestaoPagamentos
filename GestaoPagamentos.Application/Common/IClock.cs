namespace GestaoPagamentos.Application.Common;

public interface IClock
{
    DateTime Agora { get; }
    DateOnly Hoje { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime Agora => DateTime.Now;
    public DateOnly Hoje => DateOnly.FromDateTime(DateTime.Now);
}
