using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class RedefinicaoSenhaSegundoFatorConfiguration : IEntityTypeConfiguration<RedefinicaoSenhaSegundoFator>
{
    public void Configure(EntityTypeBuilder<RedefinicaoSenhaSegundoFator> builder)
    {
        builder.ToTable("redefinicao_senha_segundo_fator");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id");
        builder.Property(g => g.ContaId).HasColumnName("conta_id");
        builder.Property(g => g.Tentativas).HasColumnName("tentativas");
        builder.Property(g => g.JanelaInicio).HasColumnName("janela_inicio");
        builder.Property(g => g.AtualizadoEm).HasColumnName("atualizado_em");

        builder.HasIndex(g => g.ContaId).IsUnique().HasDatabaseName("ix_redefinicao_senha_segundo_fator_conta_id");
    }
}
