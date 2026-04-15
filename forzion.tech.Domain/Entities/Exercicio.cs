using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class Exercicio
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public GrupoMuscular GrupoMuscular { get; private set; }
    public string? Descricao { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Exercicio() { }

    public static Exercicio Criar(string nome, GrupoMuscular grupoMuscular, Guid tenantId, string? descricao = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome é obrigatório.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome deve ter no máximo 100 caracteres.");
        if (tenantId == Guid.Empty)
            throw new DomainException("O tenant é inválido.");
        if (descricao is not null && descricao.Length > 500)
            throw new DomainException("A descrição deve ter no máximo 500 caracteres.");

        return new Exercicio
        {
            Id = Guid.NewGuid(),
            Nome = nome.Trim(),
            GrupoMuscular = grupoMuscular,
            TenantId = tenantId,
            Descricao = descricao,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Atualizar(string? nome, GrupoMuscular? grupoMuscular, string? descricao)
    {
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new DomainException("O nome não pode ser vazio.");
            if (nome.Trim().Length > 100)
                throw new DomainException("O nome deve ter no máximo 100 caracteres.");
            Nome = nome.Trim();
        }

        if (grupoMuscular is not null)
            GrupoMuscular = grupoMuscular.Value;

        if (descricao is not null)
            Descricao = descricao.Length == 0 ? null : descricao.Length > 500
                ? throw new DomainException("A descrição deve ter no máximo 500 caracteres.")
                : descricao;

        UpdatedAt = DateTime.UtcNow;
    }
}
