using forzion.tech.Domain.Entities;
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

        builder.HasOne<Conta>()
            .WithMany()
            .HasForeignKey(a => a.ContaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Nome)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Email)
            .HasMaxLength(256);

        builder.Property(a => a.Telefone)
            .HasMaxLength(20);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt);
    }
}
