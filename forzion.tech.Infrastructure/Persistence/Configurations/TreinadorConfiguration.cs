using System.Text.Json;
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

        builder.Property(t => t.PlanoCortesiaId);

        builder.HasOne<PlanoPlataforma>()
            .WithMany()
            .HasForeignKey(t => t.PlanoCortesiaId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(t => t.AlunosAcimaDoCapDesde);

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

        builder.OwnsOne(t => t.PerfilPublico, pp =>
        {
            pp.Property(p => p.NomeFantasia).HasMaxLength(200);
            pp.Property(p => p.IsPublicado).IsRequired().HasDefaultValue(false);
            pp.Property(p => p.UpdatedAt);

            pp.Property(p => p.Politicas)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null));

            pp.OwnsOne(p => p.Endereco, e =>
            {
                e.Property(x => x.Rua).HasMaxLength(200);
                e.Property(x => x.Numero).HasMaxLength(20);
                e.Property(x => x.Complemento).HasMaxLength(100);
                e.Property(x => x.Bairro).HasMaxLength(100);
                e.Property(x => x.Cidade).HasMaxLength(100);
                e.Property(x => x.Estado).HasMaxLength(2);
                e.Property(x => x.Cep).HasMaxLength(8);
            });
            pp.Navigation(p => p.Endereco).IsRequired(false);

            pp.OwnsMany(p => p.HorariosFuncionamento, h =>
            {
                h.ToTable("horarios_funcionamento");
                h.WithOwner().HasForeignKey("treinador_id");
                h.HasKey(x => x.Id);
                // Guid é gerado no domínio (HorarioFuncionamento.Criar), nunca pelo banco. Sem isto, EF
                // assume ValueGeneratedOnAdd por convenção e trata cada item novo descoberto dentro da
                // coleção owned como Modified (gera UPDATE numa linha que não existe ainda -> 0 rows
                // affected -> DbUpdateConcurrencyException), em vez de Added (INSERT).
                h.Property(x => x.Id).ValueGeneratedNever();
                h.Property(x => x.DiaSemana).IsRequired();
                h.Property(x => x.AbreAs).IsRequired();
                h.Property(x => x.FechaAs).IsRequired();
            });
        });
        builder.Navigation(t => t.PerfilPublico).IsRequired();

        // Sem esta configuracao explicita, a descoberta automatica de owned type do EF
        // quebra o build do modelo assim que Treinador ganha uma propriedade record sem
        // constructor binding resolvivel (PoliticaAgenda). Nomes de coluna adiantam o
        // prefixo "agenda_" definido no design da fatia 3 (T11 completa o restante).
        builder.OwnsOne(t => t.PoliticaAgenda, pa =>
        {
            pa.Property(p => p.AntecedenciaMinimaHoras).HasColumnName("agenda_antecedencia_minima_horas").IsRequired();
            pa.Property(p => p.HorizonteDias).HasColumnName("agenda_horizonte_dias").IsRequired();
        });
        builder.Navigation(t => t.PoliticaAgenda).IsRequired();
    }
}
