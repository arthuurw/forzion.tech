using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("pagamentos", t =>
            t.HasCheckConstraint("ck_pagamentos_valor_nao_negativo", "\"valor\" >= 0"));
        builder.HasKey(p => p.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(p => p.AssinaturaAlunoId).IsRequired();
        builder.HasOne<AssinaturaAluno>()
            .WithMany()
            .HasForeignKey(p => p.AssinaturaAlunoId)
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

        builder.Property(p => p.ClientSecret).HasMaxLength(500);

        builder.Property(p => p.StripePaymentIntentId).HasMaxLength(200);
        // Único: PaymentIntentId identifica unicamente um pagamento no Stripe (NULLs permitidos — não conflitam em UNIQUE)
        builder.HasIndex(p => p.StripePaymentIntentId)
            .IsUnique()
            .HasDatabaseName("ix_pagamentos_stripe_payment_intent_id");

        builder.Property(p => p.PixQrCode).HasColumnType("text");
        builder.Property(p => p.PixQrCodeUrl).HasMaxLength(500);
        builder.Property(p => p.PixExpiracao);
        builder.Property(p => p.DataPagamento);

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        builder.HasIndex(p => p.AssinaturaAlunoId)
            .HasDatabaseName("ix_pagamentos_assinatura_aluno_id");

        builder.HasIndex(p => new { p.AssinaturaAlunoId, p.Status })
            .HasDatabaseName("ix_pagamentos_assinatura_aluno_id_status");

        // Índice parcial único: previne race condition de cobrança dupla — no máx 1 pagamento Pendente por assinatura
        builder.HasIndex(p => p.AssinaturaAlunoId)
            .HasFilter("status = 'Pendente'")
            .IsUnique()
            .HasDatabaseName("ix_pagamentos_assinatura_aluno_id_pendente_unique");
    }
}
