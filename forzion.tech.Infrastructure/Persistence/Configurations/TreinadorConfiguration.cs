using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class TreinadorConfiguration : IEntityTypeConfiguration<Treinador>
{
    public void Configure(EntityTypeBuilder<Treinador> builder)
    {
        builder.ToTable("treinadores");
        builder.HasKey(t => t.Id);

        // Concorrência otimista via system column xmin: impede dois switches simultâneos de modo_pagamento burlarem o cooldown.
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

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

        builder.Property(t => t.PlanoPlataformaId);

        builder.HasOne<PlanoPlataforma>()
            .WithMany()
            .HasForeignKey(t => t.PlanoPlataformaId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(t => t.ModoPagamentoAluno)
            .HasConversion<string>()
            .HasDefaultValue(ModoPagamentoAluno.Plataforma)
            .IsRequired();

        builder.Property(t => t.ModoPagamentoAlunoAlteradoEm);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .IsRequired();
        builder.HasIndex(t => t.Status);

        builder.Property(t => t.AprovadoPorId);
        builder.Property(t => t.AprovadoEm);

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);

        builder.Property(t => t.Anonimizado).HasDefaultValue(false);
    }
}
