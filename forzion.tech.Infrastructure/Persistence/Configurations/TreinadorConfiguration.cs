using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class TreinadorConfiguration : IEntityTypeConfiguration<Treinador>
{
    public void Configure(EntityTypeBuilder<Treinador> builder)
    {
        builder.ToTable("treinadores");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.ContaId).IsRequired();

        builder.HasOne<Conta>()
            .WithMany()
            .HasForeignKey(t => t.ContaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.ContaId).IsUnique();

        builder.Property(t => t.Nome)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Telefone).HasMaxLength(20);

        builder.Property(t => t.PlanoTreinadorId);

        builder.HasOne<PlanoTreinador>()
            .WithMany()
            .HasForeignKey(t => t.PlanoTreinadorId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .IsRequired();
        builder.HasIndex(t => t.Status);

        builder.Property(t => t.AprovadoPorId);
        builder.Property(t => t.AprovadoEm);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);
    }
}
