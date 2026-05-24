using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class PlanoPlataformaConfiguration : IEntityTypeConfiguration<PlanoPlataforma>
{
    public void Configure(EntityTypeBuilder<PlanoPlataforma> builder)
    {
        builder.ToTable("planos_plataforma");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nome)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Tier)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Descricao)
            .HasMaxLength(200);

        builder.Property(p => p.MaxAlunos).IsRequired();

        builder.Property(p => p.Preco)
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(p => p.IsAtivo).IsRequired();

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);
    }
}
