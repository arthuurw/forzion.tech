using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class TreinoAlunoTests
{
    [Fact]
    public void Criar_DadosValidos_RetornaTreinoAluno()
    {
        var ta = TreinoAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        ta.Status.Should().Be(TreinoAlunoStatus.Ativo);
        ta.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_TreinoIdVazio_LancaDomainException()
    {
        var act = () => TreinoAluno.Criar(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_AlunoIdVazio_LancaDomainException()
    {
        var act = () => TreinoAluno.Criar(Guid.NewGuid(), Guid.Empty, DateTime.UtcNow);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AlterarStatus_Inativo_AtualizaStatus()
    {
        var ta = TreinoAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        ta.AlterarStatus(TreinoAlunoStatus.Inativo);
        ta.Status.Should().Be(TreinoAlunoStatus.Inativo);
        ta.UpdatedAt.Should().NotBeNull();
    }
}
