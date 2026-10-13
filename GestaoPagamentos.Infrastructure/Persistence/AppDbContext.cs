using System.Reflection;
using Microsoft.EntityFrameworkCore;
using GestaoPagamentos.Domain.Configuracoes;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Devedor> Devedores => Set<Devedor>();
    public DbSet<Divida> Dividas => Set<Divida>();
    public DbSet<Parcela> Parcelas => Set<Parcela>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
