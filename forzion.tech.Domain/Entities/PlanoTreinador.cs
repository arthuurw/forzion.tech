using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class PlanoTreinador
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public int MaxAlunos { get; private set; }
    public decimal Preco { get; private set; }
    public bool IsAtivo { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private PlanoTreinador() { }

    public static PlanoTreinador Criar(string nome, int maxAlunos, decimal preco)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome é obrigatório.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome deve ter no máximo 100 caracteres.");
        if (maxAlunos <= 0)
            throw new DomainException("O limite de alunos deve ser maior que zero.");
        if (preco < 0)
            throw new DomainException("O preço não pode ser negativo.");

        return new PlanoTreinador
        {
            Id = Guid.NewGuid(),
            Nome = nome.Trim(),
            MaxAlunos = maxAlunos,
            Preco = preco,
            IsAtivo = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Atualizar(string? nome, int? maxAlunos, decimal? preco)
    {
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new DomainException("O nome não pode ser vazio.");
            if (nome.Trim().Length > 100)
                throw new DomainException("O nome deve ter no máximo 100 caracteres.");
            Nome = nome.Trim();
        }

        if (maxAlunos is not null)
        {
            if (maxAlunos <= 0)
                throw new DomainException("O limite de alunos deve ser maior que zero.");
            MaxAlunos = maxAlunos.Value;
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

    public void Ativar()
    {
        IsAtivo = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
