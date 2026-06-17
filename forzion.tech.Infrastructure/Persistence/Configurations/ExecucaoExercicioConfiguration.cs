using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class ExecucaoExercicioConfiguration : IEntityTypeConfiguration<ExecucaoExercicio>
{
    public void Configure(EntityTypeBuilder<ExecucaoExercicio> builder)
    {
        builder.ToTable("execucoes_exercicio", t =>
        {
            t.HasCheckConstraint("ck_execucoes_exercicio_series_positivo", "\"series_executadas\" > 0");
            t.HasCheckConstraint("ck_execucoes_exercicio_repeticoes_positivo", "\"repeticoes_executadas\" > 0");
        });
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SeriesExecutadas).IsRequired();
        builder.Property(e => e.RepeticoesExecutadas).IsRequired();
        builder.Property(e => e.CargaExecutada).HasColumnType("numeric(10,2)");
        builder.Property(e => e.Observacao).HasMaxLength(500);

        // ExecucaoTreinoId FK configurado via ExecucaoTreinoConfiguration.HasMany
        builder.HasOne<TreinoExercicio>()
               .WithMany()
               .HasForeignKey(e => e.TreinoExercicioId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
