using GestaoPagamentos.Application.Autenticacao;
using GestaoPagamentos.Application.Common;
using Microsoft.Extensions.Logging;
using Supabase.Gotrue.Exceptions;

namespace GestaoPagamentos.Infrastructure.Nuvem;

public sealed class SupabaseAuthService(
    ClienteSupabase cliente,
    ISessionStore store,
    ILogger<SupabaseAuthService> logger) : IAuthService
{
    public bool ExigeLogin => true;
    public string? EmailAtual { get; private set; }

    public async Task<bool> RestaurarSessaoAsync(CancellationToken ct = default)
    {
        try
        {
            var c = await cliente.ObterAsync(ct);
            var sessao = await c.Auth.RetrieveSessionAsync();
            if (string.IsNullOrEmpty(sessao?.AccessToken)) return false;

            EmailAtual = sessao.User?.Email;
            return true;
        }
        catch (Exception ex)
        {
            // Sem rede, sessão vencida ou configuração ausente: cai na tela de login.
            logger.LogWarning(ex, "Não foi possível restaurar a sessão guardada.");
            return false;
        }
    }

    public async Task<Result> EntrarAsync(string email, string senha, bool manterConectado, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(senha))
            return Result.Falha("Informe o e-mail e a senha.");

        try
        {
            var c = await cliente.ObterAsync(ct);

            cliente.Sessao.Persistir = manterConectado;
            if (!manterConectado) await store.ApagarAsync(ct);

            var sessao = await c.Auth.SignIn(email.Trim(), senha);
            if (sessao?.User is null) return Result.Falha("E-mail ou senha incorretos.");

            EmailAtual = sessao.User.Email ?? email.Trim();
            return Result.Ok();
        }
        catch (GotrueException ex)
        {
            logger.LogWarning("Login recusado pelo Supabase (código {Codigo}).", ex.StatusCode);
            return Result.Falha(ex.StatusCode is 400 or 401 or 422
                ? "E-mail ou senha incorretos."
                : $"Não foi possível entrar (código {ex.StatusCode}). Tente novamente.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Sem conexão ao entrar.");
            return Result.Falha("Sem conexão com a internet. Verifique a rede e tente de novo.");
        }
        catch (InvalidOperationException ex)
        {
            // Configuração do Supabase ausente ou inválida: a mensagem já diz o que ajustar.
            return Result.Falha(ex.Message);
        }
    }

    public async Task SairAsync(CancellationToken ct = default)
    {
        try
        {
            var c = await cliente.ObterAsync(ct);
            await c.Auth.SignOut();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível encerrar a sessão no servidor; apagando só a cópia local.");
        }
        finally
        {
            await store.ApagarAsync(ct);
            EmailAtual = null;
        }
    }
}
