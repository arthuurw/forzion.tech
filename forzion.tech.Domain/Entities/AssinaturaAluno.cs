using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class AssinaturaAluno : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>Threshold pra transição Ativa → Inadimplente.</summary>
    public const int LimiteTentativasFalhas = 3;

    public Guid Id { get; private set; }
    public Guid VinculoId { get; private set; }
    public Guid PacoteId { get; private set; }
    public Guid TreinadorId { get; private set; }
    public Guid AlunoId { get; private set; }
    public decimal Valor { get; private set; }
    public AssinaturaAlunoStatus Status { get; private set; }
    public DateTime DataInicio { get; private set; }
    public DateTime DataProximaCobranca { get; private set; }
    public DateTime? DataCancelamento { get; private set; }
    public int TentativasFalhasConsecutivas { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private AssinaturaAluno() { }

    public static Result<AssinaturaAluno> Criar(Guid vinculoId, Guid pacoteId, Guid treinadorId, Guid alunoId, decimal valor, DateTime agora)
    {
        if (vinculoId == Guid.Empty)
            return Result.Failure<AssinaturaAluno>(AssinaturaAlunoErrors.VinculoIdInvalido);
        if (pacoteId == Guid.Empty)
            return Result.Failure<AssinaturaAluno>(AssinaturaAlunoErrors.PacoteIdInvalido);
        if (treinadorId == Guid.Empty)
            return Result.Failure<AssinaturaAluno>(AssinaturaAlunoErrors.TreinadorIdInvalido);
        if (alunoId == Guid.Empty)
            return Result.Failure<AssinaturaAluno>(AssinaturaAlunoErrors.AlunoIdInvalido);
        if (valor <= 0)
            return Result.Failure<AssinaturaAluno>(AssinaturaAlunoErrors.ValorInvalido);

        var assinatura = new AssinaturaAluno
        {
            Id = Guid.NewGuid(),
            VinculoId = vinculoId,
            PacoteId = pacoteId,
            TreinadorId = treinadorId,
            AlunoId = alunoId,
            Valor = valor,
            Status = AssinaturaAlunoStatus.Pendente,
            DataInicio = agora,
            DataProximaCobranca = agora,
            CreatedAt = agora
        };

        assinatura._domainEvents.Add(new AssinaturaAlunoCriadaEvent(
            assinatura.Id, treinadorId, alunoId, pacoteId, valor, agora));

        return Result.Success(assinatura);
    }

    public Result Ativar(DateTime agora)
    {
        if (Status == AssinaturaAlunoStatus.Cancelada)
            return Result.Failure(AssinaturaAlunoErrors.CanceladaNaoAtivavel);

        // Inadimplente → Ativa NÃO pode ser feito via Ativar; o contador de falhas
        // consecutivas precisa ser zerado antes. Callers devem invocar RegistrarPagamentoRegularizado,
        // que transiciona atomicamente e reseta TentativasFalhasConsecutivas.
        if (Status == AssinaturaAlunoStatus.Inadimplente)
            return Result.Failure(AssinaturaAlunoErrors.InadimplenteDeveUsarRegularizacao);

        Status = AssinaturaAlunoStatus.Ativa;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result MarcarInadimplente(DateTime agora)
    {
        if (Status != AssinaturaAlunoStatus.Ativa)
            return Result.Failure(AssinaturaAlunoErrors.ApenasAtivasInadimplentes);

        Status = AssinaturaAlunoStatus.Inadimplente;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Cancelar(DateTime agora)
    {
        if (Status == AssinaturaAlunoStatus.Cancelada)
            return Result.Failure(AssinaturaAlunoErrors.JaCancelada);

        Status = AssinaturaAlunoStatus.Cancelada;
        DataCancelamento = agora;
        UpdatedAt = agora;

        _domainEvents.Add(new AssinaturaAlunoCanceladaEvent(
            Id, AlunoId, TreinadorId, Valor, agora));
        return Result.Success();
    }

    public Result AgendarProximaCobranca(DateTime dataProximaCobranca, DateTime agora)
    {
        if (dataProximaCobranca <= agora)
            return Result.Failure(AssinaturaAlunoErrors.ProximaCobrancaNaoFutura);

        DataProximaCobranca = dataProximaCobranca;
        UpdatedAt = agora;
        return Result.Success();
    }

    /// <summary>
    /// Incrementa contador de tentativas falhas e, se atingir
    /// <see cref="LimiteTentativasFalhas"/>, transiciona Ativa → Inadimplente
    /// (atomicamente). Sempre dispara <see cref="PagamentoFalhouEvent"/>; quando
    /// cruza o threshold, dispara também <see cref="AssinaturaAlunoMarcadaInadimplenteEvent"/>.
    ///
    /// Assinatura Cancelada → no-op (não conta tentativas).
    /// Assinatura Pendente → conta tentativas mas não marca Inadimplente (só
    /// Ativa transiciona; Pendente sai por outro caminho).
    /// </summary>
    public void RegistrarPagamentoFalho(DateTime agora)
    {
        if (Status == AssinaturaAlunoStatus.Cancelada)
            return;

        TentativasFalhasConsecutivas++;
        UpdatedAt = agora;

        _domainEvents.Add(new PagamentoFalhouEvent(
            Id, AlunoId, TentativasFalhasConsecutivas, agora));

        if (TentativasFalhasConsecutivas >= LimiteTentativasFalhas
            && Status == AssinaturaAlunoStatus.Ativa)
        {
            Status = AssinaturaAlunoStatus.Inadimplente;
            _domainEvents.Add(new AssinaturaAlunoMarcadaInadimplenteEvent(
                Id, AlunoId, TreinadorId, TentativasFalhasConsecutivas, agora));
        }
    }

    /// <summary>
    /// Força transição Ativa → Inadimplente em caso de disputa (chargeback) Stripe.
    /// Diferente de <see cref="RegistrarPagamentoFalho"/>, não incrementa contador
    /// gradualmente: disputa é evento de alta gravidade (fraude ou desistência
    /// drástica do aluno), então o acesso é congelado imediatamente — não vale a
    /// pena esperar atingir <see cref="LimiteTentativasFalhas"/>.
    ///
    /// <para>
    /// Cancelada → no-op (cancelada permanece cancelada).
    /// Inadimplente → no-op idempotente (já está no estado correto).
    /// Pendente → no-op (sai por outra via; disputa em assinatura nunca-ativada
    /// é cenário improvável).
    /// Ativa → transiciona, equipara contador a <see cref="LimiteTentativasFalhas"/>
    /// e dispara <see cref="AssinaturaAlunoMarcadaInadimplenteEvent"/>.
    /// </para>
    /// </summary>
    public void MarcarInadimplentePorDisputa(DateTime agora)
    {
        if (Status != AssinaturaAlunoStatus.Ativa) return;

        Status = AssinaturaAlunoStatus.Inadimplente;
        TentativasFalhasConsecutivas = LimiteTentativasFalhas;
        UpdatedAt = agora;

        _domainEvents.Add(new AssinaturaAlunoMarcadaInadimplenteEvent(
            Id, AlunoId, TreinadorId, TentativasFalhasConsecutivas, agora));
    }

    /// <summary>
    /// Zera contador de tentativas falhas. Se assinatura estava Inadimplente,
    /// volta pra Ativa (reativa) e dispara <see cref="AssinaturaAlunoReativadaEvent"/>.
    /// Idempotente — chamar 2x não causa dano (segunda chamada não dispara evento pois
    /// já está Ativa). Cancelada permanece Cancelada (não auto-reativa).
    /// </summary>
    public void RegistrarPagamentoRegularizado(DateTime agora)
    {
        if (Status == AssinaturaAlunoStatus.Cancelada)
            return;

        TentativasFalhasConsecutivas = 0;

        if (Status == AssinaturaAlunoStatus.Inadimplente)
        {
            Status = AssinaturaAlunoStatus.Ativa;
            _domainEvents.Add(new AssinaturaAlunoReativadaEvent(Id, AlunoId, TreinadorId, agora));
        }

        UpdatedAt = agora;
    }
}
