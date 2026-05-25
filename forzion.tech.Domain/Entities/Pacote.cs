using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class Pacote
{
    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public decimal Preco { get; private set; }
    public bool IsAtivo { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Pacote() { }

    public static Pacote Criar(Guid treinadorId, string nome, decimal preco, DateTime agora, string? descricao = null)
    {
        if (treinadorId == Guid.Empty)
            throw new DomainException("O identificador do treinador é inválido.");
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome é obrigatório.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome deve ter no máximo 100 caracteres.");
        if (preco < 0)
            throw new DomainException("O preço não pode ser negativo.");
        if (descricao?.Trim().Length > 500)
            throw new DomainException("A descrição deve ter no máximo 500 caracteres.");

        return new Pacote
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Nome = nome.Trim(),
            Descricao = descricao?.Trim(),
            Preco = preco,
            IsAtivo = true,
            CreatedAt = agora
        };
    }

    public void Atualizar(string? nome, decimal? preco, string? descricao)
    {
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new DomainException("O nome não pode ser vazio.");
            if (nome.Trim().Length > 100)
                throw new DomainException("O nome deve ter no máximo 100 caracteres.");
            Nome = nome.Trim();
        }

        if (preco is not null)
        {
            if (preco < 0)
                throw new DomainException("O preço não pode ser negativo.");
            Preco = preco.Value;
        }

        if (descricao is not null)
        {
            if (descricao.Trim().Length > 500)
                throw new DomainException("A descrição deve ter no máximo 500 caracteres.");
            Descricao = descricao.Trim();
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void Inativar()
    {
        IsAtivo = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
