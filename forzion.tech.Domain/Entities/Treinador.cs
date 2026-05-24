using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class Treinador : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Guid? PlanoTreinadorId { get; private set; }
    public TreinadorStatus Status { get; private set; }
    public string? Telefone { get; private set; }
    public Guid? AprovadoPorId { get; private set; }
    public DateTime? AprovadoEm { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Treinador() { }

    public static Treinador Criar(Guid contaId, string nome, string? telefone = null)
    {
        if (contaId == Guid.Empty)
            throw new DomainException("O identificador da conta é inválido.");
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome é obrigatório.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome deve ter no máximo 100 caracteres.");

        return new Treinador
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            Nome = nome.Trim(),
            Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim(),
            Status = TreinadorStatus.AguardandoAprovacao,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Aprovar(Guid aprovadoPorId)
    {
        if (Status != TreinadorStatus.AguardandoAprovacao)
            throw new DomainException("Apenas treinadores aguardando aprovação podem ser aprovados.");

        Status = TreinadorStatus.Ativo;
        AprovadoPorId = aprovadoPorId;
        AprovadoEm = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new TreinadorAprovadoEvent(Id, aprovadoPorId, DateTime.UtcNow));
    }

    public void Reprovar(Guid reprovadoPorId)
    {
        if (Status != TreinadorStatus.AguardandoAprovacao)
            throw new DomainException("Apenas treinadores aguardando aprovação podem ser reprovados.");

        Status = TreinadorStatus.Inativo;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new TreinadorReprovadoEvent(Id, reprovadoPorId, DateTime.UtcNow));
    }

    public void Inativar(Guid? inativadoPorId = null)
    {
        if (Status == TreinadorStatus.Inativo)
            throw new DomainException("O treinador já está inativo.");

        Status = TreinadorStatus.Inativo;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new TreinadorInativadoEvent(Id, inativadoPorId ?? Guid.Empty, DateTime.UtcNow));
    }

    public void ValidarDisponibilidade()
    {
        if (Status != TreinadorStatus.Ativo)
            throw new DomainException("O treinador selecionado não está disponível.");
    }

    public void AtribuirPlano(Guid planoTreinadorId)
    {
        if (planoTreinadorId == Guid.Empty)
            throw new DomainException("O identificador do plano é inválido.");
        if (Status == TreinadorStatus.Inativo)
            throw new DomainException("Não é possível atribuir plano a um treinador inativo.");

        PlanoTreinadorId = planoTreinadorId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ValidarParaExclusao()
    {
        if (Status != TreinadorStatus.Inativo)
            throw new DomainException("Apenas treinadores inativos podem ser excluídos permanentemente.");
    }

    public void AtualizarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome não pode ser vazio.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome deve ter no máximo 100 caracteres.");

        Nome = nome.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
