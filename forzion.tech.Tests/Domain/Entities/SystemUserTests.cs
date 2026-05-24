using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class SystemUserTests
{
    private static readonly Guid ContaId = Guid.NewGuid();

    [Fact]
    public void Criar_ComDadosValidos_RetornaSystemUser()
    {
        var su = SystemUser.Criar(ContaId, "Admin", DateTime.UtcNow);

        su.Id.Should().NotBeEmpty();
        su.ContaId.Should().Be(ContaId);
        su.Nome.Should().Be("Admin");
        su.Role.Should().Be(SystemRole.SuperAdmin);
        su.Status.Should().Be(UsuarioStatus.Ativo);
        su.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        su.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_ComRoleSupport_DefinRoleSupport()
    {
        var su = SystemUser.Criar(ContaId, "Support", DateTime.UtcNow, SystemRole.Support);
        su.Role.Should().Be(SystemRole.Support);
    }

    [Fact]
    public void Criar_ComContaIdVazio_LancaDomainException()
    {
        var act = () => SystemUser.Criar(Guid.Empty, "Admin", DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("O identificador da conta é inválido.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComNomeVazio_LancaDomainException(string nome)
    {
        var act = () => SystemUser.Criar(ContaId, nome, DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_ComNomeMuitoLongo_LancaDomainException()
    {
        var act = () => SystemUser.Criar(ContaId, new string('a', 101), DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }

    [Fact]
    public void AlterarRole_AtualizaRoleEUpdatedAt()
    {
        var su = SystemUser.Criar(ContaId, "Admin", DateTime.UtcNow);
        var antes = DateTime.UtcNow;
        su.AlterarRole(SystemRole.Support);
        su.Role.Should().Be(SystemRole.Support);
        su.UpdatedAt.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public void AlterarStatus_AtualizaStatusEUpdatedAt()
    {
        var su = SystemUser.Criar(ContaId, "Admin", DateTime.UtcNow);
        var antes = DateTime.UtcNow;
        su.AlterarStatus(UsuarioStatus.Inativo);
        su.Status.Should().Be(UsuarioStatus.Inativo);
        su.UpdatedAt.Should().BeOnOrAfter(antes);
    }

    // --- AtualizarNome ---

    [Fact]
    public void AtualizarNome_DadosValidos_AtualizaNomeEUpdatedAt()
    {
        var su = SystemUser.Criar(ContaId, "Admin", DateTime.UtcNow);
        su.AtualizarNome("  SuperAdmin  ");
        su.Nome.Should().Be("SuperAdmin");
        su.UpdatedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AtualizarNome_NomeVazio_LancaDomainException(string nome)
    {
        var su = SystemUser.Criar(ContaId, "Admin", DateTime.UtcNow);
        var act = () => su.AtualizarNome(nome);
        act.Should().Throw<DomainException>().WithMessage("O nome é obrigatório.");
    }

    [Fact]
    public void AtualizarNome_NomeMuitoLongo_LancaDomainException()
    {
        var su = SystemUser.Criar(ContaId, "Admin", DateTime.UtcNow);
        var act = () => su.AtualizarNome(new string('a', 101));
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }
}
