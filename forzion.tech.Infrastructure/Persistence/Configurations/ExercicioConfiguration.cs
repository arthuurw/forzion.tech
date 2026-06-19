using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class ExercicioConfiguration : IEntityTypeConfiguration<Exercicio>
{
    public void Configure(EntityTypeBuilder<Exercicio> builder)
    {
        builder.ToTable("exercicios");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TreinadorId);

        builder.HasOne<Treinador>()
            .WithMany()
            .HasForeignKey(e => e.TreinadorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(e => e.Nome)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.GrupoMuscularId).IsRequired();

        builder.HasOne<GrupoMuscular>()
            .WithMany()
            .HasForeignKey(e => e.GrupoMuscularId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.GrupoMuscularId);

        builder.Property(e => e.Descricao)
            .HasMaxLength(500);

        builder.Property(e => e.ComoExecutar)
            .HasMaxLength(2000);

        builder.Property(e => e.VideoId)
            .HasMaxLength(16);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        builder.HasIndex(e => e.TreinadorId);
    }
}
