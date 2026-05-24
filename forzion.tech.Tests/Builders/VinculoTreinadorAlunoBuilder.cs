using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Builders;

/// <summary>
/// Builder determinístico de <see cref="VinculoTreinadorAluno"/>. Delega a
/// <see cref="VinculoTreinadorAluno.Criar"/>.
/// </summary>
public sealed class VinculoTreinadorAlunoBuilder
{
    private Guid _treinadorId = TestData.NextGuid();
    private Guid _alunoId = TestData.NextGuid();
    private DateTime _agora = TestData.Agora;
    private Guid? _pacoteId;

    public VinculoTreinadorAlunoBuilder ComTreinadorId(Guid treinadorId)
    {
        _treinadorId = treinadorId;
        return this;
    }

    public VinculoTreinadorAlunoBuilder ComAlunoId(Guid alunoId)
    {
        _alunoId = alunoId;
        return this;
    }

    public VinculoTreinadorAlunoBuilder Em(DateTime agora)
    {
        _agora = agora;
        return this;
    }

    public VinculoTreinadorAlunoBuilder ComPacoteId(Guid? pacoteId)
    {
        _pacoteId = pacoteId;
        return this;
    }

    public VinculoTreinadorAluno Build() =>
        VinculoTreinadorAluno.Criar(_treinadorId, _alunoId, _agora, _pacoteId);

    public static implicit operator VinculoTreinadorAluno(VinculoTreinadorAlunoBuilder builder) => builder.Build();
}
