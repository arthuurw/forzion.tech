using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class VinculoTreinadorAlunoTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaVinculo()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId);

        v.Id.Should().NotBeEmpty();
        v.TreinadorId.Should().Be(TreinadorId);
        v.AlunoId.Should().Be(AlunoId);
        v.Status.Should().Be(VinculoStatus.AguardandoAprovacao);
        v.PacoteAlunoId.Should().BeNull();
        v.AprovadoPorId.Should().BeNull();
        v.DataInicio.Should().BeNull();
        v.DataFim.Should().BeNull();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var act = () => VinculoTreinadorAluno.Criar(Guid.Empty, AlunoId);
        act.Should().Throw<DomainException>().WithMessage("O identificador do treinador é inválido.");
    }

    [Fact]
    public void Criar_AlunoIdVazio_LancaDomainException()
    {
        var act = () => VinculoTreinadorAluno.Criar(TreinadorId, Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("O identificador do aluno é inválido.");
    }

    // --- Aprovar ---

    [Fact]
    public void Aprovar_AguardandoAprovacao_MudaParaAtivo()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId);
        var pacoteId = Guid.NewGuid();

        v.Aprovar(TreinadorId, pacoteId);

        v.Status.Should().Be(VinculoStatus.Ativo);
        v.PacoteAlunoId.Should().Be(pacoteId);
        v.AprovadoPorId.Should().Be(TreinadorId);
        v.AprovadoEm.Should().NotBeNull();
        v.DataInicio.Should().NotBeNull();
    }

    [Fact]
    public void Aprovar_JaAtivo_LancaDomainException()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId);
        v.Aprovar(TreinadorId, Guid.NewGuid());

        var act = () => v.Aprovar(TreinadorId, Guid.NewGuid());
        act.Should().Throw<DomainException>().WithMessage("Apenas vínculos aguardando aprovação podem ser aprovados.");
    }

    [Fact]
    public void Aprovar_PacoteIdVazio_LancaDomainException()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId);
        var act = () => v.Aprovar(TreinadorId, Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("O identificador do pacote é inválido.");
    }

    // --- Inativar ---

    [Fact]
    public void Inativar_Ativo_MudaParaInativo()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId);
        v.Aprovar(TreinadorId, Guid.NewGuid());

        v.Inativar();

        v.Status.Should().Be(VinculoStatus.Inativo);
        v.DataFim.Should().NotBeNull();
    }

    [Fact]
    public void Inativar_JaInativo_LancaDomainException()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId);
        v.Aprovar(TreinadorId, Guid.NewGuid());
        v.Inativar();

        var act = () => v.Inativar();
        act.Should().Throw<DomainException>().WithMessage("O vínculo já está inativo.");
    }

    [Fact]
    public void Inativar_AguardandoAprovacao_MudaParaInativo()
    {
        var v = VinculoTreinadorAluno.Criar(TreinadorId, AlunoId);
        v.Inativar();
        v.Status.Should().Be(VinculoStatus.Inativo);
    }
}
