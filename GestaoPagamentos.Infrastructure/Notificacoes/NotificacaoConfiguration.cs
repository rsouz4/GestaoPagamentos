using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;
using GestaoPagamentos.Domain.Notificacoes;

namespace GestaoPagamentos.Infrastructure.Notificacoes;

public sealed class NotificacaoConfiguration : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("notificacoes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Mensagem).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.Tipo).HasConversion<int>();
        builder.Property(n => n.Status).HasConversion<int>();
        builder.Property(n => n.DataGeracao).HasColumnType("timestamp without time zone");
        builder.Property(n => n.DataEnvio).HasColumnType("timestamp without time zone");

        builder.HasOne<Devedor>()
            .WithMany()
            .HasForeignKey(n => n.DevedorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Parcela>()
            .WithMany()
            .HasForeignKey(n => n.ParcelaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.ParcelaId, n.Tipo });
        builder.HasIndex(n => n.Status);
    }
}
