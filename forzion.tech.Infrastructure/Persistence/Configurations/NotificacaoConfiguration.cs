using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class NotificacaoConfiguration : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("notificacoes");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Tipo).HasConversion<string>().IsRequired();
        builder.Property(n => n.Titulo).HasMaxLength(120).IsRequired();
        builder.Property(n => n.Corpo).HasMaxLength(500).IsRequired();
        builder.Property(n => n.Lida).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();

        builder.HasOne<Conta>()
               .WithMany()
               .HasForeignKey(n => n.DestinatarioContaId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.DestinatarioContaId, n.Lida, n.CreatedAt })
               .HasDatabaseName("ix_notificacoes_conta_lida_created");

        builder.HasIndex(n => new { n.DestinatarioContaId, n.Tipo, n.DiaReferencia })
               .IsUnique()
               .HasFilter("dia_referencia IS NOT NULL")
               .HasDatabaseName("ix_notificacoes_dedup");
    }
}
