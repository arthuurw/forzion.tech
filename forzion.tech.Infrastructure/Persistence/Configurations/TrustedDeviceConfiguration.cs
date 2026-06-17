using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class TrustedDeviceConfiguration : IEntityTypeConfiguration<TrustedDevice>
{
    public void Configure(EntityTypeBuilder<TrustedDevice> builder)
    {
        builder.ToTable("trusted_devices");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.ContaId).HasColumnName("conta_id");
        builder.Property(d => d.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
        builder.Property(d => d.ExpiraEm).HasColumnName("expira_em");
        builder.Property(d => d.CriadoEm).HasColumnName("criado_em");
        builder.Property(d => d.UltimoUsoEm).HasColumnName("ultimo_uso_em");
        builder.Property(d => d.Rotulo).HasColumnName("rotulo").HasMaxLength(256);
        builder.Property(d => d.RevogadoEm).HasColumnName("revogado_em");

        builder.HasIndex(d => d.TokenHash).IsUnique().HasDatabaseName("ix_trusted_devices_token_hash");
    }
}
