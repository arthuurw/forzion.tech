using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class RefreshTokenFamilyConfiguration : IEntityTypeConfiguration<RefreshTokenFamily>
{
    public void Configure(EntityTypeBuilder<RefreshTokenFamily> builder)
    {
        builder.ToTable("refresh_token_families");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.ContaId).HasColumnName("conta_id");
        builder.Property(f => f.CriadaEm).HasColumnName("criada_em");
        builder.Property(f => f.AbsolutoExpiraEm).HasColumnName("absoluto_expira_em");
        builder.Property(f => f.RevogadaEm).HasColumnName("revogada_em");
        builder.Property(f => f.MotivoRevogacao).HasColumnName("motivo_revogacao").HasConversion<string>().HasMaxLength(32);
        builder.Property(f => f.Rotulo).HasColumnName("rotulo").HasMaxLength(256);

        builder.HasIndex(f => f.ContaId).HasDatabaseName("ix_refresh_token_families_conta_id");
        // GC varre famílias revogadas/expiradas; índice em revogada_em acelera a purga.
        builder.HasIndex(f => f.RevogadaEm).HasDatabaseName("ix_refresh_token_families_revogada_em");
    }
}
