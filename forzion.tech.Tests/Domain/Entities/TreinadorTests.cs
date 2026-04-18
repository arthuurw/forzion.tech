using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class TreinadorTests
{
    private static readonly Guid ContaId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaTreinador()
    {
        var t = Treinador.Criar(ContaId, "Carlos");

        t.Id.Should().NotBeEmpty();
        t.ContaId.Should().Be(ContaId);
        t.Nome.Should().Be("Carlos");
        t.Status.Should().Be(TreinadorStatus.AguardandoAprovacao);
        t.PlanoTreinadorId.Should().BeNull();
        t.AprovadoPorId.Should().BeNull();
        t.AprovadoEm.Should().BeNull();
    }

    [Fact]
    public void Criar_NomeComEspacos_Remove()
    {
        var t = Treinador.Criar(ContaId, "  Carlos  ");
        t.Nome.Should().Be("Carlos");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => Treinador.Criar(ContaId, nome);
        act.Should().Throw<DomainException>().WithMessage("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_ContaIdVazio_LancaDomainException()
    {
        var act = () => Treinador.Criar(Guid.Empty, "Carlos");
        act.Should().Throw<DomainException>().WithMessage("O identificador da conta é inválido.");
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var act = () => Treinador.Criar(ContaId, new string('a', 101));
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }

    // --- Aprovar ---

    [Fact]
    public void Aprovar_AguardandoAprovacao_MudaParaAtivo()
    {
        var t = Treinador.Criar(ContaId, "Carlos");
        var adminId = Guid.NewGuid();

        t.Aprovar(adminId);

        t.Status.Should().Be(TreinadorStatus.Ativo);
        t.AprovadoPorId.Should().Be(adminId);
        t.AprovadoEm.Should().NotBeNull();
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Aprovar_JaAtivo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos");
        t.Aprovar(Guid.NewGuid());

        var act = () => t.Aprovar(Guid.NewGuid());
        act.Should().Throw<DomainException>().WithMessage("Apenas treinadores aguardando aprovação podem ser aprovados.");
    }

    // --- Inativar ---

    [Fact]
    public void Inativar_Ativo_MudaParaInativo()
    {
        var t = Treinador.Criar(ContaId, "Carlos");
        t.Aprovar(Guid.NewGuid());

        t.Inativar();

        t.Status.Should().Be(TreinadorStatus.Inativo);
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Inativar_JaInativo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos");
        t.Aprovar(Guid.NewGuid());
        t.Inativar();

        var act = () => t.Inativar();
        act.Should().Throw<DomainException>().WithMessage("O treinador já está inativo.");
    }

    // --- AtribuirPlano ---

    [Fact]
    public void AtribuirPlano_PlanoValido_AtribuiId()
    {
        var t = Treinador.Criar(ContaId, "Carlos");
        var planoId = Guid.NewGuid();

        t.AtribuirPlano(planoId);

        t.PlanoTreinadorId.Should().Be(planoId);
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void AtribuirPlano_IdVazio_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos");
        var act = () => t.AtribuirPlano(Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("O identificador do plano é inválido.");
    }
}
