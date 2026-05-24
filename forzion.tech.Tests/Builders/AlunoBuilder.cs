using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Builders;

/// <summary>
/// Builder determinístico de <see cref="Aluno"/>. Defaults validos; overrides via With*.
/// Delega a construcao a <see cref="Aluno.Criar"/> — nao duplica validacao de dominio.
/// </summary>
public sealed class AlunoBuilder
{
    private Guid _contaId = TestData.NextGuid();
    private string _nome = "Aluno Teste";
    private DateTime _agora = TestData.Agora;
    private string? _email;
    private string? _telefone;

    public AlunoBuilder ComContaId(Guid contaId)
    {
        _contaId = contaId;
        return this;
    }

    public AlunoBuilder ComNome(string nome)
    {
        _nome = nome;
        return this;
    }

    public AlunoBuilder Em(DateTime agora)
    {
        _agora = agora;
        return this;
    }

    public AlunoBuilder ComEmail(string? email)
    {
        _email = email;
        return this;
    }

    public AlunoBuilder ComTelefone(string? telefone)
    {
        _telefone = telefone;
        return this;
    }

    public Aluno Build() => Aluno.Criar(_contaId, _nome, _agora, _email, _telefone);

    public static implicit operator Aluno(AlunoBuilder builder) => builder.Build();
}
