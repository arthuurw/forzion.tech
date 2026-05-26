using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class HealthSnapshotConfiguration : IEntityTypeConfiguration<HealthSnapshot>
{
    public void Configure(EntityTypeBuilder<HealthSnapshot> builder)
    {
        builder.ToTable("health_snapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.CapturadoEm).IsRequired();

        builder.Property(s => s.Ambiente)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.StatusGeral)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.PayloadJson).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasIndex(s => s.CapturadoEm).HasDatabaseName("ix_health_snapshots_capturado_em");
    }
}
