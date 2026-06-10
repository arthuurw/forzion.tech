using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class EmailDeliveryLogConfiguration : IEntityTypeConfiguration<EmailDeliveryLog>
{
    public void Configure(EntityTypeBuilder<EmailDeliveryLog> builder)
    {
        builder.ToTable("email_delivery_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ResendMessageId).HasColumnName("resend_message_id").HasMaxLength(100);
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(50);
        builder.Property(e => e.RecipientEmail).HasColumnName("recipient_email").HasMaxLength(254);
        builder.Property(e => e.OcorridoEm).HasColumnName("ocorrido_em");
        builder.Property(e => e.Payload).HasColumnName("payload");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        // Único: idempotência sob redelivery concorrente de webhook — o par (mensagem, evento) identifica
        // unicamente uma ocorrência; insert duplicado viola o índice e o handler trata a violação (T8).
        builder.HasIndex(e => new { e.ResendMessageId, e.EventType })
            .IsUnique()
            .HasDatabaseName("ix_email_delivery_logs_resend_message_id_event_type");
        builder.HasIndex(e => e.EventType).HasDatabaseName("ix_email_delivery_logs_event_type");
    }
}
