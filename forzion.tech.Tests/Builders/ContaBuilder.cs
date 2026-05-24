using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Builders;

/// <summary>
/// Builder determinístico de <see cref="Conta"/>. Delega a <see cref="Conta.Criar"/>.
/// </summary>
public sealed class ContaBuilder
{
    private Email _email = Email.Criar("conta.teste@forzion.tech");
    private string _passwordHash = "hash-deterministico";
    private TipoConta _tipoConta = TipoConta.Aluno;
    private DateTime _agora = TestData.Agora;

    public ContaBuilder ComEmail(string email)
    {
        _email = Email.Criar(email);
        return this;
    }

    public ContaBuilder ComPasswordHash(string passwordHash)
    {
        _passwordHash = passwordHash;
        return this;
    }

    public ContaBuilder ComTipo(TipoConta tipoConta)
    {
        _tipoConta = tipoConta;
        return this;
    }

    public ContaBuilder Em(DateTime agora)
    {
        _agora = agora;
        return this;
    }

    public Conta Build() => Conta.Criar(_email, _passwordHash, _tipoConta, _agora);

    public static implicit operator Conta(ContaBuilder builder) => builder.Build();
}
