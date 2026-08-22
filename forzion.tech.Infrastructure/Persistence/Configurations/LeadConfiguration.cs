using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("leads");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.TreinadorId).IsRequired();
        builder.HasOne<Treinador>()
            .WithMany()
            .HasForeignKey(l => l.TreinadorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.Nome).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Interesse).HasMaxLength(1000);

        builder.OwnsOne(l => l.Contato, c =>
        {
            c.Property(x => x.Tipo).HasColumnName("contato_tipo").HasConversion<string>().IsRequired();
            c.Property(x => x.Valor).HasColumnName("contato_valor").HasMaxLength(320).IsRequired();
            c.HasIndex(x => x.Valor);
        });
        builder.Navigation(l => l.Contato).IsRequired();

        builder.OwnsOne(l => l.Consentimento, c =>
        {
            c.Property(x => x.Finalidade).HasColumnName("consentimento_finalidade").HasMaxLength(500).IsRequired();
            c.Property(x => x.ConcedidoEm).HasColumnName("consentimento_concedido_em").IsRequired();
            c.Property(x => x.RegistradoEm).HasColumnName("consentimento_registrado_em").IsRequired();
        });
        builder.Navigation(l => l.Consentimento).IsRequired();

        builder.OwnsOne(l => l.Origem, o =>
        {
            o.Property(x => x.UserAgent).HasColumnName("origem_user_agent").HasMaxLength(500);
            o.Property(x => x.Assistente).HasColumnName("origem_assistente").HasMaxLength(100);
        });
        builder.Navigation(l => l.Origem).IsRequired(false);

        builder.Property(l => l.Source).HasConversion<string>().IsRequired();
        builder.Property(l => l.Status).HasConversion<string>().IsRequired();
        builder.Property(l => l.MotivoDescarte).HasConversion<string>();

        builder.Property(l => l.AlunoId);
        builder.HasOne<Aluno>()
            .WithMany()
            .HasForeignKey(l => l.AlunoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(l => l.IdempotencyKey).HasMaxLength(200);
        builder.Property(l => l.ArgumentosHash).HasMaxLength(64);
        builder.Property(l => l.UltimoToqueEm).IsRequired();
        builder.Property(l => l.Anonimizado).IsRequired().HasDefaultValue(false);
        builder.Property(l => l.CreatedAt).IsRequired();
        builder.Property(l => l.UpdatedAt);

        builder.HasIndex(l => new { l.TreinadorId, l.IdempotencyKey })
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ix_leads_treinador_id_idempotency_key_unique");

        builder.HasIndex(l => new { l.TreinadorId, l.Status, l.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_leads_treinador_id_status_created_at");

        builder.HasIndex(l => new { l.TreinadorId, l.CreatedAt })
            .HasDatabaseName("ix_leads_treinador_id_created_at");

        builder.HasIndex(l => l.UltimoToqueEm)
            .HasFilter("anonimizado = false")
            .HasDatabaseName("ix_leads_ultimo_toque_em");

        builder.OwnsMany(l => l.Interacoes, i =>
        {
            i.ToTable("lead_interacoes");
            i.WithOwner().HasForeignKey("lead_id");
            i.HasKey(x => x.Id);
            // Guid é gerado no domínio (LeadInteracao.Criar), nunca pelo banco. Sem isto, EF
            // trata cada item novo descoberto dentro da coleção owned recarregada como Modified
            // (UPDATE numa linha inexistente) em vez de Added (INSERT) — precedente AD-018.
            i.Property(x => x.Id).ValueGeneratedNever();
            i.Property(x => x.OcorridaEm).IsRequired();
            i.Property(x => x.StatusAnterior).HasConversion<string>();
            i.Property(x => x.StatusNovo).HasConversion<string>();
            i.Property(x => x.Observacao).HasMaxLength(1000);
            i.Property(x => x.RealizadoPorId);
        });
    }
}
