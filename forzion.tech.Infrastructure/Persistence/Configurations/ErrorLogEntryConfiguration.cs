using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class ErrorLogEntryConfiguration : IEntityTypeConfiguration<ErrorLogEntry>
{
    public void Configure(EntityTypeBuilder<ErrorLogEntry> builder)
    {
        builder.ToTable("error_logs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.OcorridoEm).IsRequired();

        builder.Property(e => e.Nivel)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Origem)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Mensagem)
            .HasMaxLength(ErrorLogEntry.MensagemMaxLength)
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => e.OcorridoEm).HasDatabaseName("ix_error_logs_ocorrido_em");
    }
}
