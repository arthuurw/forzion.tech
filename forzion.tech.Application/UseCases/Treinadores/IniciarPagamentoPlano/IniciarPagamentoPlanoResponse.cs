using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.IniciarPagamentoPlano;

public record IniciarPagamentoPlanoResponse(
    Guid PagamentoId,
    decimal Valor,
    PagamentoStatus Status,
    MetodoPagamento MetodoPagamento,
    string? StripePaymentIntentId,
    string? PixQrCode,
    string? PixQrCodeUrl,
    DateTime? PixExpiracao,
    string? ClientSecret,
    DateTime CreatedAt)
{
    public static IniciarPagamentoPlanoResponse De(PagamentoTreinador p) => new(
        p.Id, p.Valor, p.Status, p.MetodoPagamento,
        p.StripePaymentIntentId, p.PixQrCode, p.PixQrCodeUrl, p.PixExpiracao,
        p.ClientSecret, p.CreatedAt);
}
