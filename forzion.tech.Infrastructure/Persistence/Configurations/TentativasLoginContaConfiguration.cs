using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class TentativasLoginContaConfiguration : IEntityTypeConfiguration<TentativasLoginConta>
{
    public void Configure(EntityTypeBuilder<TentativasLoginConta> builder)
    {
        builder.ToTable("tentativas_login_conta");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.ContaId).HasColumnName("conta_id");
        builder.Property(t => t.Tentativas).HasColumnName("tentativas");
        builder.Property(t => t.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasIndex(t => t.ContaId).IsUnique().HasDatabaseName("ix_tentativas_login_conta_conta_id");
    }
}
