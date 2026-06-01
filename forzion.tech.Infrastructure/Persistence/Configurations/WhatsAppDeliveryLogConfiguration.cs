using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class WhatsAppDeliveryLogConfiguration : IEntityTypeConfiguration<WhatsAppDeliveryLog>
{
    public void Configure(EntityTypeBuilder<WhatsAppDeliveryLog> builder)
    {
        builder.ToTable("whatsapp_delivery_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.MetaMessageId).HasColumnName("meta_message_id").HasMaxLength(100);
        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(50);
        builder.Property(e => e.RecipientPhone).HasColumnName("recipient_phone").HasMaxLength(32);
        builder.Property(e => e.OcorridoEm).HasColumnName("ocorrido_em");
        builder.Property(e => e.Payload).HasColumnName("payload");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => e.MetaMessageId).HasDatabaseName("ix_whatsapp_delivery_logs_meta_message_id");
        builder.HasIndex(e => e.EventType).HasDatabaseName("ix_whatsapp_delivery_logs_event_type");
    }
}
