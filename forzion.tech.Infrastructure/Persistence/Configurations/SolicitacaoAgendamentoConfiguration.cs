using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class SolicitacaoAgendamentoConfiguration : IEntityTypeConfiguration<SolicitacaoAgendamento>
{
    public void Configure(EntityTypeBuilder<SolicitacaoAgendamento> builder)
    {
        builder.ToTable("solicitacoes_agendamento");
        builder.HasKey(s => s.Id);

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(s => s.TreinadorId).IsRequired();
        builder.HasOne<Treinador>()
            .WithMany()
            .HasForeignKey(s => s.TreinadorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(s => s.PacoteId).IsRequired();
        builder.HasOne<Pacote>()
            .WithMany()
            .HasForeignKey(s => s.PacoteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.LeadId).IsRequired();
        builder.HasOne<Lead>()
            .WithMany()
            .HasForeignKey(s => s.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.SlotId).IsRequired();
        builder.Property(s => s.InicioUtc).IsRequired();
        builder.Property(s => s.FimUtc).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().IsRequired();
        builder.Property(s => s.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(s => s.ArgumentosHash).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Motivo).HasMaxLength(500);
        builder.Property(s => s.DecididaEm);
        builder.Property(s => s.DecididaPorId);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt);

        builder.HasIndex(s => new { s.TreinadorId, s.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("ix_solicitacoes_agendamento_treinador_id_idempotency_key_unique");

        builder.HasIndex(s => new { s.TreinadorId, s.PacoteId, s.Status, s.InicioUtc })
            .HasDatabaseName("ix_solicitacoes_agendamento_treinador_id_pacote_id_status_inicio_utc");

        builder.HasIndex(s => new { s.TreinadorId, s.Status, s.InicioUtc })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_solicitacoes_agendamento_treinador_id_status_inicio_utc");

        // Cobre a listagem da esteira SEM filtro de status (o índice acima exige igualdade em
        // Status pra servir o ORDER BY sem sort extra) — AUD-42.
        builder.HasIndex(s => new { s.TreinadorId, s.InicioUtc, s.Id })
            .IsDescending(false, true, false)
            .HasDatabaseName("ix_solicitacoes_agendamento_treinador_id_inicio_utc_id");
    }
}
