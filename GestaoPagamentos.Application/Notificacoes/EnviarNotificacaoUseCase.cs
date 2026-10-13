using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Domain.Common;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Application.Notificacoes;

public sealed class EnviarNotificacaoUseCase(
    INotificacaoRepository notificacoes,
    IDevedorRepository devedores,
    ILauncher launcher,
    IUnitOfWork uow,
    IClock clock)
{
    public async Task<Result> ExecuteAsync(Guid notificacaoId, string? mensagemEditada = null, CancellationToken ct = default)
    {
        try
        {
            var notificacao = await notificacoes.ObterPorIdAsync(notificacaoId, ct);
            if (notificacao is null) return Result.Falha("Notificação não encontrada.");
            if (notificacao.Status != StatusNotificacao.Pendente) return Result.Falha("A notificação não está pendente.");

            var devedor = await devedores.ObterPorIdAsync(notificacao.DevedorId, ct);
            if (devedor is null) return Result.Falha("Devedor não encontrado.");

            if (!string.IsNullOrWhiteSpace(mensagemEditada) && mensagemEditada.Trim() != notificacao.Mensagem)
                notificacao.AtualizarMensagem(mensagemEditada);

            var link = WhatsAppLinkBuilder.MontarLink(devedor.Telefone, notificacao.Mensagem);
            if (link is null) return Result.Falha($"Telefone inválido ou ausente para {devedor.Nome}.");

            launcher.Abrir(link);
            notificacao.MarcarComoEnviada(clock.Agora);
            await uow.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DomainException ex)
        {
            return Result.Falha(ex.Message);
        }
    }
}
