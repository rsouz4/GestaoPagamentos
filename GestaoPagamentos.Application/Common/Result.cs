namespace GestaoPagamentos.Application.Common;

public class Result
{
    public bool Sucesso { get; }
    public string? Erro { get; }

    protected Result(bool sucesso, string? erro)
    {
        Sucesso = sucesso;
        Erro = erro;
    }

    public static Result Ok() => new(true, null);
    public static Result Falha(string erro) => new(false, erro);
    public static Result<T> Ok<T>(T valor) => new(true, valor, null);
    public static Result<T> Falha<T>(string erro) => new(false, default, erro);
}

public sealed class Result<T> : Result
{
    public T? Valor { get; }

    internal Result(bool sucesso, T? valor, string? erro) : base(sucesso, erro)
    {
        Valor = valor;
    }
}
