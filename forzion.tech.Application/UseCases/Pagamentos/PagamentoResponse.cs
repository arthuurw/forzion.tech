using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Pagamentos;

public record PagamentoResponse(
    Guid PagamentoId,
    Guid AssinaturaAlunoId,
    decimal Valor,
    PagamentoStatus Status,
    MetodoPagamento MetodoPagamento,
    string? PixQrCode,
    string? PixQrCodeUrl,
    DateTime? PixExpiracao,
    string? ClientSecret,
    DateTime? DataPagamento,
    DateTime CreatedAt);

public static class PagamentoResponseExtensions
{
    // Para o treinador — ClientSecret omitido (não deve sair do contexto do aluno)
    public static PagamentoResponse ToResponseTreinador(Pagamento p) => new(
        p.Id, p.AssinaturaAlunoId, p.Valor, p.Status, p.MetodoPagamento,
        p.PixQrCode, p.PixQrCodeUrl, p.PixExpiracao,
        null,
        p.DataPagamento, p.CreatedAt);

    // Para o aluno — inclui ClientSecret para confirmação de cartão
    public static PagamentoResponse ToResponseAluno(Pagamento p) => new(
        p.Id, p.AssinaturaAlunoId, p.Valor, p.Status, p.MetodoPagamento,
        p.PixQrCode, p.PixQrCodeUrl, p.PixExpiracao,
        p.ClientSecret,
        p.DataPagamento, p.CreatedAt);

    // Alias mantido para compatibilidade com handlers que não diferenciam contexto
    public static PagamentoResponse ToResponse(Pagamento p) => ToResponseTreinador(p);
}
