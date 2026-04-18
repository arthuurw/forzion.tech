using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class SystemUserConfiguration : IEntityTypeConfiguration<SystemUser>
{
    public void Configure(EntityTypeBuilder<SystemUser> builder)
    {
        builder.ToTable("system_users");
        builder.HasKey(su => su.Id);

        builder.Property(su => su.ContaId)
            .IsRequired()
            .HasColumnName("conta_id");

        builder.HasOne<Conta>()
            .WithMany()
            .HasForeignKey(su => su.ContaId)
            .OnDelete(DeleteBehavior.Restrict);

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
