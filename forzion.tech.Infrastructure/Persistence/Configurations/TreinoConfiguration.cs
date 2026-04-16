using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

// TODO: refatorar — remover TenantId ao concluir Fase 2 do domínio; adicionar FK para Treinador
public class TreinoConfiguration : IEntityTypeConfiguration<Treino>
{
    public void Configure(EntityTypeBuilder<Treino> builder)
    {
        builder.ToTable("treinos");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Nome)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Objetivo)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        builder.HasMany(t => t.Exercicios)
               .WithOne()
               .HasForeignKey(te => te.TreinoId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.Exercicios)
               .HasField("_exercicios")
               .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
