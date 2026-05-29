using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class PlanoPlataformaTests
{
    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaPlano()
    {
        var plano = PlanoPlataforma.Criar("Plano Gold", TierPlano.Basic, 10, 99.90m, TestData.Agora);

        plano.Id.Should().NotBeEmpty();
        plano.Nome.Should().Be("Plano Gold");
        plano.Tier.Should().Be(TierPlano.Basic);
        plano.MaxAlunos.Should().Be(10);
        plano.Preco.Should().Be(99.90m);
        plano.IsAtivo.Should().BeTrue();
        plano.CreatedAt.Should().BeCloseTo(TestData.Agora, TimeSpan.FromSeconds(2));
        plano.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_NomeComEspacos_Remove()
    {
        var plano = PlanoPlataforma.Criar("  Gold  ", TierPlano.Basic, 5, 50m, TestData.Agora);
        plano.Nome.Should().Be("Gold");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => PlanoPlataforma.Criar(nome, TierPlano.Basic, 10, 99m, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var act = () => PlanoPlataforma.Criar(new string('a', 101), TierPlano.Basic, 10, 99m, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }

    [Fact]
    public void Criar_MaxAlunosZero_LancaDomainException()
    {
        var act = () => PlanoPlataforma.Criar("Gold", TierPlano.Basic, 0, 99m, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O limite de alunos deve ser maior que zero.");
    }

    [Fact]
    public void Criar_MaxAlunosNegativo_LancaDomainException()
    {
        var act = () => PlanoPlataforma.Criar("Gold", TierPlano.Basic, -1, 99m, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O limite de alunos deve ser maior que zero.");
    }

    [Fact]
    public void Criar_PrecoNegativo_LancaDomainException()
    {
        var act = () => PlanoPlataforma.Criar("Gold", TierPlano.Basic, 10, -0.01m, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O preço não pode ser negativo.");
    }

    [Fact]
    public void Criar_PrecoZero_Permitido()
    {
        var plano = PlanoPlataforma.Criar("Gold", TierPlano.Free, 10, 0m, TestData.Agora);
        plano.Preco.Should().Be(0m);
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_SoNome_AtualizaNomeESetaUpdatedAt()
    {
        var plano = PlanoPlataforma.Criar("Gold", TierPlano.Basic, 10, 99m, TestData.Agora);
        plano.Atualizar("Silver", null, null, null);

        plano.Nome.Should().Be("Silver");
        plano.MaxAlunos.Should().Be(10);
        plano.Preco.Should().Be(99m);
        plano.UpdatedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Atualizar_NomeVazio_LancaDomainException(string nome)
    {
        var plano = PlanoPlataforma.Criar("Gold", TierPlano.Basic, 10, 99m, TestData.Agora);
        var act = () => plano.Atualizar(nome, null, null, null);
        act.Should().Throw<DomainException>().WithMessage("O nome não pode ser vazio.");
    }

    [Fact]
    public void Atualizar_NomeMuitoLongo_LancaDomainException()
    {
        var plano = PlanoPlataforma.Criar("Gold", TierPlano.Basic, 10, 99m, TestData.Agora);
        var act = () => plano.Atualizar(new string('a', 101), null, null, null);
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }

    [Fact]
    public void Atualizar_SoMaxAlunos_AtualizaMaxAlunos()
    {
        var plano = PlanoPlataforma.Criar("Gold", TierPlano.Basic, 10, 99m, TestData.Agora);
        plano.Atualizar(null, null, 20, null);

        plano.MaxAlunos.Should().Be(20);
        plano.Nome.Should().Be("Gold");
    }

    [Fact]
    public void Atualizar_MaxAlunosInvalido_LancaDomainException()
    {
        var plano = PlanoPlataforma.Criar("Gold", TierPlano.Basic, 10, 99m, TestData.Agora);
        var act = () => plano.Atualizar(null, null, 0, null);
        act.Should().Throw<DomainException>().WithMessage("O limite de alunos deve ser maior que zero.");
    }

    [Fact]
    public void Atualizar_SoPreco_AtualizaPreco()
    {
        var plano = PlanoPlataforma.Criar("Gold", TierPlano.Basic, 10, 99m, TestData.Agora);
        plano.Atualizar(null, null, null, 150m);

        plano.Preco.Should().Be(150m);
    }

    [Fact]
    public void Atualizar_PrecoNegativo_LancaDomainException()
    {
        var plano = PlanoPlataforma.Criar("Gold", TierPlano.Basic, 10, 99m, TestData.Agora);
        var act = () => plano.Atualizar(null, null, null, -1m);
        act.Should().Throw<DomainException>().WithMessage("O preço não pode ser negativo.");
    }

    [Fact]
    public void Atualizar_TudoNull_SetaUpdatedAt()
    {
        var plano = PlanoPlataforma.Criar("Gold", TierPlano.Basic, 10, 99m, TestData.Agora);
        plano.Atualizar(null, null, null, null);

        plano.Nome.Should().Be("Gold");
        plano.MaxAlunos.Should().Be(10);
        plano.Preco.Should().Be(99m);
        plano.UpdatedAt.Should().NotBeNull();
    }

    // --- Inativar / Ativar ---

    [Fact]
    public void Inativar_MudaIsAtivoParaFalse()
    {
        var plano = PlanoPlataforma.Criar("Gold", TierPlano.Basic, 10, 99m, TestData.Agora);
        plano.Inativar();

        plano.IsAtivo.Should().BeFalse();
        plano.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Ativar_MudaIsAtivoParaTrue()
    {
        var plano = PlanoPlataforma.Criar("Gold", TierPlano.Basic, 10, 99m, TestData.Agora);
        plano.Inativar();
        plano.Ativar();

        plano.IsAtivo.Should().BeTrue();
        plano.UpdatedAt.Should().NotBeNull();
    }
}
