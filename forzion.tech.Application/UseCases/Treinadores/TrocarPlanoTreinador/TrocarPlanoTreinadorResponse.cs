using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.TrocarPlanoTreinador;

public enum TipoTrocaPlano
{
    Upgrade = 0,
    Downgrade = 1,
    InadimplenteRegularizacao = 2,
    UpgradeImediato = 3    // upgrade aplicado no ato sem cobrança (proração zero)
}

public record TrocarPlanoTreinadorResponse(
    TipoTrocaPlano Tipo,
    Guid? PagamentoId,
    decimal? ValorPagamento,
    MetodoPagamento? MetodoPagamento,
    string? PixQrCode,
    string? PixQrCodeUrl,
    DateTime? PixExpiracao,
    string? ClientSecret,
    DateTime? DataEfetivacao)
{
    public static TrocarPlanoTreinadorResponse Upgrade(PagamentoTreinador p) => new(
        TipoTrocaPlano.Upgrade, p.Id, p.Valor, p.MetodoPagamento,
        p.PixQrCode, p.PixQrCodeUrl, p.PixExpiracao, p.ClientSecret, null);

    public static TrocarPlanoTreinadorResponse Downgrade(DateTime dataEfetivacao) => new(
        TipoTrocaPlano.Downgrade, null, null, null, null, null, null, null, dataEfetivacao);

    public static TrocarPlanoTreinadorResponse UpgradeImediato(DateTime agora) => new(
        TipoTrocaPlano.UpgradeImediato, null, null, null, null, null, null, null, agora);

    public static TrocarPlanoTreinadorResponse Regularizacao(PagamentoTreinador p) => new(
        TipoTrocaPlano.InadimplenteRegularizacao, p.Id, p.Valor, p.MetodoPagamento,
        p.PixQrCode, p.PixQrCodeUrl, p.PixExpiracao, p.ClientSecret, null);
}
