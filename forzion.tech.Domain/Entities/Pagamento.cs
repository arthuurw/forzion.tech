using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

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

    public static Result<Pagamento> Criar(Guid assinaturaId, decimal valor, DateTime agora, MetodoPagamento metodo = MetodoPagamento.Pix)
    {
        if (assinaturaId == Guid.Empty)
            return Result.Failure<Pagamento>(PagamentoErrors.AssinaturaIdInvalido);
        if (valor <= 0)
            return Result.Failure<Pagamento>(PagamentoErrors.ValorInvalido);

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

        return Result.Success(pagamento);
    }

    public Result DefinirDadosPix(string paymentIntentId, string qrCode, string qrCodeUrl, DateTime expiracao, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            return Result.Failure(PagamentoErrors.PaymentIntentIdInvalido);
        if (string.IsNullOrWhiteSpace(qrCode))
            return Result.Failure(PagamentoErrors.QrCodeInvalido);

        StripePaymentIntentId = paymentIntentId;
        PixQrCode = qrCode;
        PixQrCodeUrl = qrCodeUrl;
        PixExpiracao = expiracao;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result DefinirDadosCartao(string paymentIntentId, string clientSecret, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            return Result.Failure(PagamentoErrors.PaymentIntentIdInvalido);
        if (string.IsNullOrWhiteSpace(clientSecret))
            return Result.Failure(PagamentoErrors.ClientSecretInvalido);

        StripePaymentIntentId = paymentIntentId;
        ClientSecret = clientSecret;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result MarcarPago(DateTime agora)
    {
        if (Status != PagamentoStatus.Pendente)
            return Result.Failure(PagamentoErrors.ApenasPendentesPagos);

        Status = PagamentoStatus.Pago;
        DataPagamento = agora;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result MarcarFalhou(DateTime agora)
    {
        if (Status != PagamentoStatus.Pendente)
            return Result.Failure(PagamentoErrors.ApenasPendentesFalhou);

        Status = PagamentoStatus.Falhou;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result MarcarExpirado(DateTime agora)
    {
        if (Status != PagamentoStatus.Pendente)
            return Result.Failure(PagamentoErrors.ApenasPendentesExpirados);

        Status = PagamentoStatus.Expirado;
        UpdatedAt = agora;
        return Result.Success();
    }

    /// <summary>
    /// Aplica refund Stripe (webhook <c>charge.refunded</c>). Guard: só transiciona
    /// de Pago; outras origens retornam Failure. <c>DataPagamento</c> preservada
    /// como registro histórico do momento que o dinheiro chegou — auditoria/contabilidade
    /// precisam dessa data. Dispara <see cref="PagamentoEstornadoEvent"/> pra notificar aluno.
    /// </summary>
    public Result MarcarEstornado(DateTime agora)
    {
        if (Status != PagamentoStatus.Pago)
            return Result.Failure(PagamentoErrors.ApenasPagosEstornados);

        Status = PagamentoStatus.Estornado;
        UpdatedAt = agora;

        _domainEvents.Add(new PagamentoEstornadoEvent(
            Id, AssinaturaAlunoId, Valor, agora));
        return Result.Success();
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
    /// <param name="agora">Instante da operação (UpdatedAt + OcorridoEm do evento).</param>
    public Result MarcarEmDisputa(string motivoDisputa, DateTime agora)
    {
        if (Status != PagamentoStatus.Pago)
            return Result.Failure(PagamentoErrors.ApenasPagosEmDisputa);

        Status = PagamentoStatus.EmDisputa;
        UpdatedAt = agora;

        var motivo = string.IsNullOrWhiteSpace(motivoDisputa) ? "unknown" : motivoDisputa.Trim();
        _domainEvents.Add(new PagamentoEmDisputaEvent(
            Id, AssinaturaAlunoId, Valor, motivo, agora));
        return Result.Success();
    }
}
