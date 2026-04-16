using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class PlanConfigurationTests
{
    private static readonly Guid PlanoId = Guid.NewGuid();
    private static readonly Guid CriadoPor = Guid.NewGuid();

    private static PlanConfiguration CriarValido() =>
        PlanConfiguration.Criar(PlanoId, 10, 5, 20, 19.90m, CriadoPor);

    [Fact]
    public void Criar_ComDadosValidos_RetornaPlanConfiguration()
    {
        var config = CriarValido();

        config.Id.Should().NotBeEmpty();
        config.PlanoId.Should().Be(PlanoId);
        config.MaxAlunos.Should().Be(10);
        config.MaxTreinosPorAluno.Should().Be(5);
        config.MaxExerciciosPorTreino.Should().Be(20);
        config.CustoPorAluno.Should().Be(19.90m);
        config.CriadoPor.Should().Be(CriadoPor);
        config.ValidTo.Should().BeNull();
        config.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Criar_ComMaxAlunosZero_LancaDomainException()
    {
        var act = () => PlanConfiguration.Criar(PlanoId, 0, 5, 20, 0m, CriadoPor);
        act.Should().Throw<DomainException>().WithMessage("O limite de alunos deve ser maior que zero.");
    }

    [Fact]
    public void Criar_ComMaxTreinosZero_LancaDomainException()
    {
        var act = () => PlanConfiguration.Criar(PlanoId, 10, 0, 20, 0m, CriadoPor);
        act.Should().Throw<DomainException>().WithMessage("O limite de treinos por aluno deve ser maior que zero.");
    }

    [Fact]
    public void Criar_ComMaxExerciciosZero_LancaDomainException()
    {
        var act = () => PlanConfiguration.Criar(PlanoId, 10, 5, 0, 0m, CriadoPor);
        act.Should().Throw<DomainException>().WithMessage("O limite de exercícios por treino deve ser maior que zero.");
    }

    [Fact]
    public void Criar_ComCustoNegativo_LancaDomainException()
    {
        var act = () => PlanConfiguration.Criar(PlanoId, 10, 5, 20, -1m, CriadoPor);
        act.Should().Throw<DomainException>().WithMessage("O custo por aluno não pode ser negativo.");
    }

    [Fact]
    public void Criar_ComPlanoIdVazio_LancaDomainException()
    {
        var act = () => PlanConfiguration.Criar(Guid.Empty, 10, 5, 20, 0m, CriadoPor);
        act.Should().Throw<DomainException>().WithMessage("O plano é inválido.");
    }

    [Fact]
    public void Criar_ComCriadoPorVazio_LancaDomainException()
    {
        var act = () => PlanConfiguration.Criar(PlanoId, 10, 5, 20, 0m, Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("O criador é inválido.");
    }

    [Fact]
    public void EstaVigente_QuandoValidToNulo_RetornaTrue()
    {
        var config = CriarValido();
        config.EstaVigente().Should().BeTrue();
    }

    [Fact]
    public void Invalidar_DefinValidToERetornaFalseParaEstaVigente()
    {
        var config = CriarValido();
        config.Invalidar();
        config.ValidTo.Should().NotBeNull();
        config.EstaVigente().Should().BeFalse();
    }
}
