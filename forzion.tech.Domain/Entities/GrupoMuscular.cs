using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class GrupoMuscular
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private GrupoMuscular() { }

    public static GrupoMuscular Criar(string nome, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do grupo muscular é obrigatório.");

        if (nome.Trim().Length > 50)
            throw new DomainException("O nome do grupo muscular deve ter no máximo 50 caracteres.");

        return new GrupoMuscular
        {
            Id = Guid.NewGuid(),
            Nome = nome.Trim(),
            CreatedAt = agora
        };
    }

    public void Atualizar(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do grupo muscular não pode ser vazio.");

        if (nome.Trim().Length > 50)
            throw new DomainException("O nome do grupo muscular deve ter no máximo 50 caracteres.");

        Nome = nome.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
