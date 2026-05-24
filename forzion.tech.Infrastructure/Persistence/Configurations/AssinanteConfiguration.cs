using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class AssinanteConfiguration : IEntityTypeConfiguration<Assinante>
{
    public void Configure(EntityTypeBuilder<Assinante> builder)
    {
        builder.ToTable("assinantes");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AlunoId).IsRequired();
        builder.HasIndex(a => a.AlunoId).IsUnique();

        builder.Property(a => a.Nome).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Email).HasMaxLength(256);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt);
    }
}
