using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.Entities;

public class SystemUserTests
{
    private static readonly Guid SupabaseIdValido = Guid.NewGuid();
    private static readonly Email EmailValido = Email.Criar("admin@forzion.tech");

    [Fact]
    public void Criar_ComDadosValidos_RetornaSystemUser()
    {
        var su = SystemUser.Criar(SupabaseIdValido, "Admin", EmailValido);

        su.Id.Should().NotBeEmpty();
        su.SupabaseId.Should().Be(SupabaseIdValido);
        su.Nome.Should().Be("Admin");
        su.Email.Should().Be(EmailValido);
        su.Role.Should().Be(SystemRole.SuperAdmin);
        su.Status.Should().Be(UsuarioStatus.Ativo);
        su.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        su.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_ComRoleSupport_DefinRoleSupport()
    {
        var su = SystemUser.Criar(SupabaseIdValido, "Support", EmailValido, SystemRole.Support);
        su.Role.Should().Be(SystemRole.Support);
    }

    [Fact]
    public void Criar_ComSupabaseIdVazio_LancaDomainException()
    {
        var act = () => SystemUser.Criar(Guid.Empty, "Admin", EmailValido);
        act.Should().Throw<DomainException>().WithMessage("O identificador do usuário é inválido.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComNomeVazio_LancaDomainException(string nome)
    {
        var act = () => SystemUser.Criar(SupabaseIdValido, nome, EmailValido);
        act.Should().Throw<DomainException>().WithMessage("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_ComNomeMuitoLongo_LancaDomainException()
    {
        var act = () => SystemUser.Criar(SupabaseIdValido, new string('a', 101), EmailValido);
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }

    [Fact]
    public void Criar_ComEmailNulo_LancaArgumentNullException()
    {
        var act = () => SystemUser.Criar(SupabaseIdValido, "Admin", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AlterarRole_AtualizaRoleEUpdatedAt()
    {
        var su = SystemUser.Criar(SupabaseIdValido, "Admin", EmailValido);
        var antes = DateTime.UtcNow;
        su.AlterarRole(SystemRole.Support);
        su.Role.Should().Be(SystemRole.Support);
        su.UpdatedAt.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public void AlterarStatus_AtualizaStatusEUpdatedAt()
    {
        var su = SystemUser.Criar(SupabaseIdValido, "Admin", EmailValido);
        var antes = DateTime.UtcNow;
        su.AlterarStatus(UsuarioStatus.Inativo);
        su.Status.Should().Be(UsuarioStatus.Inativo);
        su.UpdatedAt.Should().BeOnOrAfter(antes);
    }
}
