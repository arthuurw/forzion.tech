using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class GrupoMuscularTests
{
    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaGrupoMuscular()
    {
        var grupo = GrupoMuscular.Criar("Peito", TestData.Agora).Value;

        grupo.Id.Should().NotBeEmpty();
        grupo.Nome.Should().Be("Peito");
        grupo.CreatedAt.Should().BeCloseTo(TestData.Agora, TimeSpan.FromSeconds(2));
        grupo.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_NomeComEspacos_Remove()
    {
        var grupo = GrupoMuscular.Criar("  Costas  ", TestData.Agora).Value;
        grupo.Nome.Should().Be("Costas");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var r = GrupoMuscular.Criar(nome, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome do grupo muscular é obrigatório.");
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var r = GrupoMuscular.Criar(new string('a', 51), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome do grupo muscular deve ter no máximo 50 caracteres.");
    }

    [Fact]
    public void Criar_NomeExatamente50Chars_Permitido()
    {
        var grupo = GrupoMuscular.Criar(new string('a', 50), TestData.Agora).Value;
        grupo.Nome.Should().HaveLength(50);
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_DadosValidos_AtualizaNomeEUpdatedAt()
    {
        var grupo = GrupoMuscular.Criar("Peito", TestData.Agora).Value;
        grupo.Atualizar("Tríceps", TestData.Agora);

        grupo.Nome.Should().Be("Tríceps");
        grupo.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_NomeComEspacos_Remove()
    {
        var grupo = GrupoMuscular.Criar("Peito", TestData.Agora).Value;
        grupo.Atualizar("  Bíceps  ", TestData.Agora);
        grupo.Nome.Should().Be("Bíceps");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Atualizar_NomeVazio_LancaDomainException(string nome)
    {
        var grupo = GrupoMuscular.Criar("Peito", TestData.Agora).Value;
        var r = grupo.Atualizar(nome, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome do grupo muscular não pode ser vazio.");
    }

    [Fact]
    public void Atualizar_NomeMuitoLongo_LancaDomainException()
    {
        var grupo = GrupoMuscular.Criar("Peito", TestData.Agora).Value;
        var r = grupo.Atualizar(new string('a', 51), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome do grupo muscular deve ter no máximo 50 caracteres.");
    }
}
