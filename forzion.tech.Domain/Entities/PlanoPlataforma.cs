using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Interfaces;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

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

    public static Result<PlanoPlataforma> Criar(string nome, TierPlano tier, int maxAlunos, decimal preco, DateTime agora, string? descricao = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure<PlanoPlataforma>(PlanoPlataformaErrors.NomeObrigatorio);
        if (nome.Trim().Length > 100)
            return Result.Failure<PlanoPlataforma>(PlanoPlataformaErrors.NomeMuitoLongo);
        if (maxAlunos <= 0)
            return Result.Failure<PlanoPlataforma>(PlanoPlataformaErrors.MaxAlunosInvalido);
        if (preco < 0)
            return Result.Failure<PlanoPlataforma>(PlanoPlataformaErrors.PrecoNegativo);

        return Result.Success(new PlanoPlataforma
        {
            Id = Guid.NewGuid(),
            Nome = nome.Trim(),
            Tier = tier,
            Descricao = descricao?.Trim(),
            MaxAlunos = maxAlunos,
            Preco = preco,
            IsAtivo = true,
            CreatedAt = agora
        });
    }

    public Result Atualizar(string? nome, TierPlano? tier, int? maxAlunos, decimal? preco, DateTime agora, string? descricao = null)
    {
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return Result.Failure(PlanoPlataformaErrors.NomeVazio);
            if (nome.Trim().Length > 100)
                return Result.Failure(PlanoPlataformaErrors.NomeMuitoLongo);
            Nome = nome.Trim();
        }

        if (tier is not null)
            Tier = tier.Value;

        if (maxAlunos is not null)
        {
            if (maxAlunos <= 0)
                return Result.Failure(PlanoPlataformaErrors.MaxAlunosInvalido);
            MaxAlunos = maxAlunos.Value;
        }

        if (preco is not null)
        {
            if (preco < 0)
                return Result.Failure(PlanoPlataformaErrors.PrecoNegativo);
            Preco = preco.Value;
        }

        Descricao = descricao?.Trim() ?? Descricao;

        UpdatedAt = agora;
        return Result.Success();
    }

    public void Inativar(DateTime agora)
    {
        IsAtivo = false;
        UpdatedAt = agora;
    }

    public void Ativar(DateTime agora)
    {
        IsAtivo = true;
        UpdatedAt = agora;
    }
}
