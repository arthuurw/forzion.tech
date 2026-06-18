using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class NotaFiscalConfiguration : IEntityTypeConfiguration<NotaFiscal>
{
    public void Configure(EntityTypeBuilder<NotaFiscal> builder)
    {
        builder.ToTable("notas_fiscais", t =>
            t.HasCheckConstraint("ck_notas_fiscais_valor_nao_negativo", "\"valor\" >= 0"));
        builder.HasKey(n => n.Id);

        builder.Property(n => n.TreinadorId).IsRequired();
        builder.HasOne<Treinador>()
            .WithMany()
            .HasForeignKey(n => n.TreinadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(n => n.Tipo)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(n => n.PagamentoTreinadorId);
        builder.HasOne<PagamentoTreinador>()
            .WithMany()
            .HasForeignKey(n => n.PagamentoTreinadorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(n => n.CompetenciaInicio);
        builder.Property(n => n.CompetenciaFim);

        builder.Property(n => n.Valor)
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(n => n.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(n => n.ChaveAcesso).HasMaxLength(100);
        builder.Property(n => n.NumeroNfse).HasMaxLength(100);
        builder.Property(n => n.NumeroDps).HasMaxLength(100);
        builder.Property(n => n.DataEmissao);
        builder.Property(n => n.DanfseRef).HasMaxLength(500);
        builder.Property(n => n.CodigoErro).HasMaxLength(100);
        builder.Property(n => n.MotivoErro).HasMaxLength(2000);
        builder.Property(n => n.CancelamentoPendentePreEmissao).IsRequired().HasDefaultValue(false);
        builder.Property(n => n.MotivoCancelamentoPendente).HasMaxLength(500);

        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.UpdatedAt);

        builder.HasIndex(n => n.TreinadorId)
            .HasDatabaseName("ix_notas_fiscais_treinador_id");

        builder.HasIndex(n => n.PagamentoTreinadorId)
            .IsUnique()
            .HasFilter("pagamento_treinador_id IS NOT NULL")
            .HasDatabaseName("ix_notas_fiscais_pagamento_treinador_id_unique");

        builder.HasIndex(n => new { n.TreinadorId, n.Tipo, n.CompetenciaInicio })
            .IsUnique()
            .HasFilter("competencia_inicio IS NOT NULL")
            .HasDatabaseName("ix_notas_fiscais_treinador_tipo_competencia_unique");
    }
}
