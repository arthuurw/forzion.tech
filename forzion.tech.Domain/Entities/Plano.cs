namespace forzion.tech.Domain.Entities;

public class Plano
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public decimal Preco { get; private set; }
    public int LimiteAlunos { get; private set; }
    public bool IsFree { get; private set; }

    private Plano() { }

    public static Plano Criar(string nome, decimal preco, int limiteAlunos, bool isFree = false)
    {
        return new Plano
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Preco = preco,
            LimiteAlunos = limiteAlunos,
            IsFree = isFree
        };
    }

    public static Plano CriarComId(Guid id, string nome, decimal preco, int limiteAlunos, bool isFree = false)
    {
        return new Plano
        {
            Id = id,
            Nome = nome,
            Preco = preco,
            LimiteAlunos = limiteAlunos,
            IsFree = isFree
        };
    }
}
