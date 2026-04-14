using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.Entities;

public class TenantTests
{
    private static readonly Slug SlugValido = Slug.FromNome("academia");
    private static readonly Guid PlanoIdValido = Guid.NewGuid();

    [Fact]
    public void Criar_ComDadosValidos_RetornaTenant()
    {
        var tenant = Tenant.Criar("Academia", SlugValido, PlanoIdValido);

        tenant.Nome.Should().Be("Academia");
        tenant.Slug.Should().Be(SlugValido);
        tenant.PlanoId.Should().Be(PlanoIdValido);
    }

    [Fact]
    public void Criar_GeraNovoGuid()
    {
        var t1 = Tenant.Criar("Academia", SlugValido, PlanoIdValido);
        var t2 = Tenant.Criar("Academia", SlugValido, PlanoIdValido);
        t1.Id.Should().NotBe(t2.Id);
    }

    [Fact]
    public void Criar_DefinCreatedAt()
    {
        var antes = DateTime.UtcNow;
        var tenant = Tenant.Criar("Academia", SlugValido, PlanoIdValido);
        tenant.CreatedAt.Should().BeOnOrAfter(antes);
        tenant.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_ComNomeComEspacos_RemoveEspacos()
    {
        var tenant = Tenant.Criar("  Academia  ", SlugValido, PlanoIdValido);
        tenant.Nome.Should().Be("Academia");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComNomeVazio_LancaDomainException(string nome)
    {
        var act = () => Tenant.Criar(nome, SlugValido, PlanoIdValido);
        act.Should().Throw<DomainException>().WithMessage("O nome do tenant é obrigatório.");
    }

    [Fact]
    public void Criar_ComNomeMuitoLongo_LancaDomainException()
    {
        var act = () => Tenant.Criar(new string('a', 101), SlugValido, PlanoIdValido);
        act.Should().Throw<DomainException>().WithMessage("O nome do tenant deve ter no máximo 100 caracteres.");
    }

    [Fact]
    public void Criar_ComPlanoIdVazio_LancaDomainException()
    {
        var act = () => Tenant.Criar("Academia", SlugValido, Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("O plano é inválido.");
    }

    [Fact]
    public void Criar_ComSlugNulo_LancaArgumentNullException()
    {
        var act = () => Tenant.Criar("Academia", null!, PlanoIdValido);
        act.Should().Throw<ArgumentNullException>();
    }
}
