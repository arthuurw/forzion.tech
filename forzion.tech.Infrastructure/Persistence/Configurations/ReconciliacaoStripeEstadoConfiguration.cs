using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class ReconciliacaoStripeEstadoConfiguration : IEntityTypeConfiguration<ReconciliacaoStripeEstado>
{
    public void Configure(EntityTypeBuilder<ReconciliacaoStripeEstado> builder)
    {
        builder.ToTable("reconciliacao_stripe_estado");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UltimoEventoReconciliadoUtc).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);
    }
}
