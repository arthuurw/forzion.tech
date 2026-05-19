using forzion.tech.Domain.Entities;
using forzion.tech.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class AlunoConfiguration : IEntityTypeConfiguration<Aluno>
{
    public void Configure(EntityTypeBuilder<Aluno> builder)
    {
        builder.ToTable("alunos");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ContaId).IsRequired();
        builder.HasIndex(a => a.ContaId);

        builder.HasOne<Conta>()
            .WithMany()
            .HasForeignKey(a => a.ContaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Nome)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Email)
            .HasConversion(
                e => e == null ? null : e.Value,
                v => v == null ? null : Email.FromDatabase(v))
            .HasMaxLength(256);

        builder.Property(a => a.Telefone)
            .HasMaxLength(20);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.DiasDisponiveis);
        builder.Property(a => a.TempoDisponivelMinutos).HasConversion<int>();
        builder.Property(a => a.Finalidade).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.FocoTreino).HasMaxLength(200);
        builder.Property(a => a.NivelCondicionamento).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.LimitacoesFisicas).HasMaxLength(500);
        builder.Property(a => a.Doencas).HasMaxLength(500);
        builder.Property(a => a.ObservacoesAdicionais).HasMaxLength(1000);

        builder.HasIndex(a => a.Status);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt);
    }
}
