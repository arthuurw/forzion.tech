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

        builder.OwnsOne(t => t.DadosFiscais, df =>
        {
            df.Property(d => d.TipoDocumento).HasConversion<string>().HasMaxLength(10);
            df.Property(d => d.Documento).HasMaxLength(14);
            df.Property(d => d.RazaoSocial).HasMaxLength(150);
            df.Property(d => d.InscricaoMunicipal).HasMaxLength(30);

            df.OwnsOne(d => d.Endereco, e =>
            {
                e.Property(x => x.Logradouro).HasMaxLength(200);
                e.Property(x => x.Numero).HasMaxLength(20);
                e.Property(x => x.Complemento).HasMaxLength(100);
                e.Property(x => x.Bairro).HasMaxLength(100);
                e.Property(x => x.CodigoMunicipioIbge).HasMaxLength(7);
                e.Property(x => x.Uf).HasMaxLength(2);
                e.Property(x => x.Cep).HasMaxLength(8);
            });
            df.Navigation(d => d.Endereco).IsRequired();
        });
        builder.Navigation(t => t.DadosFiscais).IsRequired(false);
    }
}
