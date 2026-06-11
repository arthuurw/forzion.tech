using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class OutboxEfeitoConfiguration : IEntityTypeConfiguration<OutboxEfeito>
{
    public void Configure(EntityTypeBuilder<OutboxEfeito> builder)
    {
        builder.ToTable("outbox_efeitos");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Tipo).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(e => e.Tentativas).IsRequired();
        builder.Property(e => e.ProximaTentativa).IsRequired();
        builder.Property(e => e.UltimoErro).HasColumnType("text");
        builder.Property(e => e.ChaveIdempotencia).HasMaxLength(300).IsRequired();
        builder.Property(e => e.CriadoEm).IsRequired();
        builder.Property(e => e.ProcessadoEm);

        // Único: bloqueia enfileiramento duplicado do mesmo efeito de origem (idempotência §OBX-05).
        builder.HasIndex(e => e.ChaveIdempotencia)
            .IsUnique()
            .HasDatabaseName("ix_outbox_efeitos_chave_idempotencia_unique");

        // Scan do worker: pendentes/falhos elegíveis por proxima_tentativa.
        builder.HasIndex(e => new { e.Status, e.ProximaTentativa })
            .HasDatabaseName("ix_outbox_efeitos_status_proxima_tentativa");
    }
}
