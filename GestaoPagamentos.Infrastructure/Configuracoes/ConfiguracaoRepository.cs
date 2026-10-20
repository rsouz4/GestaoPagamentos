using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GestaoPagamentos.Domain.Configuracoes;

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
