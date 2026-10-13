using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GestaoPagamentos.Domain.Devedores;

namespace GestaoPagamentos.Infrastructure.Devedores;

public sealed class DevedorConfiguration : IEntityTypeConfiguration<Devedor>
{
    public void Configure(EntityTypeBuilder<Devedor> builder)
    {
        builder.ToTable("devedores");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Nome).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Telefone).HasMaxLength(30);
        builder.Property(d => d.Email).HasMaxLength(200);
        builder.Property(d => d.Observacao).HasMaxLength(1000);
        builder.HasIndex(d => d.Nome);
    }
}
