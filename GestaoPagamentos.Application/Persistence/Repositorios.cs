using GestaoPagamentos.Domain.Configuracoes;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Application.Persistence;

public interface IDevedorRepository
{
    /// <summary>Somente leitura, ordenado por nome. O filtro é por parte do nome, sem diferenciar maiúsculas.</summary>
    Task<IReadOnlyList<Devedor>> ListarAsync(string? filtroNome, CancellationToken ct = default);

    Task<Devedor?> ObterAsync(Guid id, CancellationToken ct = default);

    /// <summary>Somente leitura. Ids sem devedor correspondente ficam fora do resultado.</summary>
    Task<IReadOnlyDictionary<Guid, Devedor>> ObterPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    Task<bool> ExisteAsync(Guid id, CancellationToken ct = default);

    Task AdicionarAsync(Devedor devedor, CancellationToken ct = default);

    /// <summary>Remove também as dívidas, parcelas, pagamentos e notificações do devedor.</summary>
    void Remover(Devedor devedor);
}

/// <summary>
/// Uma <see cref="Divida"/> é sempre carregada completa, com parcelas e pagamentos.
/// Nenhum método <c>Listar*</c> garante ordem: quem precisa de ordem ordena.
/// </summary>
public interface IDividaRepository
{
    /// <summary>Somente leitura.</summary>
    Task<IReadOnlyList<Divida>> ListarAsync(StatusDivida? status, Guid? devedorId, CancellationToken ct = default);

    /// <summary>Somente leitura. Dívidas que têm ao menos uma das parcelas informadas.</summary>
    Task<IReadOnlyList<Divida>> ListarPorParcelasAsync(IReadOnlyCollection<Guid> parcelaIds, CancellationToken ct = default);

    Task<Divida?> ObterAsync(Guid dividaId, CancellationToken ct = default);
    Task<Divida?> ObterPorParcelaAsync(Guid parcelaId, CancellationToken ct = default);
    Task<Divida?> ObterPorPagamentoAsync(Guid pagamentoId, CancellationToken ct = default);

    Task<bool> ExisteAtivaDoDevedorAsync(Guid devedorId, CancellationToken ct = default);

    Task AdicionarAsync(Divida divida, CancellationToken ct = default);
}

public interface INotificacaoRepository
{
    /// <summary>Somente leitura, sem ordem garantida.</summary>
    Task<IReadOnlyList<Notificacao>> ListarAsync(StatusNotificacao? status, TipoNotificacao? tipo, CancellationToken ct = default);

    Task<int> ContarPendentesAsync(CancellationToken ct = default);

    Task<Notificacao?> ObterAsync(Guid id, CancellationToken ct = default);

    /// <summary>Notificações pendentes das parcelas informadas, opcionalmente de um só tipo.</summary>
    Task<IReadOnlyList<Notificacao>> ListarPendentesPorParcelasAsync(
        IReadOnlyCollection<Guid> parcelaIds, TipoNotificacao? tipo, CancellationToken ct = default);

    /// <summary>Pares (parcela, tipo) que já têm notificação em qualquer status. Evita gerar em duplicidade.</summary>
    Task<IReadOnlyList<(Guid ParcelaId, TipoNotificacao Tipo)>> ListarChavesExistentesAsync(CancellationToken ct = default);

    Task AdicionarAsync(Notificacao notificacao, CancellationToken ct = default);
}

public interface IConfiguracaoRepository
{
    /// <summary>A única configuração do usuário, ou <c>null</c> se ainda não existir.</summary>
    Task<Configuracao?> ObterAsync(CancellationToken ct = default);

    Task AdicionarAsync(Configuracao configuracao, CancellationToken ct = default);
}
