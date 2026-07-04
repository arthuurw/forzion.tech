using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class ContaConfiguration : IEntityTypeConfiguration<Conta>
{
    public void Configure(EntityTypeBuilder<Conta> builder)
    {
        builder.ToTable("contas");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.Email)
            .HasColumnName("email")
            .HasConversion(e => e.Value, v => Email.FromDatabase(v))
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(c => c.Email).IsUnique();

        builder.Property(c => c.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(c => c.TipoConta)
            .HasColumnName("tipo_conta")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.Property(c => c.AnonimizadaEm)
            .HasColumnName("anonimizada_em")
            .HasColumnType("timestamptz");

        builder.Property(c => c.SessoesInvalidasAntesDeUtc)
            .HasColumnName("sessoes_invalidas_antes_de_utc")
            .HasColumnType("timestamptz");

        builder.Property(c => c.NotificacoesEngajamentoEmailOptOut)
            .HasColumnName("notificacoes_engajamento_email_opt_out")
            .HasDefaultValue(false);
    }
}
