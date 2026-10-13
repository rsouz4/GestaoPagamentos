namespace GestaoPagamentos.Domain.Common;

public interface IEntity<out TId> where TId : notnull
{
    TId Id { get; }
}
