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

    /// <summary>
    /// F32 — escape hatch para NEGATIVE tests de handler que precisam simular
    /// uma entidade "inconsistente" persistida (ex: legacy data com Nome vazio
    /// que escapou validacao no momento da criacao). Usa private ctor + setters
    /// via reflection, BURLA Aluno.Criar e seus guards de dominio.
    ///
    /// Uso esperado: handler tests que querem provar "se o repositorio devolver
    /// um aluno com X invalido, o handler reage com Y." NUNCA usar pra construir
    /// fixtures de happy-path — use Build() pra isso.
    /// </summary>
    public static Aluno BuildUnsafe(
        Guid? id = null,
        Guid? contaId = null,
        string nome = "",
        string? email = null,
        DateTime? createdAt = null,
        forzion.tech.Domain.Enums.AlunoStatus status = forzion.tech.Domain.Enums.AlunoStatus.AguardandoAprovacao)
    {
        var ctor = typeof(Aluno).GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            Type.EmptyTypes)
            ?? throw new InvalidOperationException("Aluno sem private ctor.");
        var aluno = (Aluno)ctor.Invoke(null);

        SetPrivate(aluno, "Id", id ?? TestData.NextGuid());
        SetPrivate(aluno, "ContaId", contaId ?? TestData.NextGuid());
        SetPrivate(aluno, "Nome", nome);
        SetPrivate(aluno, "CreatedAt", createdAt ?? TestData.Agora);
        SetPrivate(aluno, "Status", status);
        if (email is not null)
            SetPrivate(aluno, "Email", forzion.tech.Domain.ValueObjects.Email.Criar(email));

        return aluno;
    }

    private static void SetPrivate(object target, string name, object value)
    {
        var prop = target.GetType().GetProperty(
            name,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Propriedade {name} nao encontrada em {target.GetType().Name}.");
        prop.SetValue(target, value);
    }
}
