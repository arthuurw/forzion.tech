using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Interfaces;

namespace forzion.tech.Domain.Entities;

public class PlanoPlataforma : ICapacidadePlano
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public TierPlano Tier { get; private set; }
    public string? Descricao { get; private set; }
    public int MaxAlunos { get; private set; }
    public decimal Preco { get; private set; }
    public bool IsAtivo { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private PlanoPlataforma() { }

    public static PlanoPlataforma Criar(string nome, TierPlano tier, int maxAlunos, decimal preco, string? descricao = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome é obrigatório.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome deve ter no máximo 100 caracteres.");
        if (maxAlunos <= 0)
            throw new DomainException("O limite de alunos deve ser maior que zero.");
        if (preco < 0)
            throw new DomainException("O preço não pode ser negativo.");

        return new PlanoPlataforma
        {
            Id = Guid.NewGuid(),
            Nome = nome.Trim(),
            Tier = tier,
            Descricao = descricao?.Trim(),
            MaxAlunos = maxAlunos,
            Preco = preco,
            IsAtivo = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Atualizar(string? nome, TierPlano? tier, int? maxAlunos, decimal? preco, string? descricao = null)
    {
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new DomainException("O nome não pode ser vazio.");
            if (nome.Trim().Length > 100)
                throw new DomainException("O nome deve ter no máximo 100 caracteres.");
            Nome = nome.Trim();
        }

        if (tier is not null)
            Tier = tier.Value;

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

        Descricao = descricao?.Trim() ?? Descricao;

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
