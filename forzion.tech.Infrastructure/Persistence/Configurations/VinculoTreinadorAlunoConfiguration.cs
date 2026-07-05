using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class VinculoTreinadorAlunoConfiguration : IEntityTypeConfiguration<VinculoTreinadorAluno>
{
    public void Configure(EntityTypeBuilder<VinculoTreinadorAluno> builder)
    {
        builder.ToTable("vinculos_treinador_aluno");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.TreinadorId).IsRequired();
        builder.Property(v => v.AlunoId).IsRequired();

        builder.HasOne<Treinador>()
            .WithMany()
            .HasForeignKey(v => v.TreinadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Aluno>()
            .WithMany()
            .HasForeignKey(v => v.AlunoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(v => v.PacoteId);

        builder.HasOne<Pacote>()
            .WithMany()
            .HasForeignKey(v => v.PacoteId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(v => v.AprovadoPorId);
        builder.Property(v => v.AprovadoEm);
        builder.Property(v => v.DataInicio);
        builder.Property(v => v.DataFim);

        builder.Property(v => v.CreatedAt).IsRequired();

        builder.Property(v => v.PreservarNoLimite).HasDefaultValue(false);

        builder.HasIndex(v => new { v.TreinadorId, v.AlunoId });
        builder.HasIndex(v => v.AlunoId);
        builder.HasIndex(v => new { v.TreinadorId, v.Status });
    }
}
