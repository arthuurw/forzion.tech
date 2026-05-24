using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class Pagamento
{
    public Guid Id { get; private set; }
    public Guid AssinaturaAlunoId { get; private set; }
    public decimal Valor { get; private set; }
    public PagamentoStatus Status { get; private set; }
    public MetodoPagamento MetodoPagamento { get; private set; }
    public string? StripePaymentIntentId { get; private set; }
    public string? PixQrCode { get; private set; }
    public string? PixQrCodeUrl { get; private set; }
    public DateTime? PixExpiracao { get; private set; }
    public string? ClientSecret { get; private set; }
    public DateTime? DataPagamento { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Pagamento() { }

    public static Pagamento Criar(Guid assinaturaId, decimal valor, MetodoPagamento metodo = MetodoPagamento.Pix)
    {
        if (assinaturaId == Guid.Empty)
            throw new DomainException("O identificador da assinatura é inválido.");
        if (valor <= 0)
            throw new DomainException("O valor do pagamento deve ser maior que zero.");

        return new Pagamento
        {
            Id = Guid.NewGuid(),
            AssinaturaAlunoId = assinaturaId,
            Valor = valor,
            Status = PagamentoStatus.Pendente,
            MetodoPagamento = metodo,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void DefinirDadosPix(string paymentIntentId, string qrCode, string qrCodeUrl, DateTime expiracao)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new DomainException("O identificador do PaymentIntent é inválido.");
        if (string.IsNullOrWhiteSpace(qrCode))
            throw new DomainException("O QR code Pix é inválido.");

        StripePaymentIntentId = paymentIntentId;
        PixQrCode = qrCode;
        PixQrCodeUrl = qrCodeUrl;
        PixExpiracao = expiracao;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DefinirDadosCartao(string paymentIntentId, string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new DomainException("O identificador do PaymentIntent é inválido.");
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new DomainException("O client secret do cartão é inválido.");

        StripePaymentIntentId = paymentIntentId;
        ClientSecret = clientSecret;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarcarPago()
    {
        if (Status != PagamentoStatus.Pendente)
            throw new DomainException("Apenas pagamentos pendentes podem ser marcados como pagos.");

        Status = PagamentoStatus.Pago;
        DataPagamento = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarcarFalhou()
    {
        if (Status != PagamentoStatus.Pendente)
            throw new DomainException("Apenas pagamentos pendentes podem ser marcados como falhou.");

        Status = PagamentoStatus.Falhou;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarcarExpirado()
    {
        if (Status != PagamentoStatus.Pendente)
            throw new DomainException("Apenas pagamentos pendentes podem ser marcados como expirados.");

        Status = PagamentoStatus.Expirado;
        UpdatedAt = DateTime.UtcNow;
    }
}
