using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Entities;

public class Lead : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public ContatoLead Contato { get; private set; } = null!;
    public string? Interesse { get; private set; }
    public ConsentimentoLead Consentimento { get; private set; } = null!;
    public OrigemLead? Origem { get; private set; }
    public LeadSource Source { get; private set; }
    public LeadStatus Status { get; private set; }
    public MotivoDescarteLead? MotivoDescarte { get; }
    public Guid? AlunoId { get; }
    public string? IdempotencyKey { get; private set; }
    public string? ArgumentosHash { get; private set; }
    public DateTime UltimoToqueEm { get; private set; }
    public bool Anonimizado { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; }

    private Lead() { }

    public static Result<Lead> Criar(
        Guid treinadorId,
        string nome,
        ContatoLead contato,
        string? interesse,
        ConsentimentoLead consentimento,
        OrigemLead? origem,
        LeadSource source,
        string? idempotencyKey,
        string? argumentosHash,
        DateTime agora)
    {
        if (treinadorId == Guid.Empty)
            return Result.Failure<Lead>(LeadErrors.TreinadorIdInvalido);

        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure<Lead>(LeadErrors.NomeObrigatorio);

        var nomeNormalizado = nome.Trim();
        if (nomeNormalizado.Length > 200)
            return Result.Failure<Lead>(LeadErrors.NomeMuitoLongo);

        var interesseNormalizado = string.IsNullOrWhiteSpace(interesse) ? null : interesse.Trim();
        if (interesseNormalizado is not null && interesseNormalizado.Length > 1000)
            return Result.Failure<Lead>(LeadErrors.InteresseMuitoLongo);

        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Nome = nomeNormalizado,
            Contato = contato,
            Interesse = interesseNormalizado,
            Consentimento = consentimento,
            Origem = origem,
            Source = source,
            Status = LeadStatus.Novo,
            IdempotencyKey = idempotencyKey,
            ArgumentosHash = argumentosHash,
            UltimoToqueEm = agora,
            Anonimizado = false,
            CreatedAt = agora
        };
        lead._domainEvents.Add(new LeadCriadoEvent(lead.Id, treinadorId, source, agora));
        return Result.Success(lead);
    }
}
