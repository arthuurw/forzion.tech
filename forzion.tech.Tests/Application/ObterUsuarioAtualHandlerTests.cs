using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Usuarios.ObterUsuarioAtual;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class ObterUsuarioAtualHandlerTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo = new();
    private readonly Mock<ILogger<ObterUsuarioAtualHandler>> _logger = new();
    private readonly ObterUsuarioAtualHandler _handler;

    public ObterUsuarioAtualHandlerTests()
    {
        _handler = new ObterUsuarioAtualHandler(_usuarioRepo.Object, _logger.Object);
    }

    private static Usuario CriarUsuarioAtivo()
    {
        var plano = Plano.CriarComId(Guid.NewGuid(), "Free", 0, 5, true);
        var slug = Slug.FromNome("academia");
        var tenant = Tenant.Criar("Academia", slug, plano.Id);
        var email = Email.Criar("user@example.com");
        var usuario = Usuario.Criar(Guid.NewGuid(), "João", email, tenant.Id);
        typeof(Usuario).GetProperty("Tenant")!.SetValue(usuario, tenant);
        return usuario;
    }

    [Fact]
    public async Task HandleAsync_UsuarioExiste_RetornaResponse()
    {
        var usuario = CriarUsuarioAtivo();
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(usuario.Id, default))
            .ReturnsAsync(usuario);

        var result = await _handler.HandleAsync(new ObterUsuarioAtualQuery(usuario.Id));

        result.Should().NotBeNull();
        result.UsuarioId.Should().Be(usuario.Id);
        result.Nome.Should().Be(usuario.Nome);
        result.Email.Should().Be(usuario.Email.Value);
        result.Role.Should().Be(Role.Admin);
        result.Status.Should().Be(UsuarioStatus.Ativo);
    }

    [Fact]
    public async Task HandleAsync_UsuarioNaoEncontrado_LancaUsuarioNaoEncontradoException()
    {
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Usuario?)null);

        var act = async () => await _handler.HandleAsync(new ObterUsuarioAtualQuery(Guid.NewGuid()));

        await act.Should().ThrowAsync<UsuarioNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_UsuarioInativo_LancaUsuarioInativoException()
    {
        var usuario = CriarUsuarioAtivo();
        usuario.AlterarStatus(UsuarioStatus.Inativo);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(usuario.Id, default))
            .ReturnsAsync(usuario);

        var act = async () => await _handler.HandleAsync(new ObterUsuarioAtualQuery(usuario.Id));

        await act.Should().ThrowAsync<UsuarioInativoException>();
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
