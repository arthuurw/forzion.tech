using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class TreinoAlunoTests
{
    [Fact]
    public void Criar_DadosValidos_RetornaTreinoAluno()
    {
        var ta = TreinoAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), TestData.Agora).Value;
        ta.Status.Should().Be(TreinoAlunoStatus.Ativo);
        ta.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_TreinoIdVazio_LancaDomainException()
    {
        var r = TreinoAluno.Criar(Guid.Empty, Guid.NewGuid(), TestData.Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_AlunoIdVazio_LancaDomainException()
    {
        var r = TreinoAluno.Criar(Guid.NewGuid(), Guid.Empty, TestData.Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AlterarStatus_Inativo_AtualizaStatus()
    {
        var ta = TreinoAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), TestData.Agora).Value;
        ta.AlterarStatus(TreinoAlunoStatus.Inativo, TestData.Agora);
        ta.Status.Should().Be(TreinoAlunoStatus.Inativo);
        ta.UpdatedAt.Should().NotBeNull();
    }
}
