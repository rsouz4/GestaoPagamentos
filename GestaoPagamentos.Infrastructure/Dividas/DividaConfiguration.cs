using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GestaoPagamentos.Domain.Devedores;
using GestaoPagamentos.Domain.Dividas;

namespace GestaoPagamentos.Infrastructure.Dividas;

public sealed class DividaConfiguration : IEntityTypeConfiguration<Divida>
{
    public void Configure(EntityTypeBuilder<Divida> builder)
    {
        builder.ToTable("dividas");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Descricao).HasMaxLength(300).IsRequired();
        builder.Property(d => d.ValorTotal).HasPrecision(18, 2);
        builder.Property(d => d.Observacao).HasMaxLength(1000);
        builder.Property(d => d.Tipo).HasConversion<int>();
        builder.Property(d => d.Status).HasConversion<int>();
        builder.Ignore(d => d.ValorPago);
        builder.Ignore(d => d.ValorRestante);

        builder.HasOne<Devedor>()
            .WithMany()
            .HasForeignKey(d => d.DevedorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Parcelas)
            .WithOne()
            .HasForeignKey(p => p.DividaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Parcelas).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(d => d.DevedorId);
        builder.HasIndex(d => d.Status);
    }
}

public sealed class ParcelaConfiguration : IEntityTypeConfiguration<Parcela>
{
    public void Configure(EntityTypeBuilder<Parcela> builder)
    {
        builder.ToTable("parcelas");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.ValorPrevisto).HasPrecision(18, 2);
        builder.Ignore(p => p.ValorPago);
        builder.Ignore(p => p.ValorRestante);
        builder.Ignore(p => p.EstaQuitada);

        builder.HasMany(p => p.Pagamentos)
            .WithOne()
            .HasForeignKey(x => x.ParcelaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Pagamentos).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(p => p.DataVencimento);
        builder.HasIndex(p => new { p.DividaId, p.Numero }).IsUnique();
    }
}

public sealed class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("pagamentos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Valor).HasPrecision(18, 2);
        builder.Property(p => p.Observacao).HasMaxLength(500);
    }
}
