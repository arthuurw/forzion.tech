using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class ExecucaoTreinoConfiguration : IEntityTypeConfiguration<ExecucaoTreino>
{
    public void Configure(EntityTypeBuilder<ExecucaoTreino> builder)
    {
        builder.ToTable("execucoes_treino");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DataExecucao).IsRequired();
        builder.Property(e => e.Observacao).HasMaxLength(500);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(64);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => e.TreinoId);

        builder.HasIndex(e => new { e.AlunoId, e.DataExecucao })
               .IsDescending(false, true)
               .HasDatabaseName("ix_execucoes_treino_aluno_id_data_execucao");

        builder.HasIndex(e => new { e.AlunoId, e.IdempotencyKey })
               .IsUnique()
               .HasFilter("idempotency_key IS NOT NULL")
               .HasDatabaseName("ix_execucoes_treino_aluno_id_idempotency_key_unique");

        builder.HasOne<Treino>()
               .WithMany()
               .HasForeignKey(e => e.TreinoId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Aluno>()
               .WithMany()
               .HasForeignKey(e => e.AlunoId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Exercicios)
               .WithOne()
               .HasForeignKey(ee => ee.ExecucaoTreinoId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Exercicios)
               .HasField("_exercicios")
               .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
