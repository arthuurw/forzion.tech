using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class GrupoMuscularTests
{
    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaGrupoMuscular()
    {
        var grupo = GrupoMuscular.Criar("Peito");

        grupo.Id.Should().NotBeEmpty();
        grupo.Nome.Should().Be("Peito");
        grupo.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        grupo.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_NomeComEspacos_Remove()
    {
        var grupo = GrupoMuscular.Criar("  Costas  ");
        grupo.Nome.Should().Be("Costas");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => GrupoMuscular.Criar(nome);
        act.Should().Throw<DomainException>().WithMessage("O nome do grupo muscular é obrigatório.");
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var act = () => GrupoMuscular.Criar(new string('a', 51));
        act.Should().Throw<DomainException>().WithMessage("O nome do grupo muscular deve ter no máximo 50 caracteres.");
    }

    [Fact]
    public void Criar_NomeExatamente50Chars_Permitido()
    {
        var grupo = GrupoMuscular.Criar(new string('a', 50));
        grupo.Nome.Should().HaveLength(50);
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_DadosValidos_AtualizaNomeEUpdatedAt()
    {
        var grupo = GrupoMuscular.Criar("Peito");
        grupo.Atualizar("Tríceps");

        grupo.Nome.Should().Be("Tríceps");
        grupo.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_NomeComEspacos_Remove()
    {
        var grupo = GrupoMuscular.Criar("Peito");
        grupo.Atualizar("  Bíceps  ");
        grupo.Nome.Should().Be("Bíceps");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Atualizar_NomeVazio_LancaDomainException(string nome)
    {
        var grupo = GrupoMuscular.Criar("Peito");
        var act = () => grupo.Atualizar(nome);
        act.Should().Throw<DomainException>().WithMessage("O nome do grupo muscular não pode ser vazio.");
    }

    [Fact]
    public void Atualizar_NomeMuitoLongo_LancaDomainException()
    {
        var grupo = GrupoMuscular.Criar("Peito");
        var act = () => grupo.Atualizar(new string('a', 51));
        act.Should().Throw<DomainException>().WithMessage("O nome do grupo muscular deve ter no máximo 50 caracteres.");
    }
}
