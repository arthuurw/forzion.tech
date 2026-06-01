using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Builders;

/// <summary>
/// Builder determinístico de <see cref="Pacote"/>. Delega a <see cref="Pacote.Criar"/>.
/// </summary>
public sealed class PacoteBuilder
{
    private Guid _treinadorId = TestData.NextGuid();
    private string _nome = "Pacote Teste";
    private decimal _preco = 100m;
    private DateTime _agora = TestData.Agora;
    private string? _descricao;

    public PacoteBuilder ComTreinadorId(Guid treinadorId)
    {
        _treinadorId = treinadorId;
        return this;
    }

    public PacoteBuilder ComNome(string nome)
    {
        _nome = nome;
        return this;
    }

    public PacoteBuilder ComPreco(decimal preco)
    {
        _preco = preco;
        return this;
    }

    public PacoteBuilder Em(DateTime agora)
    {
        _agora = agora;
        return this;
    }

    public PacoteBuilder ComDescricao(string? descricao)
    {
        _descricao = descricao;
        return this;
    }

    public Pacote Build() => Pacote.Criar(_treinadorId, _nome, _preco, _agora, _descricao).Value;

    public static implicit operator Pacote(PacoteBuilder builder) => builder.Build();
}
