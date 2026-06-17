using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class PacoteConfiguration : IEntityTypeConfiguration<Pacote>
{
    public void Configure(EntityTypeBuilder<Pacote> builder)
    {
        builder.ToTable("pacotes", t =>
            t.HasCheckConstraint("ck_pacotes_preco_nao_negativo", "\"preco\" >= 0"));
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TreinadorId).IsRequired();

        builder.HasOne<Treinador>()
            .WithMany()
            .HasForeignKey(p => p.TreinadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.TreinadorId);

        builder.Property(p => p.Nome)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Descricao)
            .HasMaxLength(500);

        builder.Property(p => p.Preco)
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(p => p.IsAtivo).IsRequired();

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);
    }
}
