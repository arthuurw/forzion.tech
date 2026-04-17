using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class LogAprovacaoConfiguration : IEntityTypeConfiguration<LogAprovacao>
{
    public void Configure(EntityTypeBuilder<LogAprovacao> builder)
    {
        builder.ToTable("logs_aprovacao");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.TipoAcao)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(l => l.RealizadoPorId).IsRequired();
        builder.Property(l => l.EntidadeId).IsRequired();

        builder.Property(l => l.EntidadeTipo)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.Observacao)
            .HasMaxLength(500);

        builder.Property(l => l.CreatedAt).IsRequired();

        builder.HasIndex(l => l.EntidadeId);
        builder.HasIndex(l => l.RealizadoPorId);
    }
}
