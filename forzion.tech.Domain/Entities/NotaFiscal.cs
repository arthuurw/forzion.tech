using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class NotaFiscal : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public TipoNotaFiscal Tipo { get; private set; }
    public Guid? PagamentoTreinadorId { get; private set; }
    public DateOnly? CompetenciaInicio { get; private set; }
    public DateOnly? CompetenciaFim { get; private set; }
    public decimal Valor { get; private set; }
    public NotaFiscalStatus Status { get; private set; }
    public string? ChaveAcesso { get; private set; }
    public string? NumeroNfse { get; private set; }
    public string? NumeroDps { get; private set; }
    public DateTime? DataEmissao { get; private set; }
    public string? DanfseRef { get; private set; }
    public string? CodigoErro { get; private set; }
    public string? MotivoErro { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private NotaFiscal() { }

    public static Result<NotaFiscal> CriarAssinatura(Guid treinadorId, Guid pagamentoTreinadorId, decimal valor, DateTime agora)
    {
        if (treinadorId == Guid.Empty)
            return Result.Failure<NotaFiscal>(NotaFiscalErrors.TreinadorIdInvalido);
        if (pagamentoTreinadorId == Guid.Empty)
            return Result.Failure<NotaFiscal>(NotaFiscalErrors.PagamentoIdInvalido);
        if (valor <= 0)
            return Result.Failure<NotaFiscal>(NotaFiscalErrors.ValorInvalido);

        var nota = new NotaFiscal
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Tipo = TipoNotaFiscal.AssinaturaSaaS,
            PagamentoTreinadorId = pagamentoTreinadorId,
            Valor = valor,
            Status = NotaFiscalStatus.Pendente,
            CreatedAt = agora
        };
        nota.NumeroDps = nota.NumeroDpsEstavel();
        return Result.Success(nota);
    }

    public static Result<NotaFiscal> CriarComissao(Guid treinadorId, DateOnly competenciaInicio, DateOnly competenciaFim, decimal valor, DateTime agora)
    {
        if (treinadorId == Guid.Empty)
            return Result.Failure<NotaFiscal>(NotaFiscalErrors.TreinadorIdInvalido);
        if (competenciaFim < competenciaInicio)
            return Result.Failure<NotaFiscal>(NotaFiscalErrors.CompetenciaInvalida);
        if (valor <= 0)
            return Result.Failure<NotaFiscal>(NotaFiscalErrors.ValorInvalido);

        var nota = new NotaFiscal
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Tipo = TipoNotaFiscal.ComissaoMarketplace,
            CompetenciaInicio = competenciaInicio,
            CompetenciaFim = competenciaFim,
            Valor = valor,
            Status = NotaFiscalStatus.Pendente,
            CreatedAt = agora
        };
        nota.NumeroDps = nota.NumeroDpsEstavel();
        return Result.Success(nota);
    }

    public string NumeroDpsEstavel() => Tipo switch
    {
        TipoNotaFiscal.AssinaturaSaaS => $"AS-{PagamentoTreinadorId}",
        TipoNotaFiscal.ComissaoMarketplace => $"CM-{TreinadorId}-{CompetenciaInicio:yyyyMM}",
        _ => $"NF-{Id}"
    };

    public Result MarcarEmitida(string chaveAcesso, string numeroNfse, DateTime dataEmissao, string? danfseRef, DateTime agora)
    {
        if (Status is not (NotaFiscalStatus.Pendente or NotaFiscalStatus.Erro))
            return Result.Failure(NotaFiscalErrors.TransicaoEmissaoInvalida);
        if (string.IsNullOrWhiteSpace(chaveAcesso))
            return Result.Failure(NotaFiscalErrors.ChaveAcessoObrigatoria);

        Status = NotaFiscalStatus.Emitida;
        ChaveAcesso = chaveAcesso;
        NumeroNfse = string.IsNullOrWhiteSpace(numeroNfse) ? null : numeroNfse;
        DataEmissao = dataEmissao;
        DanfseRef = string.IsNullOrWhiteSpace(danfseRef) ? null : danfseRef;
        CodigoErro = null;
        MotivoErro = null;
        UpdatedAt = agora;
        _domainEvents.Add(new NotaFiscalEmitidaEvent(Id, TreinadorId, chaveAcesso, agora));
        return Result.Success();
    }

    public Result MarcarErro(string codigo, string motivo, DateTime agora)
    {
        if (Status is not (NotaFiscalStatus.Pendente or NotaFiscalStatus.Erro))
            return Result.Failure(NotaFiscalErrors.TransicaoErroInvalida);

        Status = NotaFiscalStatus.Erro;
        CodigoErro = codigo;
        MotivoErro = motivo;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result MarcarBloqueadaDadosFiscais(DateTime agora)
    {
        if (Status != NotaFiscalStatus.Pendente)
            return Result.Failure(NotaFiscalErrors.TransicaoBloqueioInvalida);

        Status = NotaFiscalStatus.BloqueadaDadosFiscais;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result SolicitarCancelamento(DateTime agora)
    {
        if (Status != NotaFiscalStatus.Emitida)
            return Result.Failure(NotaFiscalErrors.TransicaoCancelamentoInvalida);

        Status = NotaFiscalStatus.CancelamentoSolicitado;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result MarcarCancelada(DateTime agora)
    {
        if (Status != NotaFiscalStatus.CancelamentoSolicitado)
            return Result.Failure(NotaFiscalErrors.TransicaoCanceladaInvalida);

        Status = NotaFiscalStatus.Cancelada;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result MarcarCancelamentoExpirado(DateTime agora)
    {
        if (Status != NotaFiscalStatus.CancelamentoSolicitado)
            return Result.Failure(NotaFiscalErrors.TransicaoExpiradoInvalida);

        Status = NotaFiscalStatus.CancelamentoExpirado;
        UpdatedAt = agora;
        return Result.Success();
    }
}
