namespace GestaoPagamentos.Application.Persistence;

/// <summary>
/// Cria uma unidade de trabalho por operação de caso de uso. É a única porta de acesso a dados da
/// camada Application: a implementação (EF Core/SQLite hoje, Supabase depois) fica na Infrastructure.
/// </summary>
public interface IUnitOfWorkFactory
{
    Task<IUnitOfWork> CriarAsync(CancellationToken ct = default);
}

/// <summary>
/// Agrupa os repositórios de uma operação. Entidades obtidas por <c>Obter*</c> e <c>ListarPendentes*</c>
/// são acompanhadas pela unidade de trabalho: o que for alterado nelas, mais o que for passado a
/// <c>Adicionar*</c> e <c>Remover</c>, é gravado de uma vez por <see cref="SalvarAsync"/>.
/// Entidades de <c>Listar*</c> e <c>ObterPorIds*</c> são só para leitura.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IDevedorRepository Devedores { get; }
    IDividaRepository Dividas { get; }
    INotificacaoRepository Notificacoes { get; }
    IConfiguracaoRepository Configuracoes { get; }

    Task SalvarAsync(CancellationToken ct = default);
}
