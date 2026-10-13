using GestaoPagamentos.Application.Configuracoes;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Application.Notificacoes;

public sealed class EnviarAtrasadosEmLoteUseCase(
    ListarNotificacoesUseCase listar,
    EnviarNotificacaoUseCase enviar,
    ObterConfiguracaoUseCase configuracao)
{
    public async Task<IReadOnlyList<NotificacaoDto>> PrepararAsync(CancellationToken ct = default) =>
        await listar.ExecuteAsync(StatusNotificacao.Pendente, TipoNotificacao.Atraso, ct);

    public async Task<LoteResultado> ExecuteAsync(
        IReadOnlyList<NotificacaoDto> itens,
        IProgress<LoteProgresso>? progresso = null,
        CancellationToken ct = default)
    {
        var intervalo = (await configuracao.ObterEntidadeAsync(ct)).IntervaloEnvioLoteMs;
        var enviadas = 0;
        var puladas = 0;

        for (var i = 0; i < itens.Count; i++)
        {
            if (ct.IsCancellationRequested) return new LoteResultado(enviadas, puladas, true);

            var item = itens[i];
            progresso?.Report(new LoteProgresso(i + 1, itens.Count, item.DevedorNome));

            if (!item.TelefoneValido)
            {
                puladas++;
                continue;
            }

            var resultado = await enviar.ExecuteAsync(item.Id, null, CancellationToken.None);
            if (resultado.Sucesso) enviadas++;
            else puladas++;

            if (i < itens.Count - 1 && intervalo > 0)
            {
                try
                {
                    await Task.Delay(intervalo, ct);
                }
                catch (OperationCanceledException)
                {
                    return new LoteResultado(enviadas, puladas, true);
                }
            }
        }

        return new LoteResultado(enviadas, puladas, false);
    }
}
