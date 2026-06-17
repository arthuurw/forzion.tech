using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class MfaChallengeConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> builder)
    {
        builder.ToTable("mfa_challenges");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.ContaId).HasColumnName("conta_id");
        builder.Property(c => c.CodigoHash).HasColumnName("codigo_hash").HasMaxLength(64);
        builder.Property(c => c.Proposito).HasColumnName("proposito").HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.ExpiraEm).HasColumnName("expira_em");
        builder.Property(c => c.UsadoEm).HasColumnName("usado_em");
        builder.Property(c => c.Tentativas).HasColumnName("tentativas");
        builder.Property(c => c.CriadoEm).HasColumnName("criado_em");

        builder.HasIndex(c => new { c.ContaId, c.Proposito }).HasDatabaseName("ix_mfa_challenges_conta_id_proposito");
    }
}
