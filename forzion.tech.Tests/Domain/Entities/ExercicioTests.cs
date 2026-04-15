using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class ExercicioTests
{
    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaExercicio()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscular.Peito, Guid.NewGuid());
        e.Nome.Should().Be("Supino");
        e.GrupoMuscular.Should().Be(GrupoMuscular.Peito);
        e.Descricao.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => Exercicio.Criar(nome, GrupoMuscular.Peito, Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var act = () => Exercicio.Criar(new string('a', 101), GrupoMuscular.Peito, Guid.NewGuid());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_TenantIdVazio_LancaDomainException()
    {
        var act = () => Exercicio.Criar("Supino", GrupoMuscular.Peito, Guid.Empty);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_DescricaoMuitoLonga_LancaDomainException()
    {
        var act = () => Exercicio.Criar("Supino", GrupoMuscular.Peito, Guid.NewGuid(), new string('a', 501));
        act.Should().Throw<DomainException>();
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_DadosValidos_AtualizaCampos()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscular.Peito, Guid.NewGuid());
        e.Atualizar("Supino Reto", GrupoMuscular.Triceps, "Descrição");
        e.Nome.Should().Be("Supino Reto");
        e.GrupoMuscular.Should().Be(GrupoMuscular.Triceps);
        e.Descricao.Should().Be("Descrição");
        e.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_DescricaoVazia_LimpaDescricao()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscular.Peito, Guid.NewGuid(), "Desc");
        e.Atualizar(null, null, "");
        e.Descricao.Should().BeNull();
    }

    [Fact]
    public void Atualizar_NomeVazio_LancaDomainException()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscular.Peito, Guid.NewGuid());
        var act = () => e.Atualizar("", null, null);
        act.Should().Throw<DomainException>();
    }
}
