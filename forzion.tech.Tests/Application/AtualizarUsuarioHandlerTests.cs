using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Usuarios.AtualizarUsuario;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class AtualizarUsuarioHandlerTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<AtualizarUsuarioHandler>> _logger = new();
    private readonly AtualizarUsuarioHandler _handler;

    public AtualizarUsuarioHandlerTests()
    {
        _handler = new AtualizarUsuarioHandler(
            _usuarioRepo.Object,
            _unitOfWork.Object,
            _logger.Object);
    }

    private static Usuario CriarUsuarioAtivo(Guid? id = null)
    {
        var plano = Plano.CriarComId(Guid.NewGuid(), "Free", 0, 5, true);
        var slug = Slug.FromNome("academia");
        var tenant = Tenant.Criar("Academia", slug, plano.Id);
        var email = Email.Criar("user@example.com");
        var usuario = Usuario.Criar(id ?? Guid.NewGuid(), "João", email, tenant.Id);
        typeof(Usuario).GetProperty("Tenant")!.SetValue(usuario, tenant);
        return usuario;
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_AtualizaERetornaResponse()
    {
        var usuario = CriarUsuarioAtivo();
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(usuario.Id, default)).ReturnsAsync(usuario);

        var command = new AtualizarUsuarioCommand(usuario.Id, "Maria", null, "Bio nova");
        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be("Maria");
        result.Bio.Should().Be("Bio nova");
        _unitOfWork.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ComCamposNulos_NaoAlteraCampos()
    {
        var usuario = CriarUsuarioAtivo();
        var nomeOriginal = usuario.Nome;
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(usuario.Id, default)).ReturnsAsync(usuario);

        var command = new AtualizarUsuarioCommand(usuario.Id, null, null, null);
        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be(nomeOriginal);
        result.FotoUrl.Should().BeNull();
        result.Bio.Should().BeNull();
        result.Status.Should().Be(UsuarioStatus.Ativo);
    }

    [Fact]
    public async Task HandleAsync_UsuarioNaoEncontrado_LancaUsuarioNaoEncontradoException()
    {
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Usuario?)null);

        var act = async () => await _handler.HandleAsync(
            new AtualizarUsuarioCommand(Guid.NewGuid(), "Maria", null, null));

        await act.Should().ThrowAsync<UsuarioNaoEncontradoException>();
        _unitOfWork.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UsuarioInativo_LancaUsuarioInativoException()
    {
        var usuario = CriarUsuarioAtivo();
        usuario.AlterarStatus(UsuarioStatus.Inativo);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(usuario.Id, default)).ReturnsAsync(usuario);

        var act = async () => await _handler.HandleAsync(
            new AtualizarUsuarioCommand(usuario.Id, "Maria", null, null));

        await act.Should().ThrowAsync<UsuarioInativoException>();
        _unitOfWork.Verify(u => u.CommitAsync(default), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
