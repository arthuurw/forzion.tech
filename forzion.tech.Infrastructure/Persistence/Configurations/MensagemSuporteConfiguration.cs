using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class MensagemSuporteConfiguration : IEntityTypeConfiguration<MensagemSuporte>
{
    public void Configure(EntityTypeBuilder<MensagemSuporte> builder)
    {
        builder.ToTable("mensagens_suporte");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.ContaId).HasColumnName("conta_id").IsRequired();
        builder.HasIndex(m => m.ContaId);

        builder.HasOne<Conta>()
            .WithMany()
            .HasForeignKey(m => m.ContaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Enum como string (convenção do repo): legível em DB e resiliente a reordenação do enum.
        builder.Property(m => m.Categoria)
            .HasColumnName("categoria")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Assunto)
            .HasColumnName("assunto")
            .HasMaxLength(MensagemSuporte.AssuntoMaxLength)
            .IsRequired();

        builder.Property(m => m.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(MensagemSuporte.DescricaoMaxLength)
            .IsRequired();

        builder.Property(m => m.CriadaEm).HasColumnName("criada_em").IsRequired();
    }
}
