using forzion.tech.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public sealed class AiTokenUsageConfiguration : IEntityTypeConfiguration<AiTokenUsage>
{
    public void Configure(EntityTypeBuilder<AiTokenUsage> builder)
    {
        builder.ToTable("ai_token_usage");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.AgentType).HasColumnName("agent_type").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Date).HasColumnName("date");
        builder.Property(x => x.TokenCount).HasColumnName("token_count");

        builder.HasIndex(x => new { x.UserId, x.AgentType, x.Date })
            .IsUnique()
            .HasDatabaseName("ix_ai_token_usage_user_agent_date");
    }
}
