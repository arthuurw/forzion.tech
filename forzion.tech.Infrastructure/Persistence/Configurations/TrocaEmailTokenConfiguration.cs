using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class TrocaEmailTokenConfiguration : IEntityTypeConfiguration<TrocaEmailToken>
{
    public void Configure(EntityTypeBuilder<TrocaEmailToken> builder)
    {
        builder.ToTable("troca_email_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.ContaId).HasColumnName("conta_id");
        builder.Property(t => t.NovoEmail).HasColumnName("novo_email").HasMaxLength(256);
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
        builder.Property(t => t.ExpiraEm).HasColumnName("expira_em");
        builder.Property(t => t.UsadoEm).HasColumnName("usado_em");
        builder.Property(t => t.CriadoEm).HasColumnName("criado_em");

        builder.HasIndex(t => t.ContaId).HasDatabaseName("ix_troca_email_tokens_conta_id");
        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ix_troca_email_tokens_token_hash");
    }
}
