using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class PagamentoTreinadorConfiguration : IEntityTypeConfiguration<PagamentoTreinador>
{
    public void Configure(EntityTypeBuilder<PagamentoTreinador> builder)
    {
        builder.ToTable("pagamentos_treinador", t =>
            t.HasCheckConstraint("ck_pagamentos_treinador_valor_nao_negativo", "\"valor\" >= 0"));
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TreinadorId).IsRequired();
        builder.HasOne<Treinador>()
            .WithMany()
            .HasForeignKey(p => p.TreinadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.AssinaturaTreinadorId).IsRequired();
        builder.HasOne<AssinaturaTreinador>()
            .WithMany()
            .HasForeignKey(p => p.AssinaturaTreinadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.Valor)
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.MetodoPagamento)
            .HasConversion<string>()
            .HasDefaultValue(MetodoPagamento.Pix)
            .IsRequired();

        builder.Property(p => p.Finalidade)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.PlanoAlvoId);
        builder.HasOne<PlanoPlataforma>()
            .WithMany()
            .HasForeignKey(p => p.PlanoAlvoId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(p => p.ClientSecret).HasMaxLength(500);

        builder.Property(p => p.StripePaymentIntentId).HasMaxLength(200);
        builder.HasIndex(p => p.StripePaymentIntentId)
            .IsUnique()
            .HasDatabaseName("ix_pagamentos_treinador_stripe_payment_intent_id");

        builder.Property(p => p.PixQrCode).HasColumnType("text");
        builder.Property(p => p.PixQrCodeUrl).HasMaxLength(500);
        builder.Property(p => p.PixExpiracao);
        builder.Property(p => p.DataPagamento);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        builder.HasIndex(p => p.AssinaturaTreinadorId)
            .HasDatabaseName("ix_pagamentos_treinador_assinatura_id");

        builder.HasIndex(p => new { p.AssinaturaTreinadorId, p.Status })
            .HasDatabaseName("ix_pagamentos_treinador_assinatura_id_status");

        // Previne cobrança dupla concorrente: no máx 1 pagamento Pendente por assinatura.
        builder.HasIndex(p => p.AssinaturaTreinadorId)
            .HasFilter("status = 'Pendente'")
            .IsUnique()
            .HasDatabaseName("ix_pagamentos_treinador_assinatura_id_pendente_unique");
    }
}
