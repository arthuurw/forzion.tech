using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class SerieConfigConfiguration : IEntityTypeConfiguration<SerieConfig>
{
    public void Configure(EntityTypeBuilder<SerieConfig> builder)
    {
        builder.ToTable("treino_exercicio_series");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TreinoExercicioId).IsRequired();
        builder.Property(s => s.Quantidade).IsRequired();
        builder.Property(s => s.RepeticoesMin).IsRequired();
        builder.Property(s => s.RepeticoesMax);
        builder.Property(s => s.Descricao).HasMaxLength(100);
        builder.Property(s => s.Carga).HasColumnType("numeric(10,2)");
        builder.Property(s => s.Descanso);
        builder.Property(s => s.Ordem).IsRequired();

        builder.HasIndex(s => s.TreinoExercicioId);
    }
}
