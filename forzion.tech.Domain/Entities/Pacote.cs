using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

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

    public static Result<Pacote> Criar(Guid treinadorId, string nome, decimal preco, DateTime agora, string? descricao = null)
    {
        if (treinadorId == Guid.Empty)
            return Result.Failure<Pacote>(PacoteErrors.TreinadorIdInvalido);
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure<Pacote>(PacoteErrors.NomeObrigatorio);
        if (nome.Trim().Length > 100)
            return Result.Failure<Pacote>(PacoteErrors.NomeMuitoLongo);
        if (preco < 0)
            return Result.Failure<Pacote>(PacoteErrors.PrecoNegativo);
        if (descricao?.Trim().Length > 500)
            return Result.Failure<Pacote>(PacoteErrors.DescricaoMuitoLonga);

        return Result.Success(new Pacote
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Nome = nome.Trim(),
            Descricao = descricao?.Trim(),
            Preco = preco,
            IsAtivo = true,
            CreatedAt = agora
        });
    }

    public Result Atualizar(string? nome, decimal? preco, string? descricao, DateTime agora)
    {
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return Result.Failure(PacoteErrors.NomeVazio);
            if (nome.Trim().Length > 100)
                return Result.Failure(PacoteErrors.NomeMuitoLongo);
            Nome = nome.Trim();
        }

        if (preco is not null)
        {
            if (preco < 0)
                return Result.Failure(PacoteErrors.PrecoNegativo);
            Preco = preco.Value;
        }

        if (descricao is not null)
        {
            if (descricao.Trim().Length > 500)
                return Result.Failure(PacoteErrors.DescricaoMuitoLonga);
            Descricao = descricao.Trim();
        }

        UpdatedAt = agora;
        return Result.Success();
    }

    public void Inativar(DateTime agora)
    {
        IsAtivo = false;
        UpdatedAt = agora;
    }
}
