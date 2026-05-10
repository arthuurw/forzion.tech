using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class TokenRevogadoConfiguration : IEntityTypeConfiguration<TokenRevogado>
{
    public void Configure(EntityTypeBuilder<TokenRevogado> builder)
    {
        builder.ToTable("tokens_revogados");
        builder.HasKey(t => t.Jti);
        builder.Property(t => t.Jti).HasColumnName("jti");
        builder.Property(t => t.ExpiraEm).HasColumnName("expira_em");
        builder.HasIndex(t => t.ExpiraEm).HasDatabaseName("ix_tokens_revogados_expira_em");
    }
}
