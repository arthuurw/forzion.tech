using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class AssinaturaConfiguration : IEntityTypeConfiguration<Assinatura>
{
    public void Configure(EntityTypeBuilder<Assinatura> builder)
    {
        builder.ToTable("assinaturas");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.VinculoId).IsRequired();
        builder.HasOne<VinculoTreinadorAluno>()
            .WithMany()
            .HasForeignKey(a => a.VinculoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.PacoteAlunoId).IsRequired();
        builder.HasOne<PacoteAluno>()
            .WithMany()
            .HasForeignKey(a => a.PacoteAlunoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.TreinadorId).IsRequired();
        builder.HasOne<Treinador>()
            .WithMany()
            .HasForeignKey(a => a.TreinadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.AlunoId).IsRequired();
        builder.HasOne<Aluno>()
            .WithMany()
            .HasForeignKey(a => a.AlunoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Valor)
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.DataInicio).IsRequired();
        builder.Property(a => a.DataProximaCobranca).IsRequired();
        builder.Property(a => a.DataCancelamento);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt);

        builder.HasIndex(a => a.AlunoId);
        builder.HasIndex(a => a.TreinadorId);
        builder.HasIndex(a => new { a.Status, a.DataProximaCobranca });
        builder.HasIndex(a => a.VinculoId).IsUnique();
    }
}
