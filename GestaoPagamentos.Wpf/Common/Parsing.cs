using System.Globalization;

namespace GestaoPagamentos.Wpf.Common;

public static class Parsing
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static bool TentarDecimal(string? texto, out decimal valor) =>
        decimal.TryParse(texto, NumberStyles.Currency, PtBr, out valor);
}
