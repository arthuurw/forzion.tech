using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class ExercicioTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_ComTreinador_RetornaExercicio()
    {
        var e = Exercicio.Criar("Supino", forzion.tech.Domain.Enums.TipoGrupoMuscular.Peito, TreinadorId);
        e.Nome.Should().Be("Supino");
        e.GrupoMuscular.Should().Be(forzion.tech.Domain.Enums.TipoGrupoMuscular.Peito);
        e.TreinadorId.Should().Be(TreinadorId);
        e.IsGlobal.Should().BeFalse();
        e.Descricao.Should().BeNull();
    }

    [Fact]
    public void Criar_SemTreinador_RetornaExercicioGlobal()
    {
        var e = Exercicio.Criar("Supino", forzion.tech.Domain.Enums.TipoGrupoMuscular.Peito);
        e.TreinadorId.Should().BeNull();
        e.IsGlobal.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => Exercicio.Criar(nome, forzion.tech.Domain.Enums.TipoGrupoMuscular.Peito, TreinadorId);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var act = () => Exercicio.Criar(new string('a', 101), forzion.tech.Domain.Enums.TipoGrupoMuscular.Peito, TreinadorId);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var act = () => Exercicio.Criar("Supino", forzion.tech.Domain.Enums.TipoGrupoMuscular.Peito, Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("O identificador do treinador é inválido.");
    }

    [Fact]
    public void Criar_DescricaoMuitoLonga_LancaDomainException()
    {
        var act = () => Exercicio.Criar("Supino", forzion.tech.Domain.Enums.TipoGrupoMuscular.Peito, TreinadorId, new string('a', 501));
        act.Should().Throw<DomainException>();
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_DadosValidos_AtualizaCampos()
    {
        var e = Exercicio.Criar("Supino", forzion.tech.Domain.Enums.TipoGrupoMuscular.Peito, TreinadorId);
        e.Atualizar("Supino Reto", forzion.tech.Domain.Enums.TipoGrupoMuscular.Triceps, "Descrição");
        e.Nome.Should().Be("Supino Reto");
        e.GrupoMuscular.Should().Be(forzion.tech.Domain.Enums.TipoGrupoMuscular.Triceps);
        e.Descricao.Should().Be("Descrição");
        e.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_DescricaoVazia_LimpaDescricao()
    {
        var e = Exercicio.Criar("Supino", forzion.tech.Domain.Enums.TipoGrupoMuscular.Peito, TreinadorId, "Desc");
        e.Atualizar(null, null, "");
        e.Descricao.Should().BeNull();
    }

    [Fact]
    public void Atualizar_NomeVazio_LancaDomainException()
    {
        var e = Exercicio.Criar("Supino", forzion.tech.Domain.Enums.TipoGrupoMuscular.Peito, TreinadorId);
        var act = () => e.Atualizar("", null, null);
        act.Should().Throw<DomainException>();
    }
}
