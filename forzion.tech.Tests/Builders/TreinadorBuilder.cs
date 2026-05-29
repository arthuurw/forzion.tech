using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Builders;

/// <summary>
/// Builder determinístico de <see cref="Treinador"/>. Delega a <see cref="Treinador.Criar"/>.
/// </summary>
public sealed class TreinadorBuilder
{
    private Guid _contaId = TestData.NextGuid();
    private string _nome = "Treinador Teste";
    private DateTime _agora = TestData.Agora;
    private string? _telefone;

    public TreinadorBuilder ComContaId(Guid contaId)
    {
        _contaId = contaId;
        return this;
    }

    public TreinadorBuilder ComNome(string nome)
    {
        _nome = nome;
        return this;
    }

    public TreinadorBuilder Em(DateTime agora)
    {
        _agora = agora;
        return this;
    }

    public TreinadorBuilder ComTelefone(string? telefone)
    {
        _telefone = telefone;
        return this;
    }

    public Treinador Build() => Treinador.Criar(_contaId, _nome, _agora, _telefone).Value;

    public static implicit operator Treinador(TreinadorBuilder builder) => builder.Build();
}
