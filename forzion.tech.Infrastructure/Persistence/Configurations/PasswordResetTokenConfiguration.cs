using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.ContaId).HasColumnName("conta_id");
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.UsedAt).HasColumnName("used_at");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(t => t.ContaId)
            .IsUnique()
            .HasFilter("used_at IS NULL")
            .HasDatabaseName("ux_password_reset_tokens_conta_id_pendente");
        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ix_password_reset_tokens_token_hash");
    }
}
