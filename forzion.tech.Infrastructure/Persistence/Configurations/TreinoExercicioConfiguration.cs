using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class TreinoExercicioConfiguration : IEntityTypeConfiguration<TreinoExercicio>
{
    public void Configure(EntityTypeBuilder<TreinoExercicio> builder)
    {
        builder.ToTable("treino_exercicios");
        builder.HasKey(te => te.Id);

        builder.Property(te => te.Series).IsRequired();
        builder.Property(te => te.Repeticoes).IsRequired();
        builder.Property(te => te.Carga).HasColumnType("numeric(10,2)");
        builder.Property(te => te.Descanso);
        builder.Property(te => te.Ordem).IsRequired();

        // TreinoId FK configurado via TreinoConfiguration.HasMany
        builder.HasOne(te => te.Exercicio)
               .WithMany()
               .HasForeignKey(te => te.ExercicioId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
