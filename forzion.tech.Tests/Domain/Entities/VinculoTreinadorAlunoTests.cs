using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class VinculoTreinadorAlunoTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaVinculo()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId, TestData.Agora).Value;

        v.Id.Should().NotBeEmpty();
        v.TreinadorId.Should().Be(TreinadorId);
        v.AlunoId.Should().Be(AlunoId);
        v.Status.Should().Be(VinculoStatus.AguardandoAprovacao);
        v.PacoteId.Should().BeNull();
        v.AprovadoPorId.Should().BeNull();
        v.DataInicio.Should().BeNull();
        v.DataFim.Should().BeNull();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var r = VinculoTreinadorAluno.Criar(Guid.Empty, AlunoId, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do treinador é inválido.");
    }

    [Fact]
    public void Criar_AlunoIdVazio_LancaDomainException()
    {
        var r = VinculoTreinadorAluno.Criar(TreinadorId, Guid.Empty, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do aluno é inválido.");
    }

    // --- Aprovar ---

    [Fact]
    public void Aprovar_AguardandoAprovacao_MudaParaAtivo()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId, TestData.Agora).Value;
        var pacoteId = Guid.NewGuid();

        v.Aprovar(TreinadorId, pacoteId, TestData.Agora);

        v.Status.Should().Be(VinculoStatus.Ativo);
        v.PacoteId.Should().Be(pacoteId);
        v.AprovadoPorId.Should().Be(TreinadorId);
        v.AprovadoEm.Should().NotBeNull();
        v.DataInicio.Should().NotBeNull();
    }

    [Fact]
    public void Aprovar_JaAtivo_LancaDomainException()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId, TestData.Agora).Value;
        v.Aprovar(TreinadorId, Guid.NewGuid(), TestData.Agora);

        var r = v.Aprovar(TreinadorId, Guid.NewGuid(), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas vínculos aguardando aprovação podem ser aprovados.");
    }

    [Fact]
    public void Aprovar_PacoteIdVazio_LancaDomainException()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId, TestData.Agora).Value;
        var r = v.Aprovar(TreinadorId, Guid.Empty, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do pacote é inválido.");
    }

    // --- Inativar ---

    [Fact]
    public void Inativar_Ativo_MudaParaInativo()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId, TestData.Agora).Value;
        v.Aprovar(TreinadorId, Guid.NewGuid(), TestData.Agora);

        v.Inativar(TestData.Agora);

        v.Status.Should().Be(VinculoStatus.Inativo);
        v.DataFim.Should().NotBeNull();
    }

    [Fact]
    public void Inativar_JaInativo_LancaDomainException()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId, TestData.Agora).Value;
        v.Aprovar(TreinadorId, Guid.NewGuid(), TestData.Agora);
        v.Inativar(TestData.Agora);

        var r = v.Inativar(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O vínculo já está inativo.");
    }

    [Fact]
    public void Inativar_AguardandoAprovacao_MudaParaInativo()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId, TestData.Agora).Value;
        v.Inativar(TestData.Agora);
        v.Status.Should().Be(VinculoStatus.Inativo);
    }

    // --- DefinirPreservacao ---

    [Fact]
    public void Criar_Default_PreservarNoLimiteFalso()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId, TestData.Agora).Value;
        v.PreservarNoLimite.Should().BeFalse();
    }

    [Fact]
    public void DefinirPreservacao_True_AtivaFlag()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId, TestData.Agora).Value;

        var r = v.DefinirPreservacao(true, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        v.PreservarNoLimite.Should().BeTrue();
    }

    [Fact]
    public void DefinirPreservacao_False_DesativaFlag()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId, TestData.Agora).Value;
        v.DefinirPreservacao(true, TestData.Agora);

        var r = v.DefinirPreservacao(false, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        v.PreservarNoLimite.Should().BeFalse();
    }
}
