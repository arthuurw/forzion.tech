using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class AssinaturaTreinador : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public const int LimiteTentativasFalhas = 3;

    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public Guid PlanoPlataformaId { get; private set; }
    public decimal Valor { get; private set; }
    public AssinaturaTreinadorStatus Status { get; private set; }
    public DateTime DataInicio { get; private set; }
    public DateTime DataProximaCobranca { get; private set; }
    public DateTime? DataCancelamento { get; private set; }
    public int TentativasFalhasConsecutivas { get; private set; }
    public Guid? PlanoPlataformaIdAgendado { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private AssinaturaTreinador() { }

    public static Result<AssinaturaTreinador> Criar(Guid treinadorId, Guid planoPlataformaId, decimal valor, DateTime agora)
    {
        if (treinadorId == Guid.Empty)
            return Result.Failure<AssinaturaTreinador>(AssinaturaTreinadorErrors.TreinadorIdInvalido);
        if (planoPlataformaId == Guid.Empty)
            return Result.Failure<AssinaturaTreinador>(AssinaturaTreinadorErrors.PlanoIdInvalido);
        if (valor <= 0)
            return Result.Failure<AssinaturaTreinador>(AssinaturaTreinadorErrors.ValorInvalido);

        var assinatura = new AssinaturaTreinador
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            PlanoPlataformaId = planoPlataformaId,
            Valor = valor,
            Status = AssinaturaTreinadorStatus.Pendente,
            DataInicio = agora,
            DataProximaCobranca = agora,
            CreatedAt = agora
        };

        assinatura._domainEvents.Add(new AssinaturaTreinadorCriadaEvent(
            assinatura.Id, treinadorId, planoPlataformaId, valor, agora));

        return Result.Success(assinatura);
    }

    public Result Ativar(DateTime agora)
    {
        if (Status == AssinaturaTreinadorStatus.Cancelada)
            return Result.Failure(AssinaturaTreinadorErrors.CanceladaNaoAtivavel);
        if (Status == AssinaturaTreinadorStatus.Inadimplente)
            return Result.Failure(AssinaturaTreinadorErrors.InadimplenteDeveUsarRegularizacao);

        Status = AssinaturaTreinadorStatus.Ativa;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result MarcarInadimplente(DateTime agora)
    {
        if (Status != AssinaturaTreinadorStatus.Ativa)
            return Result.Failure(AssinaturaTreinadorErrors.ApenasAtivasInadimplentes);

        Status = AssinaturaTreinadorStatus.Inadimplente;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Cancelar(DateTime agora)
    {
        if (Status == AssinaturaTreinadorStatus.Cancelada)
            return Result.Failure(AssinaturaTreinadorErrors.JaCancelada);

        Status = AssinaturaTreinadorStatus.Cancelada;
        DataCancelamento = agora;
        UpdatedAt = agora;
        _domainEvents.Add(new AssinaturaTreinadorCanceladaEvent(Id, TreinadorId, agora));
        return Result.Success();
    }

    public Result AgendarProximaCobranca(DateTime dataProximaCobranca, DateTime agora)
    {
        if (dataProximaCobranca <= agora)
            return Result.Failure(AssinaturaTreinadorErrors.ProximaCobrancaNaoFutura);

        DataProximaCobranca = dataProximaCobranca;
        UpdatedAt = agora;
        return Result.Success();
    }

    public void RegistrarPagamentoFalho(DateTime agora)
    {
        if (Status == AssinaturaTreinadorStatus.Cancelada)
            return;

        TentativasFalhasConsecutivas++;
        UpdatedAt = agora;

        if (TentativasFalhasConsecutivas >= LimiteTentativasFalhas
            && Status == AssinaturaTreinadorStatus.Ativa)
        {
            Status = AssinaturaTreinadorStatus.Inadimplente;
            _domainEvents.Add(new AssinaturaTreinadorMarcadaInadimplenteEvent(
                Id, TreinadorId, TentativasFalhasConsecutivas, agora));
        }
    }

    public void RegistrarPagamentoRegularizado(DateTime agora)
    {
        if (Status == AssinaturaTreinadorStatus.Cancelada)
            return;

        TentativasFalhasConsecutivas = 0;

        if (Status == AssinaturaTreinadorStatus.Inadimplente)
        {
            Status = AssinaturaTreinadorStatus.Ativa;
            _domainEvents.Add(new AssinaturaTreinadorReativadaEvent(Id, TreinadorId, agora));
        }

        UpdatedAt = agora;
    }

    public Result TrocarPlanoImediato(Guid novoPlanoId, decimal novoValor, DateTime agora)
    {
        if (Status is not (AssinaturaTreinadorStatus.Ativa or AssinaturaTreinadorStatus.Inadimplente))
            return Result.Failure(AssinaturaTreinadorErrors.TrocaPlanoEstadoInvalido);
        if (novoPlanoId == Guid.Empty)
            return Result.Failure(AssinaturaTreinadorErrors.PlanoIdInvalido);
        if (novoValor <= 0)
            return Result.Failure(AssinaturaTreinadorErrors.ValorInvalido);

        var anterior = PlanoPlataformaId;
        PlanoPlataformaId = novoPlanoId;
        Valor = novoValor;
        PlanoPlataformaIdAgendado = null;
        UpdatedAt = agora;
        _domainEvents.Add(new AssinaturaTreinadorPlanoTrocadoEvent(Id, TreinadorId, anterior, novoPlanoId, agora));
        return Result.Success();
    }

    public Result AgendarDowngrade(Guid novoPlanoId, DateTime agora)
    {
        if (Status != AssinaturaTreinadorStatus.Ativa)
            return Result.Failure(AssinaturaTreinadorErrors.TrocaPlanoEstadoInvalido);
        if (novoPlanoId == Guid.Empty)
            return Result.Failure(AssinaturaTreinadorErrors.PlanoAgendadoIdInvalido);

        PlanoPlataformaIdAgendado = novoPlanoId;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result AplicarPlanoAgendado(decimal novoValor, DateTime agora)
    {
        if (PlanoPlataformaIdAgendado is null)
            return Result.Success();
        if (novoValor <= 0)
            return Result.Failure(AssinaturaTreinadorErrors.ValorInvalido);

        var anterior = PlanoPlataformaId;
        PlanoPlataformaId = PlanoPlataformaIdAgendado.Value;
        Valor = novoValor;
        PlanoPlataformaIdAgendado = null;
        UpdatedAt = agora;
        _domainEvents.Add(new AssinaturaTreinadorPlanoTrocadoEvent(Id, TreinadorId, anterior, PlanoPlataformaId, agora));
        return Result.Success();
    }
}
