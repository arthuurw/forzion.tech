using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class PacoteAluno
{
    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public int MaxFichas { get; private set; }
    public decimal Preco { get; private set; }
    public bool IsAtivo { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private PacoteAluno() { }

    public static PacoteAluno Criar(Guid treinadorId, string nome, int maxFichas, decimal preco)
    {
        if (treinadorId == Guid.Empty)
            throw new DomainException("O identificador do treinador é inválido.");
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome é obrigatório.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome deve ter no máximo 100 caracteres.");
        if (maxFichas <= 0)
            throw new DomainException("O limite de fichas deve ser maior que zero.");
        if (preco < 0)
            throw new DomainException("O preço não pode ser negativo.");

        return new PacoteAluno
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Nome = nome.Trim(),
            MaxFichas = maxFichas,
            Preco = preco,
            IsAtivo = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Atualizar(string? nome, int? maxFichas, decimal? preco)
    {
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new DomainException("O nome não pode ser vazio.");
            if (nome.Trim().Length > 100)
                throw new DomainException("O nome deve ter no máximo 100 caracteres.");
            Nome = nome.Trim();
        }

        if (maxFichas is not null)
        {
            if (maxFichas <= 0)
                throw new DomainException("O limite de fichas deve ser maior que zero.");
            MaxFichas = maxFichas.Value;
        }

        if (preco is not null)
        {
            if (preco < 0)
                throw new DomainException("O preço não pode ser negativo.");
            Preco = preco.Value;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void Inativar()
    {
        IsAtivo = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
