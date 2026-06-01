using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class Treinador : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Guid? PlanoPlataformaId { get; private set; }
    public TreinadorStatus Status { get; private set; }
    public string? Telefone { get; private set; }
    public Guid? AprovadoPorId { get; private set; }
    public DateTime? AprovadoEm { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Treinador() { }

    public static Result<Treinador> Criar(Guid contaId, string nome, DateTime agora, string? telefone = null)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<Treinador>(TreinadorErrors.ContaIdInvalido);
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure<Treinador>(TreinadorErrors.NomeObrigatorio);
        if (nome.Trim().Length > 100)
            return Result.Failure<Treinador>(TreinadorErrors.NomeMuitoLongo);

        var treinador = new Treinador
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            Nome = nome.Trim(),
            Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim(),
            Status = TreinadorStatus.AguardandoAprovacao,
            CreatedAt = agora
        };

        return Result.Success(treinador);
    }

    public Result Aprovar(Guid aprovadoPorId, DateTime agora)
    {
        if (Status != TreinadorStatus.AguardandoAprovacao)
            return Result.Failure(TreinadorErrors.NaoAguardandoAprovacaoParaAprovar);

        Status = TreinadorStatus.Ativo;
        AprovadoPorId = aprovadoPorId;
        AprovadoEm = agora;
        UpdatedAt = agora;
        _domainEvents.Add(new TreinadorAprovadoEvent(Id, aprovadoPorId, agora));
        return Result.Success();
    }

    public Result Reprovar(Guid reprovadoPorId, DateTime agora)
    {
        if (Status != TreinadorStatus.AguardandoAprovacao)
            return Result.Failure(TreinadorErrors.NaoAguardandoAprovacaoParaReprovar);

        Status = TreinadorStatus.Inativo;
        UpdatedAt = agora;
        _domainEvents.Add(new TreinadorReprovadoEvent(Id, reprovadoPorId, agora));
        return Result.Success();
    }

    public Result Inativar(DateTime agora, Guid? inativadoPorId = null)
    {
        if (Status == TreinadorStatus.Inativo)
            return Result.Failure(TreinadorErrors.JaInativo);

        Status = TreinadorStatus.Inativo;
        UpdatedAt = agora;
        _domainEvents.Add(new TreinadorInativadoEvent(Id, inativadoPorId ?? Guid.Empty, agora));
        return Result.Success();
    }

    public Result ValidarDisponibilidade()
    {
        if (Status != TreinadorStatus.Ativo)
            return Result.Failure(TreinadorErrors.NaoDisponivel);

        return Result.Success();
    }

    public Result AtribuirPlano(Guid planoPlataformaId, DateTime agora)
    {
        if (planoPlataformaId == Guid.Empty)
            return Result.Failure(TreinadorErrors.PlanoIdInvalido);
        if (Status == TreinadorStatus.Inativo)
            return Result.Failure(TreinadorErrors.PlanoTreinadorInativo);

        PlanoPlataformaId = planoPlataformaId;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Anonimizar(DateTime agora)
    {
        if (Nome == "Usuário anonimizado")
            return Result.Success();

        Nome = "Usuário anonimizado";
        Telefone = null;
        UpdatedAt = agora;

        return Result.Success();
    }

    public Result ValidarParaExclusao()
    {
        if (Status != TreinadorStatus.Inativo)
            return Result.Failure(TreinadorErrors.ExclusaoApenasInativos);

        return Result.Success();
    }

    public Result AtualizarNome(string nome, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure(TreinadorErrors.NomeVazio);
        if (nome.Trim().Length > 100)
            return Result.Failure(TreinadorErrors.NomeMuitoLongo);

        Nome = nome.Trim();
        UpdatedAt = agora;
        return Result.Success();
    }
}
