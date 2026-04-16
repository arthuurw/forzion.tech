using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class TreinoAlunoConfiguration : IEntityTypeConfiguration<TreinoAluno>
{
    public void Configure(EntityTypeBuilder<TreinoAluno> builder)
    {
        builder.ToTable("treino_alunos");
        builder.HasKey(ta => ta.Id);

        builder.Property(ta => ta.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(ta => ta.CreatedAt).IsRequired();
        builder.Property(ta => ta.UpdatedAt);

        builder.HasIndex(ta => ta.TreinoId);
        builder.HasIndex(ta => ta.AlunoId);

        builder.HasOne<Treino>()
               .WithMany()
               .HasForeignKey(ta => ta.TreinoId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Aluno>()
               .WithMany()
               .HasForeignKey(ta => ta.AlunoId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
