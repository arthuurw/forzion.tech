using forzion.tech.Domain.Entities;
using forzion.tech.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Nome)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Slug)
            .HasConversion(s => s.Value, v => Slug.Reconstituir(v))
            .HasMaxLength(200)
            .IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        builder.HasOne(t => t.Plano)
               .WithMany()
               .HasForeignKey(t => t.PlanoId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
