using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class TreinoTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();

    private static Treino CriarTreino() =>
        Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, TreinadorId);

    private static TreinoExercicio AdicionarComSerie(Treino t, Guid? exercicioId = null)
    {
        var ex = t.AdicionarExercicio(exercicioId ?? Guid.NewGuid());
        ex.AdicionarSerie(3, 10, 12, null, null, null);
        return ex;
    }

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaTreino()
    {
        var t = CriarTreino();
        t.Nome.Should().Be("Treino A");
        t.Objetivo.Should().Be(ObjetivoTreino.Hipertrofia);
        t.TreinadorId.Should().Be(TreinadorId);
        t.Exercicios.Should().BeEmpty();
        t.UpdatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => Treino.Criar(nome, ObjetivoTreino.Hipertrofia, TreinadorId);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var act = () => Treino.Criar(new string('a', 101), ObjetivoTreino.Hipertrofia, TreinadorId);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var act = () => Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, Guid.Empty);
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

    // --- AdicionarExercicio / AdicionarSerie ---

    [Fact]
    public void AdicionarExercicio_DadosValidos_AdicionaOrdenado()
    {
        var t = CriarTreino();
        AdicionarComSerie(t);
        AdicionarComSerie(t);
        t.Exercicios.Should().HaveCount(2);
        t.Exercicios[0].Ordem.Should().Be(1);
        t.Exercicios[1].Ordem.Should().Be(2);
    }

    [Fact]
    public void AdicionarSerie_QuantidadeZero_LancaDomainException()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid());
        var act = () => ex.AdicionarSerie(0, 12, null, null, null, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdicionarSerie_RepeticoesMinZero_LancaDomainException()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid());
        var act = () => ex.AdicionarSerie(3, 0, null, null, null, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdicionarSerie_CargaNegativa_LancaDomainException()
    {
        var t = CriarTreino();
        var ex = t.AdicionarExercicio(Guid.NewGuid());
        var act = () => ex.AdicionarSerie(3, 10, null, null, -1m, null);
        act.Should().Throw<DomainException>();
    }

    // --- RemoverExercicio ---

    [Fact]
    public void RemoverExercicio_ExercicioExistente_RemoveEReordena()
    {
        var t = CriarTreino();
        AdicionarComSerie(t);
        AdicionarComSerie(t);
        AdicionarComSerie(t);

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
        var ex = t.AdicionarExercicio(Guid.NewGuid());
        ex.AdicionarSerie(3, 10, 12, "Trabalho", 10m, 60);

        var copia = t.Duplicar();

        copia.Id.Should().NotBe(t.Id);
        copia.Nome.Should().Be("Treino A (cópia)");
        copia.Objetivo.Should().Be(t.Objetivo);
        copia.TreinadorId.Should().Be(t.TreinadorId);
        copia.Exercicios.Should().HaveCount(1);
        copia.Exercicios[0].Id.Should().NotBe(t.Exercicios[0].Id);
        copia.Exercicios[0].ExercicioId.Should().Be(t.Exercicios[0].ExercicioId);
        copia.Exercicios[0].Series.Should().HaveCount(1);
    }

    [Fact]
    public void Duplicar_SemExercicios_CriaCopiaSemExercicios()
    {
        var t = CriarTreino();
        var copia = t.Duplicar();
        copia.Exercicios.Should().BeEmpty();
    }
}
