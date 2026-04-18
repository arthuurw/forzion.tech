using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class ExecucaoTreinoTests
{
    private static ExecucaoTreino CriarExecucao() =>
        ExecucaoTreino.Criar(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaExecucao()
    {
        var e = CriarExecucao();
        e.Exercicios.Should().BeEmpty();
        e.Observacao.Should().BeNull();
    }

    [Fact]
    public void Criar_TreinoIdVazio_LancaDomainException()
    {
        var act = () => ExecucaoTreino.Criar(Guid.Empty, Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_AlunoIdVazio_LancaDomainException()
    {
        var act = () => ExecucaoTreino.Criar(Guid.NewGuid(), Guid.Empty, DateTime.UtcNow);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_DataDefault_LancaDomainException()
    {
        var act = () => ExecucaoTreino.Criar(Guid.NewGuid(), Guid.NewGuid(), default);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_ObservacaoMuitoLonga_LancaDomainException()
    {
        var act = () => ExecucaoTreino.Criar(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, new string('a', 501));
        act.Should().Throw<DomainException>();
    }

    // --- AdicionarExercicio ---

    [Fact]
    public void AdicionarExercicio_DadosValidos_Adiciona()
    {
        var e = CriarExecucao();
        e.AdicionarExercicio(Guid.NewGuid(), 3, 12, 10m);
        e.Exercicios.Should().HaveCount(1);
        e.Exercicios[0].SeriesExecutadas.Should().Be(3);
        e.Exercicios[0].RepeticoesExecutadas.Should().Be(12);
        e.Exercicios[0].CargaExecutada.Should().Be(10m);
    }

    [Fact]
    public void AdicionarExercicio_SeriesZero_LancaDomainException()
    {
        var e = CriarExecucao();
        var act = () => e.AdicionarExercicio(Guid.NewGuid(), 0, 12, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdicionarExercicio_RepeticoesZero_LancaDomainException()
    {
        var e = CriarExecucao();
        var act = () => e.AdicionarExercicio(Guid.NewGuid(), 3, 0, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdicionarExercicio_CargaNegativa_LancaDomainException()
    {
        var e = CriarExecucao();
        var act = () => e.AdicionarExercicio(Guid.NewGuid(), 3, 12, -1m);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdicionarExercicio_MultiplosPorExecucao()
    {
        var e = CriarExecucao();
        e.AdicionarExercicio(Guid.NewGuid(), 3, 12, null);
        e.AdicionarExercicio(Guid.NewGuid(), 4, 8, 20m);
        e.Exercicios.Should().HaveCount(2);
    }
}
