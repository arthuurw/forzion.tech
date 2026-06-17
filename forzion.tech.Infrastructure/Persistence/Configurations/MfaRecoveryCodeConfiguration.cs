using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class MfaRecoveryCodeConfiguration : IEntityTypeConfiguration<MfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryCode> builder)
    {
        builder.ToTable("mfa_recovery_codes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.ContaId).HasColumnName("conta_id");
        builder.Property(c => c.CodigoHash).HasColumnName("codigo_hash").HasMaxLength(64);
        builder.Property(c => c.UsadoEm).HasColumnName("usado_em");
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em");

        builder.HasIndex(c => c.ContaId).HasDatabaseName("ix_mfa_recovery_codes_conta_id");
    }
}
