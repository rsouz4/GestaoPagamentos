using System.Globalization;

namespace GestaoPagamentos.Application.Notificacoes;

public static class WhatsAppLinkBuilder
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static string? NormalizarTelefone(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return null;

        var digitos = new string(telefone.Where(char.IsDigit).ToArray()).TrimStart('0');

        if (digitos.Length is 10 or 11) return "55" + digitos;
        if (digitos.Length is 12 or 13 && digitos.StartsWith("55", StringComparison.Ordinal)) return digitos;
        return null;
    }

    public static bool TelefoneValido(string? telefone) => NormalizarTelefone(telefone) is not null;

    public static string? MontarLink(string? telefone, string mensagem)
    {
        var normalizado = NormalizarTelefone(telefone);
        return normalizado is null
            ? null
            : $"https://wa.me/{normalizado}?text={Uri.EscapeDataString(mensagem)}";
    }

    public static string RenderizarTemplate(string template, string nome, decimal valor, DateOnly vencimento, string descricao) =>
        template
            .Replace("{Nome}", nome)
            .Replace("{Valor}", valor.ToString("C", PtBr))
            .Replace("{Vencimento}", vencimento.ToString("dd/MM/yyyy", PtBr))
            .Replace("{Descricao}", descricao);
}
