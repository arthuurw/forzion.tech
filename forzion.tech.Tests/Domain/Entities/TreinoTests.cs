using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class TreinoTests
{
    private static Treino CriarTreino() =>
        Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, Guid.NewGuid(), Guid.NewGuid());

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaTreino()
    {
        var t = CriarTreino();
        t.Nome.Should().Be("Treino A");
        t.Objetivo.Should().Be(ObjetivoTreino.Hipertrofia);
        t.Exercicios.Should().BeEmpty();
        t.UpdatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => Treino.Criar(nome, ObjetivoTreino.Hipertrofia, Guid.NewGuid(), Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var act = () => Treino.Criar(new string('a', 101), ObjetivoTreino.Hipertrofia, Guid.NewGuid(), Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_TenantIdVazio_LancaDomainException()
    {
        var act = () => Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, Guid.Empty, Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var act = () => Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, Guid.NewGuid(), Guid.Empty);
        act.Should().Throw<DomainException>();
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_DadosValidos_AtualizaCampos()
    {
        var t = CriarTreino();
        t.Atualizar("Treino B", ObjetivoTreino.Forca);
        t.Nome.Should().Be("Treino B");
        t.Objetivo.Should().Be(ObjetivoTreino.Forca);
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_NomeVazio_LancaDomainException()
    {
        var t = CriarTreino();
        var act = () => t.Atualizar("", null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Atualizar_ApenasObjetivo_MantemNome()
    {
        var t = CriarTreino();
        t.Atualizar(null, ObjetivoTreino.Resistencia);
        t.Nome.Should().Be("Treino A");
        t.Objetivo.Should().Be(ObjetivoTreino.Resistencia);
    }

    // --- AdicionarExercicio ---

    [Fact]
    public void AdicionarExercicio_DadosValidos_AdicionaOrdenado()
    {
        var t = CriarTreino();
        t.AdicionarExercicio(Guid.NewGuid(), 3, 12, 10m, 60);
        t.AdicionarExercicio(Guid.NewGuid(), 4, 8, null, null);
        t.Exercicios.Should().HaveCount(2);
        t.Exercicios[0].Ordem.Should().Be(1);
        t.Exercicios[1].Ordem.Should().Be(2);
    }

    [Fact]
    public void AdicionarExercicio_SeriesZero_LancaDomainException()
    {
        var t = CriarTreino();
        var act = () => t.AdicionarExercicio(Guid.NewGuid(), 0, 12, null, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdicionarExercicio_RepeticoesZero_LancaDomainException()
    {
        var t = CriarTreino();
        var act = () => t.AdicionarExercicio(Guid.NewGuid(), 3, 0, null, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdicionarExercicio_CargaNegativa_LancaDomainException()
    {
        var t = CriarTreino();
        var act = () => t.AdicionarExercicio(Guid.NewGuid(), 3, 12, -1m, null);
        act.Should().Throw<DomainException>();
    }

    // --- RemoverExercicio ---

    [Fact]
    public void RemoverExercicio_ExercicioExistente_RemoveEReordena()
    {
        var t = CriarTreino();
        t.AdicionarExercicio(Guid.NewGuid(), 3, 12, null, null);
        t.AdicionarExercicio(Guid.NewGuid(), 4, 8, null, null);
        t.AdicionarExercicio(Guid.NewGuid(), 2, 15, null, null);

        t.RemoverExercicio(t.Exercicios[0].Id);

        t.Exercicios.Should().HaveCount(2);
        t.Exercicios[0].Ordem.Should().Be(1);
        t.Exercicios[1].Ordem.Should().Be(2);
    }

    [Fact]
    public void RemoverExercicio_IdInexistente_LancaDomainException()
    {
        var t = CriarTreino();
        var act = () => t.RemoverExercicio(Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    // --- Duplicar ---

    [Fact]
    public void Duplicar_CriaCopiaComExercicios()
    {
        var t = CriarTreino();
        t.AdicionarExercicio(Guid.NewGuid(), 3, 12, 10m, 60);

        var copia = t.Duplicar();

        copia.Id.Should().NotBe(t.Id);
        copia.Nome.Should().Be("Treino A (cópia)");
        copia.Objetivo.Should().Be(t.Objetivo);
        copia.TenantId.Should().Be(t.TenantId);
        copia.TreinadorId.Should().Be(t.TreinadorId);
        copia.Exercicios.Should().HaveCount(1);
        copia.Exercicios[0].Id.Should().NotBe(t.Exercicios[0].Id);
        copia.Exercicios[0].ExercicioId.Should().Be(t.Exercicios[0].ExercicioId);
    }

    [Fact]
    public void Duplicar_SemExercicios_CriaCopiaSemExercicios()
    {
        var t = CriarTreino();
        var copia = t.Duplicar();
        copia.Exercicios.Should().BeEmpty();
    }
}
