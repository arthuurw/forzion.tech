using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.Entities;

public class UsuarioTests
{
    private static readonly Guid IdValido = Guid.NewGuid();
    private static readonly Guid TenantIdValido = Guid.NewGuid();
    private static readonly Email EmailValido = Email.Criar("user@example.com");

    // --- Criar ---

    [Fact]
    public void Criar_ComDadosValidos_RetornaUsuario()
    {
        var usuario = Usuario.Criar(IdValido, "João Silva", EmailValido, TenantIdValido);

        usuario.Id.Should().Be(IdValido);
        usuario.Nome.Should().Be("João Silva");
        usuario.Email.Should().Be(EmailValido);
        usuario.TenantId.Should().Be(TenantIdValido);
    }

    [Fact]
    public void Criar_DefinStatusAtivoERoleAdmin()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);

        usuario.Status.Should().Be(UsuarioStatus.Ativo);
        usuario.Role.Should().Be(Role.Admin);
    }

    [Fact]
    public void Criar_DefinCreatedAt()
    {
        var antes = DateTime.UtcNow;
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);

        usuario.CreatedAt.Should().BeOnOrAfter(antes).And.BeOnOrBefore(DateTime.UtcNow);
        usuario.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_ComRoleTrainer_DefinRoleTrainer()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido, Role.Trainer);
        usuario.Role.Should().Be(Role.Trainer);
    }

    [Fact]
    public void Criar_ComNomeComEspacos_RemoveEspacos()
    {
        var usuario = Usuario.Criar(IdValido, "  João  ", EmailValido, TenantIdValido);
        usuario.Nome.Should().Be("João");
    }

    [Fact]
    public void Criar_ComSupabaseIdVazio_LancaDomainException()
    {
        var act = () => Usuario.Criar(Guid.Empty, "João", EmailValido, TenantIdValido);
        act.Should().Throw<DomainException>().WithMessage("O identificador do usuário é inválido.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ComNomeVazio_LancaDomainException(string nome)
    {
        var act = () => Usuario.Criar(IdValido, nome, EmailValido, TenantIdValido);
        act.Should().Throw<DomainException>().WithMessage("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_ComNomeMuitoLongo_LancaDomainException()
    {
        var act = () => Usuario.Criar(IdValido, new string('a', 101), EmailValido, TenantIdValido);
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }

    [Fact]
    public void Criar_ComTenantIdVazio_LancaDomainException()
    {
        var act = () => Usuario.Criar(IdValido, "João", EmailValido, Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("O tenant é inválido.");
    }

    [Fact]
    public void Criar_ComEmailNulo_LancaArgumentNullException()
    {
        var act = () => Usuario.Criar(IdValido, "João", null!, TenantIdValido);
        act.Should().Throw<ArgumentNullException>();
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_ComNome_AtualizaNome()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        usuario.Atualizar("Maria", null, null);
        usuario.Nome.Should().Be("Maria");
    }

    [Fact]
    public void Atualizar_ComNomeComEspacos_RemoveEspacos()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        usuario.Atualizar("  Maria  ", null, null);
        usuario.Nome.Should().Be("Maria");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Atualizar_ComNomeVazio_LancaDomainException(string nome)
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        var act = () => usuario.Atualizar(nome, null, null);
        act.Should().Throw<DomainException>().WithMessage("O nome não pode ser vazio.");
    }

    [Fact]
    public void Atualizar_ComNomeMuitoLongo_LancaDomainException()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        var act = () => usuario.Atualizar(new string('a', 101), null, null);
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }

    [Fact]
    public void Atualizar_ComFotoUrl_AtualizaFotoUrl()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        usuario.Atualizar(null, "https://foto.com/img.jpg", null);
        usuario.FotoUrl.Should().Be("https://foto.com/img.jpg");
    }

    [Fact]
    public void Atualizar_ComFotoUrlVazia_LimpaFotoUrl()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        usuario.Atualizar(null, "", null);
        usuario.FotoUrl.Should().BeNull();
    }

    [Fact]
    public void Atualizar_ComFotoUrlMuitoLonga_LancaDomainException()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        var act = () => usuario.Atualizar(null, new string('a', 501), null);
        act.Should().Throw<DomainException>().WithMessage("A URL da foto deve ter no máximo 500 caracteres.");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://files.example.com/img.jpg")]
    [InlineData("not-a-url")]
    public void Atualizar_ComFotoUrlSchemeInvalido_LancaDomainException(string fotoUrl)
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        var act = () => usuario.Atualizar(null, fotoUrl, null);
        act.Should().Throw<DomainException>().WithMessage("A URL da foto deve ser uma URL válida (http ou https).");
    }

    [Theory]
    [InlineData("https://cdn.example.com/foto.jpg")]
    [InlineData("http://cdn.example.com/foto.jpg")]
    public void Atualizar_ComFotoUrlValida_Aceita(string fotoUrl)
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        usuario.Atualizar(null, fotoUrl, null);
        usuario.FotoUrl.Should().Be(fotoUrl);
    }

    [Fact]
    public void Atualizar_ComBio_AtualizaBio()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        usuario.Atualizar(null, null, "Minha bio");
        usuario.Bio.Should().Be("Minha bio");
    }

    [Fact]
    public void Atualizar_ComBioVazia_LimpaBio()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        usuario.Atualizar(null, null, "");
        usuario.Bio.Should().BeNull();
    }

    [Fact]
    public void Atualizar_ComBioMuitoLonga_LancaDomainException()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        var act = () => usuario.Atualizar(null, null, new string('a', 501));
        act.Should().Throw<DomainException>().WithMessage("A bio deve ter no máximo 500 caracteres.");
    }

    [Fact]
    public void Atualizar_ComTodosCamposNulos_NaoAlteraNada()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        var nomeOriginal = usuario.Nome;
        usuario.Atualizar(null, null, null);
        usuario.Nome.Should().Be(nomeOriginal);
        usuario.FotoUrl.Should().BeNull();
        usuario.Bio.Should().BeNull();
    }

    [Fact]
    public void Atualizar_DefinUpdatedAt()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        var antes = DateTime.UtcNow;
        usuario.Atualizar("Maria", null, null);
        usuario.UpdatedAt.Should().BeOnOrAfter(antes);
    }

    // --- AlterarStatus ---

    [Fact]
    public void AlterarStatus_ParaInativo_AlteraStatus()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        usuario.AlterarStatus(UsuarioStatus.Inativo);
        usuario.Status.Should().Be(UsuarioStatus.Inativo);
    }

    [Fact]
    public void AlterarStatus_DefinUpdatedAt()
    {
        var usuario = Usuario.Criar(IdValido, "João", EmailValido, TenantIdValido);
        var antes = DateTime.UtcNow;
        usuario.AlterarStatus(UsuarioStatus.Inativo);
        usuario.UpdatedAt.Should().BeOnOrAfter(antes);
    }
}
