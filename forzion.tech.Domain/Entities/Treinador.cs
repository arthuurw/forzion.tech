using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class Treinador
{
    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Guid? PlanoTreinadorId { get; private set; }
    public TreinadorStatus Status { get; private set; }
    public Guid? AprovadoPorId { get; private set; }
    public DateTime? AprovadoEm { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Treinador() { }

    public static Treinador Criar(Guid contaId, string nome)
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
    }

    public void Inativar()
    {
        if (Status == TreinadorStatus.Inativo)
            throw new DomainException("O treinador já está inativo.");

        Status = TreinadorStatus.Inativo;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AtribuirPlano(Guid planoTreinadorId)
    {
        if (planoTreinadorId == Guid.Empty)
            throw new DomainException("O identificador do plano é inválido.");

        PlanoTreinadorId = planoTreinadorId;
        UpdatedAt = DateTime.UtcNow;
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
