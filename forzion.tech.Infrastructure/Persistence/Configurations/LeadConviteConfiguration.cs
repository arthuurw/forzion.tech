using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class LeadConviteConfiguration : IEntityTypeConfiguration<LeadConvite>
{
    public void Configure(EntityTypeBuilder<LeadConvite> builder)
    {
        builder.ToTable("lead_convites");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.LeadId).IsRequired();
        builder.HasOne<Lead>()
            .WithMany()
            .HasForeignKey(c => c.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(c => c.TreinadorId).IsRequired();

        builder.Property(c => c.TokenHash).IsRequired();
        builder.HasIndex(c => c.TokenHash).IsUnique();

        builder.Property(c => c.ExpiraEm).IsRequired();
        builder.HasIndex(c => c.ExpiraEm);

        builder.Property(c => c.UsadoEm);
        builder.Property(c => c.InvalidadoEm);
        builder.Property(c => c.CreatedAt).IsRequired();
    }
}
