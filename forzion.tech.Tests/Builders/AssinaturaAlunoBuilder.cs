using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Builders;

/// <summary>
/// Builder determinístico de <see cref="AssinaturaAluno"/>. Delega a
/// <see cref="AssinaturaAluno.Criar"/>.
/// </summary>
public sealed class AssinaturaAlunoBuilder
{
    private Guid _vinculoId = TestData.NextGuid();
    private Guid _pacoteId = TestData.NextGuid();
    private Guid _treinadorId = TestData.NextGuid();
    private Guid _alunoId = TestData.NextGuid();
    private decimal _valor = 100m;
    private DateTime _agora = TestData.Agora;

    public AssinaturaAlunoBuilder ComVinculoId(Guid vinculoId)
    {
        _vinculoId = vinculoId;
        return this;
    }

    public AssinaturaAlunoBuilder ComPacoteId(Guid pacoteId)
    {
        _pacoteId = pacoteId;
        return this;
    }

    public AssinaturaAlunoBuilder ComTreinadorId(Guid treinadorId)
    {
        _treinadorId = treinadorId;
        return this;
    }

    public AssinaturaAlunoBuilder ComAlunoId(Guid alunoId)
    {
        _alunoId = alunoId;
        return this;
    }

    public AssinaturaAlunoBuilder ComValor(decimal valor)
    {
        _valor = valor;
        return this;
    }

    public AssinaturaAlunoBuilder Em(DateTime agora)
    {
        _agora = agora;
        return this;
    }

    public AssinaturaAluno Build() =>
        AssinaturaAluno.Criar(_vinculoId, _pacoteId, _treinadorId, _alunoId, _valor, _agora);

    public static implicit operator AssinaturaAluno(AssinaturaAlunoBuilder builder) => builder.Build();
}
