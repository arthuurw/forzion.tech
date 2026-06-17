using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class ContaMfaConfiguration : IEntityTypeConfiguration<ContaMfa>
{
    public void Configure(EntityTypeBuilder<ContaMfa> builder)
    {
        builder.ToTable("conta_mfa");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.ContaId).HasColumnName("conta_id");
        builder.Property(m => m.TotpSecretCifrado).HasColumnName("totp_secret_cifrado");
        builder.Property(m => m.Habilitado).HasColumnName("habilitado");
        builder.Property(m => m.UltimoTimeStep).HasColumnName("ultimo_time_step");
        builder.Property(m => m.CriadoEm).HasColumnName("criado_em");
        builder.Property(m => m.ConfirmadoEm).HasColumnName("confirmado_em");
        builder.Property(m => m.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasIndex(m => m.ContaId).IsUnique().HasDatabaseName("ix_conta_mfa_conta_id");
    }
}
