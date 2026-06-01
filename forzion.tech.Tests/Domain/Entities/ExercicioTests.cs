using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Tests.Builders;

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
        var e = Exercicio.Criar("Supino", GrupoMuscularId, TestData.Agora, TreinadorId).Value;
        e.Nome.Should().Be("Supino");
        e.GrupoMuscularId.Should().Be(GrupoMuscularId);
        e.TreinadorId.Should().Be(TreinadorId);
        e.IsGlobal.Should().BeFalse();
        e.Descricao.Should().BeNull();
    }

    [Fact]
    public void Criar_SemTreinador_RetornaExercicioGlobal()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscularId, TestData.Agora).Value;
        e.TreinadorId.Should().BeNull();
        e.IsGlobal.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var r = Exercicio.Criar(nome, GrupoMuscularId, TestData.Agora, TreinadorId);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var r = Exercicio.Criar(new string('a', 101), GrupoMuscularId, TestData.Agora, TreinadorId);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_GrupoMuscularVazio_LancaDomainException()
    {
        var r = Exercicio.Criar("Supino", Guid.Empty, TestData.Agora, TreinadorId);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O grupo muscular é obrigatório.");
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var r = Exercicio.Criar("Supino", GrupoMuscularId, TestData.Agora, Guid.Empty);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do treinador é inválido.");
    }

    [Fact]
    public void Criar_DescricaoMuitoLonga_LancaDomainException()
    {
        var r = Exercicio.Criar("Supino", GrupoMuscularId, TestData.Agora, TreinadorId, new string('a', 501));
        r.IsFailure.Should().BeTrue();
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_DadosValidos_AtualizaCampos()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscularId, TestData.Agora, TreinadorId).Value;
        e.Atualizar("Supino Reto", OutroGrupoMuscularId, "Descrição", TestData.Agora);
        e.Nome.Should().Be("Supino Reto");
        e.GrupoMuscularId.Should().Be(OutroGrupoMuscularId);
        e.Descricao.Should().Be("Descrição");
        e.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_DescricaoVazia_LimpaDescricao()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscularId, TestData.Agora, TreinadorId, "Desc").Value;
        e.Atualizar(null, null, "", TestData.Agora);
        e.Descricao.Should().BeNull();
    }

    [Fact]
    public void Atualizar_NomeVazio_LancaDomainException()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscularId, TestData.Agora, TreinadorId).Value;
        var r = e.Atualizar("", null, null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Atualizar_GrupoMuscularVazio_LancaDomainException()
    {
        var e = Exercicio.Criar("Supino", GrupoMuscularId, TestData.Agora, TreinadorId).Value;
        var r = e.Atualizar(null, Guid.Empty, null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O grupo muscular é obrigatório.");
    }
}
