using GestaoPagamentos.Application.Common;
using GestaoPagamentos.Application.Configuracoes;
using GestaoPagamentos.Application.Devedores;
using GestaoPagamentos.Application.Dividas;
using GestaoPagamentos.Application.Notificacoes;
using GestaoPagamentos.Application.Pagamentos;
using GestaoPagamentos.Application.Persistence;
using GestaoPagamentos.Application.Relatorios;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GestaoPagamentos.Tests.Servicos;

internal sealed class RelogioDeTeste : IClock
{
    public DateTime Agora { get; set; } = new(2026, 11, 25, 10, 0, 0);
    public DateOnly Hoje => DateOnly.FromDateTime(Agora);
}

internal sealed class LauncherDeTeste : ILauncher
{
    public List<string> Abertos { get; } = [];
    public void Abrir(string url) => Abertos.Add(url);
}

internal sealed class AutomacaoDeTeste : IWhatsAppAutomationService
{
    public bool Resultado { get; set; } = true;
    public List<(string Telefone, string Mensagem)> Enviadas { get; } = [];

    public Task<bool> EnviarMensagemAsync(string telefone, string mensagem, CancellationToken ct = default)
    {
        Enviadas.Add((telefone, mensagem));
        return Task.FromResult(Resultado);
    }

    public Task<bool> VerificarAutenticacaoAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task FecharAsync(CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FabricaDeContexto(SqliteConnection conexao) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conexao).Options);
}

/// <summary>
/// Monta os serviços sobre um SQLite em memória, com relógio fixo (25/11/2026 10:00).
/// Os testes só usam a API pública dos serviços, então valem antes e depois da troca de camada de dados.
/// </summary>
internal sealed class Ambiente : IDisposable
{
    private readonly SqliteConnection _conexao = new("DataSource=:memory:");

    public RelogioDeTeste Relogio { get; } = new();
    public LauncherDeTeste Launcher { get; } = new();
    public AutomacaoDeTeste Automacao { get; } = new();

    public DevedorService Devedores { get; }
    public DividaService Dividas { get; }
    public PagamentoService Pagamentos { get; }
    public NotificacaoService Notificacoes { get; }
    public ConfiguracaoService Configuracoes { get; }
    public RelatorioService Relatorios { get; }

    public DateOnly Hoje => Relogio.Hoje;

    public Ambiente()
    {
        _conexao.Open();
        // Por padrão, SQLite em memória (rápido, sem rede). Com GESTAO_TESTES_ARMAZENAMENTO=Supabase,
        // os mesmos testes rodam contra a conta de teste no Supabase; ver ContratoSupabase.
        IUnitOfWorkFactory fabrica;
        if (ContratoSupabase.Ativo)
        {
            fabrica = ContratoSupabase.ObterFabricaELimpar();
        }
        else
        {
            var fabricaDeContexto = new FabricaDeContexto(_conexao);
            using (var db = fabricaDeContexto.CreateDbContext())
                db.Database.EnsureCreated();

            fabrica = new EfUnitOfWorkFactory(fabricaDeContexto);
        }

        Devedores = new DevedorService(fabrica);
        Dividas = new DividaService(fabrica, Relogio);
        Pagamentos = new PagamentoService(fabrica, Relogio);
        Notificacoes = new NotificacaoService(fabrica, Launcher, NullLogger<EnviarMensagemWhatsAppHandler>.Instance, Relogio);
        Configuracoes = new ConfiguracaoService(fabrica);
        Relatorios = new RelatorioService(fabrica, Relogio);
    }

    public async Task<Guid> CriarDevedorAsync(string nome = "Maria", string? telefone = "11987654321")
    {
        var r = await Devedores.SalvarAsync(new SalvarDevedorCommand(null, nome, telefone, null, null));
        Assert.True(r.Sucesso, r.Erro);
        return r.Valor!.Id;
    }

    public async Task<Guid> CriarDividaAsync(
        Guid devedorId,
        decimal valor = 1000m,
        int diasAteVencimento = 10,
        TipoDivida tipo = TipoDivida.Avulsa,
        int parcelas = 1,
        string descricao = "Emprestimo")
    {
        var r = await Dividas.CriarAsync(new CriarDividaCommand(
            devedorId, descricao, valor, Hoje.AddDays(diasAteVencimento), tipo, parcelas, null));
        Assert.True(r.Sucesso, r.Erro);
        return r.Valor;
    }

    public async Task<DividaDetalheDto> ObterDividaAsync(Guid dividaId)
    {
        var r = await Dividas.ObterAsync(dividaId);
        Assert.True(r.Sucesso, r.Erro);
        return r.Valor!;
    }

    public void Dispose() => _conexao.Dispose();
}
