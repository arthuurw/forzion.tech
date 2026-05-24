using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class AssinaturaAluno : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

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
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private AssinaturaAluno() { }

    public static AssinaturaAluno Criar(Guid vinculoId, Guid pacoteId, Guid treinadorId, Guid alunoId, decimal valor)
    {
        if (vinculoId == Guid.Empty)
            throw new DomainException("O identificador do vínculo é inválido.");
        if (pacoteId == Guid.Empty)
            throw new DomainException("O identificador do pacote é inválido.");
        if (treinadorId == Guid.Empty)
            throw new DomainException("O identificador do treinador é inválido.");
        if (alunoId == Guid.Empty)
            throw new DomainException("O identificador do aluno é inválido.");
        if (valor <= 0)
            throw new DomainException("O valor da assinatura deve ser maior que zero.");

        var assinatura = new AssinaturaAluno
        {
            Id = Guid.NewGuid(),
            VinculoId = vinculoId,
            PacoteId = pacoteId,
            TreinadorId = treinadorId,
            AlunoId = alunoId,
            Valor = valor,
            Status = AssinaturaAlunoStatus.Pendente,
            DataInicio = DateTime.UtcNow,
            DataProximaCobranca = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        assinatura._domainEvents.Add(new AssinaturaAlunoCriadaEvent(
            assinatura.Id, treinadorId, alunoId, pacoteId, valor, DateTime.UtcNow));

        return assinatura;
    }

    public void Ativar()
    {
        if (Status == AssinaturaAlunoStatus.Cancelada)
            throw new DomainException("AssinaturaAluno cancelada não pode ser ativada.");

        Status = AssinaturaAlunoStatus.Ativa;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarcarInadimplente()
    {
        if (Status != AssinaturaAlunoStatus.Ativa)
            throw new DomainException("Apenas assinaturas ativas podem ser marcadas como inadimplentes.");

        Status = AssinaturaAlunoStatus.Inadimplente;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancelar()
    {
        if (Status == AssinaturaAlunoStatus.Cancelada)
            throw new DomainException("A assinatura já está cancelada.");

        Status = AssinaturaAlunoStatus.Cancelada;
        DataCancelamento = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AgendarProximaCobranca(DateTime dataProximaCobranca)
    {
        if (dataProximaCobranca <= DateTime.UtcNow)
            throw new DomainException("A data da próxima cobrança deve ser futura.");

        DataProximaCobranca = dataProximaCobranca;
        UpdatedAt = DateTime.UtcNow;
    }
}
