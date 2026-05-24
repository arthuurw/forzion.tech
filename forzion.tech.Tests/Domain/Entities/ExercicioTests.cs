using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class ExercicioTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid GrupoMuscularId = Guid.NewGuid();
    private static readonly Guid OutroGrupoMuscularId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_ComTreinador_RetornaExercicio()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscularId, TreinadorId);
        e.Nome.Should().Be("Supino");
        e.GrupoMuscularId.Should().Be(GrupoMuscularId);
        e.TreinadorId.Should().Be(TreinadorId);
        e.IsGlobal.Should().BeFalse();
        e.Descricao.Should().BeNull();
    }

    [Fact]
    public void Criar_SemTreinador_RetornaExercicioGlobal()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscularId);
        e.TreinadorId.Should().BeNull();
        e.IsGlobal.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => Exercicio.Criar(nome, GrupoMuscularId, TreinadorId);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var act = () => Exercicio.Criar(new string('a', 101), GrupoMuscularId, TreinadorId);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Criar_GrupoMuscularVazio_LancaDomainException()
    {
        var act = () => Exercicio.Criar("Supino", Guid.Empty, TreinadorId);
        act.Should().Throw<DomainException>().WithMessage("O grupo muscular é obrigatório.");
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var act = () => Exercicio.Criar("Supino", GrupoMuscularId, Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("O identificador do treinador é inválido.");
    }

    [Fact]
    public void Criar_DescricaoMuitoLonga_LancaDomainException()
    {
        var act = () => Exercicio.Criar("Supino", GrupoMuscularId, TreinadorId, new string('a', 501));
        act.Should().Throw<DomainException>();
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_DadosValidos_AtualizaCampos()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscularId, TreinadorId);
        e.Atualizar("Supino Reto", OutroGrupoMuscularId, "Descrição");
        e.Nome.Should().Be("Supino Reto");
        e.GrupoMuscularId.Should().Be(OutroGrupoMuscularId);
        e.Descricao.Should().Be("Descrição");
        e.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_DescricaoVazia_LimpaDescricao()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscularId, TreinadorId, "Desc");
        e.Atualizar(null, null, "");
        e.Descricao.Should().BeNull();
    }

    [Fact]
    public void Atualizar_NomeVazio_LancaDomainException()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscularId, TreinadorId);
        var act = () => e.Atualizar("", null, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Atualizar_GrupoMuscularVazio_LancaDomainException()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscularId, TreinadorId);
        var act = () => e.Atualizar(null, Guid.Empty, null);
        act.Should().Throw<DomainException>().WithMessage("O grupo muscular é obrigatório.");
    }
}
