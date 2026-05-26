using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class HealthReportConfigConfiguration : IEntityTypeConfiguration<HealthReportConfig>
{
    public void Configure(EntityTypeBuilder<HealthReportConfig> builder)
    {
        builder.ToTable("health_report_config");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Ativo).IsRequired();
        builder.Property(c => c.HoraEnvioUtc).IsRequired();
        builder.Property(c => c.Destinatarios).IsRequired();
        builder.Property(c => c.IncluirLiveness).IsRequired();
        builder.Property(c => c.IncluirKpis).IsRequired();
        builder.Property(c => c.IncluirEntregabilidade).IsRequired();
        builder.Property(c => c.IncluirErros).IsRequired();
        builder.Property(c => c.UltimoEnvioEm);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
    }
}
