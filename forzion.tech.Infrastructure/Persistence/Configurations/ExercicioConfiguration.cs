using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

// TODO: refatorar — trocar TenantId por TreinadorId (nullable para exercícios globais) ao concluir Fase 2
public class ExercicioConfiguration : IEntityTypeConfiguration<Exercicio>
{
    public void Configure(EntityTypeBuilder<Exercicio> builder)
    {
        builder.ToTable("exercicios");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Nome)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.GrupoMuscular)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(e => e.Descricao)
            .HasMaxLength(500);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);
    }
}
