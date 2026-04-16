using forzion.tech.Domain.Entities;
using forzion.tech.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class SystemUserConfiguration : IEntityTypeConfiguration<SystemUser>
{
    public void Configure(EntityTypeBuilder<SystemUser> builder)
    {
        builder.ToTable("system_users");
        builder.HasKey(su => su.Id);

        builder.Property(su => su.SupabaseId)
            .IsRequired()
            .HasColumnName("supabase_id");

        builder.HasIndex(su => su.SupabaseId)
            .IsUnique()
            .HasDatabaseName("ix_system_users_supabase_id");

        builder.Property(su => su.Email)
            .HasConversion(e => e.Value, v => Email.Reconstituir(v))
            .HasMaxLength(256)
            .IsRequired()
            .HasColumnName("email");

        builder.Property(su => su.Nome)
            .HasMaxLength(100)
            .IsRequired()
            .HasColumnName("nome");

        builder.Property(su => su.Role)
            .HasConversion<string>()
            .IsRequired()
            .HasColumnName("role");

        builder.Property(su => su.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasColumnName("status");

        builder.Property(su => su.CreatedAt).IsRequired().HasColumnName("created_at");
        builder.Property(su => su.UpdatedAt).HasColumnName("updated_at");
    }
}
