using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Usuarios.AlterarStatusUsuario;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class AlterarStatusUsuarioHandlerTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<AlterarStatusUsuarioHandler>> _logger = new();
    private readonly AlterarStatusUsuarioHandler _handler;

    public AlterarStatusUsuarioHandlerTests()
    {
        _handler = new AlterarStatusUsuarioHandler(
            _usuarioRepo.Object,
            _alunoRepo.Object,
            _unitOfWork.Object,
            _logger.Object);
    }

    private static Usuario CriarUsuario(Guid? id = null, Role role = Role.Admin, UsuarioStatus status = UsuarioStatus.Ativo)
    {
        var plano = Plano.CriarComId(Guid.NewGuid(), "Free", 0, 5, true);
        var slug = Slug.FromNome("academia");
        var tenant = Tenant.Criar("Academia", slug, plano.Id);
        var email = Email.Criar("user@example.com");
        var usuario = Usuario.Criar(id ?? Guid.NewGuid(), "João", email, tenant.Id, role);
        typeof(Usuario).GetProperty("Tenant")!.SetValue(usuario, tenant);
        if (status == UsuarioStatus.Inativo)
            usuario.AlterarStatus(UsuarioStatus.Inativo);
        return usuario;
    }

    [Fact]
    public async Task HandleAsync_AdminAlteraStatus_RetornaResponse()
    {
        var admin = CriarUsuario(role: Role.Admin);
        var alvo = CriarUsuario();
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(alvo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(alvo);

        var result = await _handler.HandleAsync(new AlterarStatusUsuarioCommand(admin.Id, alvo.Id, UsuarioStatus.Inativo));

        result.Status.Should().Be(UsuarioStatus.Inativo);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TrainerTentaAlterar_LancaAcessoNegadoException()
    {
        var trainer = CriarUsuario(role: Role.Trainer);
        var alvo = CriarUsuario();
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(trainer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(trainer);

        var act = async () => await _handler.HandleAsync(
            new AlterarStatusUsuarioCommand(trainer.Id, alvo.Id, UsuarioStatus.Inativo));

        await act.Should().ThrowAsync<AcessoNegadoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AdminInativo_LancaUsuarioInativoException()
    {
        var admin = CriarUsuario(role: Role.Admin, status: UsuarioStatus.Inativo);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        var act = async () => await _handler.HandleAsync(
            new AlterarStatusUsuarioCommand(admin.Id, Guid.NewGuid(), UsuarioStatus.Inativo));

        await act.Should().ThrowAsync<UsuarioInativoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AdminNaoEncontrado_LancaUsuarioNaoEncontradoException()
    {
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        var act = async () => await _handler.HandleAsync(
            new AlterarStatusUsuarioCommand(Guid.NewGuid(), Guid.NewGuid(), UsuarioStatus.Inativo));

        await act.Should().ThrowAsync<UsuarioNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_UsuarioAlvoNaoEncontrado_LancaUsuarioNaoEncontradoException()
    {
        var admin = CriarUsuario(role: Role.Admin);
        var alvoId = Guid.NewGuid();
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(alvoId, It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);

        var act = async () => await _handler.HandleAsync(
            new AlterarStatusUsuarioCommand(admin.Id, alvoId, UsuarioStatus.Inativo));

        await act.Should().ThrowAsync<UsuarioNaoEncontradoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_InativarUsuario_InativaAlunosVinculados()
    {
        var admin = CriarUsuario(role: Role.Admin);
        var alvo = CriarUsuario();
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(alvo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(alvo);

        await _handler.HandleAsync(new AlterarStatusUsuarioCommand(admin.Id, alvo.Id, UsuarioStatus.Inativo));

        _alunoRepo.Verify(r => r.InativarPorTreinadorAsync(alvo.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AtivarUsuario_NaoInativaAlunos()
    {
        var admin = CriarUsuario(role: Role.Admin);
        var alvo = CriarUsuario(status: UsuarioStatus.Inativo);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(alvo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(alvo);

        await _handler.HandleAsync(new AlterarStatusUsuarioCommand(admin.Id, alvo.Id, UsuarioStatus.Ativo));

        _alunoRepo.Verify(r => r.InativarPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
