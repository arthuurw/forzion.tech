using forzion.tech.Domain.Constants;
using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class PlanoConfiguration : IEntityTypeConfiguration<Plano>
{
    public void Configure(EntityTypeBuilder<Plano> builder)
    {
        builder.ToTable("planos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nome).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Preco).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(p => p.LimiteAlunos).IsRequired();
        builder.Property(p => p.IsFree).IsRequired();

        builder.HasData(
            Plano.CriarComId(PlanoIds.FreeId, "Free", 0m, 5, isFree: true),
            Plano.CriarComId(PlanoIds.ProId, "Pro", 49.90m, int.MaxValue)
        );
    }
}
