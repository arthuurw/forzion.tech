using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class GrupoMuscularConfiguration : IEntityTypeConfiguration<GrupoMuscular>
{
    public void Configure(EntityTypeBuilder<GrupoMuscular> builder)
    {
        builder.ToTable("grupos_musculares");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Nome)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(g => g.Nome).IsUnique();

        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.UpdatedAt);
    }
}
