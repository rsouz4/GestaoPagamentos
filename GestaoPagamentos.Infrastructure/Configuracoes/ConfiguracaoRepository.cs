using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GestaoPagamentos.Application.Configuracoes;
using GestaoPagamentos.Domain.Configuracoes;
using GestaoPagamentos.Infrastructure.Persistence;

namespace GestaoPagamentos.Infrastructure.Configuracoes;

public sealed class ConfiguracaoConfiguration : IEntityTypeConfiguration<Configuracao>
{
    public void Configure(EntityTypeBuilder<Configuracao> builder)
    {
        builder.ToTable("configuracoes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.TemplateLembrete).HasMaxLength(2000).IsRequired();
        builder.Property(c => c.TemplateAtraso).HasMaxLength(2000).IsRequired();
    }
}

public sealed class ConfiguracaoRepository(AppDbContext db) : IConfiguracaoRepository
{
    public Task<Configuracao?> ObterAsync(CancellationToken ct = default) =>
        db.Configuracoes.FirstOrDefaultAsync(ct);

    public async Task AdicionarAsync(Configuracao configuracao, CancellationToken ct = default) =>
        await db.Configuracoes.AddAsync(configuracao, ct);
}
