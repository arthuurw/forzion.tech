using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.FamiliaId).HasColumnName("familia_id");
        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
        builder.Property(t => t.CriadoEm).HasColumnName("criado_em");
        builder.Property(t => t.ExpiraEm).HasColumnName("expira_em");
        builder.Property(t => t.UsadoEm).HasColumnName("usado_em");
        builder.Property(t => t.SubstituidoPorId).HasColumnName("substituido_por_id");

        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("ix_refresh_tokens_token_hash");
        builder.HasIndex(t => t.FamiliaId).HasDatabaseName("ix_refresh_tokens_familia_id");

        // Cascade no nível do banco: GC apaga famílias via ExecuteDelete (não dispara cascade
        // do EF), então a FK precisa de ON DELETE CASCADE p/ os tokens irem junto.
        builder.HasOne<RefreshTokenFamily>()
            .WithMany()
            .HasForeignKey(t => t.FamiliaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
