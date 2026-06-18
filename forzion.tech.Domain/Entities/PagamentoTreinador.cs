using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class PagamentoTreinador : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public Guid AssinaturaTreinadorId { get; private set; }
    public decimal Valor { get; private set; }
    public PagamentoStatus Status { get; private set; }
    public MetodoPagamento MetodoPagamento { get; private set; }
    public FinalidadePagamentoTreinador Finalidade { get; private set; }
    public Guid? PlanoAlvoId { get; private set; }
    public string? StripePaymentIntentId { get; private set; }
    public string? PixQrCode { get; private set; }
    public string? PixQrCodeUrl { get; private set; }
    public DateTime? PixExpiracao { get; private set; }
    public string? ClientSecret { get; private set; }
    public DateTime? DataPagamento { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private PagamentoTreinador() { }

    public static Result<PagamentoTreinador> Criar(
        Guid treinadorId,
        Guid assinaturaTreinadorId,
        decimal valor,
        FinalidadePagamentoTreinador finalidade,
        DateTime agora,
        MetodoPagamento metodo = MetodoPagamento.Pix,
        Guid? planoAlvoId = null)
    {
        if (treinadorId == Guid.Empty)
            return Result.Failure<PagamentoTreinador>(PagamentoTreinadorErrors.TreinadorIdInvalido);
        if (assinaturaTreinadorId == Guid.Empty)
            return Result.Failure<PagamentoTreinador>(PagamentoTreinadorErrors.AssinaturaIdInvalido);
        if (valor <= 0)
            return Result.Failure<PagamentoTreinador>(PagamentoTreinadorErrors.ValorInvalido);

        return Result.Success(new PagamentoTreinador
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            AssinaturaTreinadorId = assinaturaTreinadorId,
            Valor = valor,
            Status = PagamentoStatus.Pendente,
            MetodoPagamento = metodo,
            Finalidade = finalidade,
            PlanoAlvoId = planoAlvoId,
            CreatedAt = agora
        });
    }

    public Result DefinirDadosPix(string paymentIntentId, string qrCode, string qrCodeUrl, DateTime expiracao, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            return Result.Failure(PagamentoTreinadorErrors.PaymentIntentIdInvalido);
        if (string.IsNullOrWhiteSpace(qrCode))
            return Result.Failure(PagamentoTreinadorErrors.QrCodeInvalido);

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
            return Result.Failure(PagamentoTreinadorErrors.PaymentIntentIdInvalido);
        if (string.IsNullOrWhiteSpace(clientSecret))
            return Result.Failure(PagamentoTreinadorErrors.ClientSecretInvalido);

        StripePaymentIntentId = paymentIntentId;
        ClientSecret = clientSecret;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result MarcarPago(DateTime agora)
    {
        if (Status != PagamentoStatus.Pendente)
            return Result.Failure(PagamentoTreinadorErrors.ApenasPendentesPagos);

        Status = PagamentoStatus.Pago;
        DataPagamento = agora;
        UpdatedAt = agora;
        LimparDadosSensiveis();
        _domainEvents.Add(new PagamentoTreinadorPagoEvent(
            Id, TreinadorId, AssinaturaTreinadorId, Finalidade, PlanoAlvoId, agora));
        return Result.Success();
    }

    public Result MarcarFalhou(DateTime agora)
    {
        if (Status != PagamentoStatus.Pendente)
            return Result.Failure(PagamentoTreinadorErrors.ApenasPendentesFalhou);

        Status = PagamentoStatus.Falhou;
        UpdatedAt = agora;
        LimparDadosSensiveis();
        return Result.Success();
    }

    public Result MarcarExpirado(DateTime agora)
    {
        if (Status != PagamentoStatus.Pendente)
            return Result.Failure(PagamentoTreinadorErrors.ApenasPendentesExpirados);

        Status = PagamentoStatus.Expirado;
        UpdatedAt = agora;
        LimparDadosSensiveis();
        return Result.Success();
    }

    private void LimparDadosSensiveis()
    {
        ClientSecret = null;
        PixQrCode = null;
        PixQrCodeUrl = null;
    }

    public Result MarcarEstornado(DateTime agora)
    {
        if (Status != PagamentoStatus.Pago)
            return Result.Failure(PagamentoTreinadorErrors.ApenasPagosEstornados);

        Status = PagamentoStatus.Estornado;
        UpdatedAt = agora;
        _domainEvents.Add(new PagamentoTreinadorEstornadoEvent(Id, TreinadorId, Valor, agora));
        return Result.Success();
    }

    public Result MarcarEmDisputa(DateTime agora)
    {
        if (Status != PagamentoStatus.Pago)
            return Result.Failure(PagamentoTreinadorErrors.ApenasPagosEmDisputa);

        Status = PagamentoStatus.EmDisputa;
        UpdatedAt = agora;
        _domainEvents.Add(new PagamentoTreinadorEmDisputaEvent(Id, TreinadorId, Valor, agora));
        return Result.Success();
    }
}
