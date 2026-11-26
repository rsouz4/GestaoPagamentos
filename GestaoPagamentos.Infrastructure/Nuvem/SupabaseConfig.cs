namespace GestaoPagamentos.Infrastructure.Nuvem;

/// <summary>
/// Endereço e chave pública do projeto Supabase (seção "Supabase" da configuração).
/// A chave aqui é a pública (anon/publishable); a chave de serviço nunca entra no app.
/// </summary>
public sealed class SupabaseConfig
{
    public string? Url { get; set; }
    public string? AnonKey { get; set; }

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Url) || string.IsNullOrWhiteSpace(AnonKey))
            throw new InvalidOperationException(
                "Configure Supabase:Url e Supabase:AnonKey no arquivo appsettings.Local.json " +
                "(pasta %LOCALAPPDATA%\\GestaoPagamentos).");

        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Supabase:Url deve ser um endereço https, como https://<projeto>.supabase.co.");
    }
}
