using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.ContratarPlanoTreinador;

public record ContratarPlanoTreinadorResponse(
    Guid PagamentoId,
    decimal ValorPagamento,
    MetodoPagamento MetodoPagamento,
    string? PixQrCode,
    string? PixQrCodeUrl,
    DateTime? PixExpiracao,
    string? ClientSecret)
{
    public static ContratarPlanoTreinadorResponse De(PagamentoTreinador p) => new(
        p.Id, p.Valor, p.MetodoPagamento,
        p.PixQrCode, p.PixQrCodeUrl, p.PixExpiracao, p.ClientSecret);
}
