using System.Reflection;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Builders;

/// <summary>
/// Builder determinístico de <see cref="Conta"/>. Delega a <see cref="Conta.Criar"/>.
/// </summary>
public sealed class ContaBuilder
{
    private Email _email = Email.Criar("conta.teste@forzion.tech").Value;
    private string _passwordHash = "hash-deterministico";
    private TipoConta _tipoConta = TipoConta.Aluno;
    private DateTime _agora = TestData.Agora;
    private bool _engajamentoEmailOptOut;

    public ContaBuilder ComEmail(string email)
    {
        _email = Email.Criar(email).Value;
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

    public ContaBuilder ComEngajamentoEmailOptOut(bool valor = true)
    {
        _engajamentoEmailOptOut = valor;
        return this;
    }

    public Conta Build()
    {
        var conta = Conta.Criar(_email, _passwordHash, _tipoConta, _agora).Value;
        if (_engajamentoEmailOptOut)
        {
            typeof(Conta)
                .GetField("<NotificacoesEngajamentoEmailOptOut>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(conta, true);
        }
        return conta;
    }

    public static implicit operator Conta(ContaBuilder builder) => builder.Build();
}
