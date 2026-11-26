using GestaoPagamentos.Application.Autenticacao;
using System.Text.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace GestaoPagamentos.Infrastructure.Nuvem;

/// <summary>
/// Entrega ao cliente do Supabase um lugar para guardar a sessão. Só grava quando o usuário
/// pediu para continuar conectado; o <see cref="ISessionStore"/> cuida da proteção do arquivo.
/// A leitura e a escrita são síncronas e rápidas (um arquivo pequeno), o que evita travar a interface.
/// </summary>
public sealed class SessaoPersistenteSupabase(ISessionStore store) : IGotrueSessionPersistence<Session>
{
    /// <summary>Quando falso, a sessão fica só na memória e some ao fechar o app.</summary>
    public bool Persistir { get; set; } = true;

    public void SaveSession(Session session)
    {
        if (!Persistir) return;
        store.GravarAsync(JsonSerializer.Serialize(session)).GetAwaiter().GetResult();
    }

    public void DestroySession() => store.ApagarAsync().GetAwaiter().GetResult();

    public Session? LoadSession()
    {
        var json = store.LerAsync().GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<Session>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task SaveSessionAsync(Session session, CancellationToken cancellationToken = default)
    {
        SaveSession(session);
        return Task.CompletedTask;
    }

    public Task DestroySessionAsync(CancellationToken cancellationToken = default)
    {
        DestroySession();
        return Task.CompletedTask;
    }

    public Task<Session?> LoadSessionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(LoadSession());
}
