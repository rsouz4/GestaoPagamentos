using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Domain.Configuracoes;

namespace GestaoPagamentos.Application.Configuracoes;

public sealed class ObterConfiguracaoUseCase(IConfiguracaoRepository configuracoes, IUnitOfWork uow)
{
    public async Task<Configuracao> ObterEntidadeAsync(CancellationToken ct = default)
    {
        var config = await configuracoes.ObterAsync(ct);
        if (config is not null) return config;

        config = Configuracao.CriarPadrao();
        await configuracoes.AdicionarAsync(config, ct);
        await uow.SaveChangesAsync(ct);
        return config;
    }

    public async Task<ConfiguracaoDto> ExecuteAsync(CancellationToken ct = default) =>
        ConfiguracaoDto.De(await ObterEntidadeAsync(ct));
}
