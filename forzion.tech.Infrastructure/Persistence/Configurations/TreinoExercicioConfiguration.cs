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

        builder.Property(te => te.Ordem).IsRequired();
        builder.Property(te => te.Observacao).HasMaxLength(500);

        // TreinoId FK configurado via TreinoConfiguration.HasMany
        builder.HasOne<Exercicio>()
               .WithMany()
               .HasForeignKey(te => te.ExercicioId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(te => te.Series)
               .WithOne()
               .HasForeignKey(s => s.TreinoExercicioId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(te => te.Series)
               .HasField("_series")
               .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
