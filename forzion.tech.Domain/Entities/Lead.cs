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

    private readonly List<LeadInteracao> _interacoes = [];
    public IReadOnlyList<LeadInteracao> Interacoes => _interacoes.AsReadOnly();

    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public ContatoLead Contato { get; private set; } = null!;
    public string? Interesse { get; private set; }
    public ConsentimentoLead Consentimento { get; private set; } = null!;
    public OrigemLead? Origem { get; private set; }
    public LeadSource Source { get; private set; }
    public LeadStatus Status { get; private set; }
    public MotivoDescarteLead? MotivoDescarte { get; private set; }
    public Guid? AlunoId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? ArgumentosHash { get; private set; }
    public DateTime UltimoToqueEm { get; private set; }
    public bool Anonimizado { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

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

    public Result MarcarEmContato(Guid realizadoPorId, string? observacao, DateTime agora)
    {
        if (Anonimizado)
            return Result.Failure(LeadErrors.Anonimizado);
        if (Status is LeadStatus.Convertido or LeadStatus.Descartado)
            return Result.Failure(LeadErrors.EstadoTerminal);
        if (Status == LeadStatus.EmContato)
            return Result.Failure(LeadErrors.JaEmContato);

        var interacaoResult = CriarInteracao(Status, LeadStatus.EmContato, observacao, realizadoPorId, agora);
        if (interacaoResult.IsFailure)
            return Result.Failure(interacaoResult.Error!);

        Status = LeadStatus.EmContato;
        _interacoes.Add(interacaoResult.Value);
        UltimoToqueEm = agora;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result RegistrarInteracao(Guid realizadoPorId, string observacao, DateTime agora)
    {
        if (Anonimizado)
            return Result.Failure(LeadErrors.Anonimizado);
        if (Status is LeadStatus.Convertido or LeadStatus.Descartado)
            return Result.Failure(LeadErrors.EstadoTerminal);
        if (string.IsNullOrWhiteSpace(observacao))
            return Result.Failure(LeadErrors.ObservacaoObrigatoria);

        var interacaoResult = CriarInteracao(Status, null, observacao, realizadoPorId, agora);
        if (interacaoResult.IsFailure)
            return Result.Failure(interacaoResult.Error!);

        _interacoes.Add(interacaoResult.Value);
        UltimoToqueEm = agora;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Converter(Guid alunoId, DateTime agora)
    {
        if (Anonimizado)
            return Result.Failure(LeadErrors.Anonimizado);
        if (Status is LeadStatus.Convertido or LeadStatus.Descartado)
            return Result.Failure(LeadErrors.EstadoTerminal);
        if (alunoId == Guid.Empty)
            return Result.Failure(LeadErrors.AlunoIdInvalido);

        var interacaoResult = CriarInteracao(Status, LeadStatus.Convertido, null, null, agora);
        if (interacaoResult.IsFailure)
            return Result.Failure(interacaoResult.Error!);

        Status = LeadStatus.Convertido;
        AlunoId = alunoId;
        _interacoes.Add(interacaoResult.Value);
        UltimoToqueEm = agora;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Descartar(MotivoDescarteLead motivo, Guid realizadoPorId, string? observacao, DateTime agora)
    {
        if (Anonimizado)
            return Result.Failure(LeadErrors.Anonimizado);
        if (Status is LeadStatus.Convertido or LeadStatus.Descartado)
            return Result.Failure(LeadErrors.EstadoTerminal);

        var interacaoResult = CriarInteracao(Status, LeadStatus.Descartado, observacao, realizadoPorId, agora);
        if (interacaoResult.IsFailure)
            return Result.Failure(interacaoResult.Error!);

        Status = LeadStatus.Descartado;
        MotivoDescarte = motivo;
        _interacoes.Add(interacaoResult.Value);
        UltimoToqueEm = agora;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result RegistrarToque(DateTime agora)
    {
        if (Anonimizado)
            return Result.Failure(LeadErrors.Anonimizado);

        UltimoToqueEm = agora;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Anonimizar(DateTime agora)
    {
        if (Anonimizado)
            return Result.Success();

        Nome = "Lead anonimizado";
        Contato = Contato.Anonimizar();
        Interesse = null;
        Consentimento = Consentimento.Anonimizar();
        foreach (var interacao in _interacoes)
            interacao.Anonimizar();

        Anonimizado = true;
        UpdatedAt = agora;
        return Result.Success();
    }

    private static Result<LeadInteracao> CriarInteracao(LeadStatus? statusAnterior, LeadStatus? statusNovo, string? observacao, Guid? realizadoPorId, DateTime agora)
    {
        var observacaoNormalizada = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        if (observacaoNormalizada is not null && observacaoNormalizada.Length > 1000)
            return Result.Failure<LeadInteracao>(LeadErrors.ObservacaoMuitoLonga);

        return Result.Success(LeadInteracao.Criar(statusAnterior, statusNovo, observacaoNormalizada, realizadoPorId, agora));
    }
}

public sealed class LeadInteracao
{
    public Guid Id { get; private set; }
    public DateTime OcorridaEm { get; private set; }
    public LeadStatus? StatusAnterior { get; private set; }
    public LeadStatus? StatusNovo { get; private set; }
    public string? Observacao { get; private set; }
    public Guid? RealizadoPorId { get; private set; }

    private LeadInteracao() { }

    internal static LeadInteracao Criar(LeadStatus? statusAnterior, LeadStatus? statusNovo, string? observacao, Guid? realizadoPorId, DateTime agora) =>
        new()
        {
            Id = Guid.NewGuid(),
            OcorridaEm = agora,
            StatusAnterior = statusAnterior,
            StatusNovo = statusNovo,
            Observacao = observacao,
            RealizadoPorId = realizadoPorId
        };

    internal void Anonimizar()
    {
        if (Observacao is not null)
            Observacao = "[anonimizado]";
    }
}
