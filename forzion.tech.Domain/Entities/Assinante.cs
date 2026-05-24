namespace forzion.tech.Domain.Entities;

public class Assinante
{
    public Guid Id { get; private set; }
    public Guid AlunoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Assinante() { }

    public static Assinante Criar(Guid alunoId, string nome, string? email)
    {
        return new Assinante
        {
            Id = Guid.NewGuid(),
            AlunoId = alunoId,
            Nome = nome,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Sincronizar(string nome, string? email)
    {
        Nome = nome;
        Email = email;
        UpdatedAt = DateTime.UtcNow;
    }
}
