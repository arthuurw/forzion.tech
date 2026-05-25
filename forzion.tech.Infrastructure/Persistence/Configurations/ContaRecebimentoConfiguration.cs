using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class ContaRecebimentoConfiguration : IEntityTypeConfiguration<ContaRecebimento>
{
    public void Configure(EntityTypeBuilder<ContaRecebimento> builder)
    {
        builder.ToTable("conta_recebimento");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TreinadorId).IsRequired();
        builder.HasIndex(c => c.TreinadorId).IsUnique();

        builder.HasOne<Treinador>()
            .WithMany()
            .HasForeignKey(c => c.TreinadorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(c => c.StripeConnectAccountId).HasMaxLength(100);
        builder.HasIndex(c => c.StripeConnectAccountId);

        builder.Property(c => c.OnboardingCompleto).HasDefaultValue(false);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
    }
}
