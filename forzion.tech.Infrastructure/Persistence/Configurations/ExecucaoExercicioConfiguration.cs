using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class ExecucaoExercicioConfiguration : IEntityTypeConfiguration<ExecucaoExercicio>
{
    public void Configure(EntityTypeBuilder<ExecucaoExercicio> builder)
    {
        builder.ToTable("execucoes_exercicio");
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
