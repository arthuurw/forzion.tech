using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class AssinaturaTreinadorConfiguration : IEntityTypeConfiguration<AssinaturaTreinador>
{
    public void Configure(EntityTypeBuilder<AssinaturaTreinador> builder)
    {
        builder.ToTable("assinaturas_treinador", t =>
            t.HasCheckConstraint("ck_assinaturas_treinador_valor_nao_negativo", "\"valor\" >= 0"));
        builder.HasKey(a => a.Id);

        builder.Property(a => a.TreinadorId).IsRequired();
        builder.HasOne<Treinador>()
            .WithMany()
            .HasForeignKey(a => a.TreinadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.PlanoPlataformaId).IsRequired();
        builder.HasOne<PlanoPlataforma>()
            .WithMany()
            .HasForeignKey(a => a.PlanoPlataformaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.PlanoPlataformaIdAgendado);
        builder.HasOne<PlanoPlataforma>()
            .WithMany()
            .HasForeignKey(a => a.PlanoPlataformaIdAgendado)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(a => a.Valor)
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.DataInicio).IsRequired();
        builder.Property(a => a.DataProximaCobranca).IsRequired();
        builder.Property(a => a.DataCancelamento);

        builder.Property(a => a.TentativasFalhasConsecutivas)
            .HasColumnName("tentativas_falhas_consecutivas")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt);

        builder.HasIndex(a => a.TreinadorId);
        builder.HasIndex(a => new { a.Status, a.DataProximaCobranca });
    }
}
