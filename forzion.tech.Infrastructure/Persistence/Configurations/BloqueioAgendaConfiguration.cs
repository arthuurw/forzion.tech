using forzion.tech.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace forzion.tech.Infrastructure.Persistence.Configurations;

public class BloqueioAgendaConfiguration : IEntityTypeConfiguration<BloqueioAgenda>
{
    public void Configure(EntityTypeBuilder<BloqueioAgenda> builder)
    {
        builder.ToTable("bloqueios_agenda");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.TreinadorId).IsRequired();
        builder.HasOne<Treinador>()
            .WithMany()
            .HasForeignKey(b => b.TreinadorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(b => b.Tipo).HasConversion<string>().IsRequired();

        builder.Property(b => b.InicioUtc);
        builder.Property(b => b.FimUtc);
        builder.Property(b => b.DiaSemana);
        builder.Property(b => b.HoraInicio);
        builder.Property(b => b.HoraFim);
        builder.Property(b => b.Motivo).HasMaxLength(200);

        builder.Property(b => b.CreatedAt).IsRequired();

        builder.HasIndex(b => new { b.TreinadorId, b.Tipo });
        builder.HasIndex(b => new { b.TreinadorId, b.InicioUtc, b.FimUtc });
    }
}
