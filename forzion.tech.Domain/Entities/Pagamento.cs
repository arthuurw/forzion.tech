using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class Pagamento : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

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

    public static Pagamento Criar(Guid assinaturaId, decimal valor, DateTime agora, MetodoPagamento metodo = MetodoPagamento.Pix)
    {
        if (assinaturaId == Guid.Empty)
            throw new DomainException("O identificador da assinatura é inválido.");
        if (valor <= 0)
            throw new DomainException("O valor do pagamento deve ser maior que zero.");

        var pagamento = new Pagamento
        {
            Id = Guid.NewGuid(),
            AssinaturaAlunoId = assinaturaId,
            Valor = valor,
            Status = PagamentoStatus.Pendente,
            MetodoPagamento = metodo,
            CreatedAt = agora
        };

        pagamento._domainEvents.Add(new PagamentoCriadoEvent(
            pagamento.Id, assinaturaId, valor, metodo, agora));

        return pagamento;
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

    /// <summary>
    /// Aplica refund Stripe (webhook <c>charge.refunded</c>). Guard: só transiciona
    /// de Pago; outras origens lançam DomainException. <c>DataPagamento</c> preservada
    /// como registro histórico do momento que o dinheiro chegou — auditoria/contabilidade
    /// precisam dessa data. Dispara <see cref="PagamentoEstornadoEvent"/> pra notificar aluno.
    /// </summary>
    public void MarcarEstornado()
    {
        if (Status != PagamentoStatus.Pago)
            throw new DomainException("Apenas pagamentos pagos podem ser estornados.");

        Status = PagamentoStatus.Estornado;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new PagamentoEstornadoEvent(
            Id, AssinaturaAlunoId, Valor, UpdatedAt.Value));
    }

    /// <summary>
    /// Aplica chargeback Stripe (webhook <c>charge.dispute.created</c>). Guard:
    /// só transiciona de <see cref="PagamentoStatus.Pago"/> — disputa sobre estado
    /// diferente é incoerente (não há cobrança capturada pra disputar). <c>DataPagamento</c>
    /// preservada (registro histórico do recebimento original; auditoria precisa).
    /// O <see cref="PagamentoEmDisputaEvent"/> dispara handlers de notificação urgente
    /// (treinador via e-mail + alert crítico em log).
    /// </summary>
    /// <param name="motivoDisputa">Motivo enviado pelo Stripe (ex.: "fraudulent", "duplicate").</param>
    public void MarcarEmDisputa(string motivoDisputa)
    {
        if (Status != PagamentoStatus.Pago)
            throw new DomainException("Apenas pagamentos pagos podem ser marcados em disputa.");

        Status = PagamentoStatus.EmDisputa;
        UpdatedAt = DateTime.UtcNow;

        var motivo = string.IsNullOrWhiteSpace(motivoDisputa) ? "unknown" : motivoDisputa.Trim();
        _domainEvents.Add(new PagamentoEmDisputaEvent(
            Id, AssinaturaAlunoId, Valor, motivo, UpdatedAt.Value));
    }
}
