using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class AlterarStatusAlunoHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IUsuarioRepository> _usuarioRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<AlterarStatusAlunoHandler>> _logger = new();
    private readonly AlterarStatusAlunoHandler _handler;

    public AlterarStatusAlunoHandlerTests()
    {
        _handler = new AlterarStatusAlunoHandler(
            _alunoRepo.Object, _usuarioRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    private static Usuario CriarAdmin(Guid tenantId)
    {
        var email = Email.Criar("admin@t.com");
        return Usuario.Criar(Guid.NewGuid(), "Admin", email, tenantId, Role.Admin);
    }

    [Fact]
    public async Task HandleAsync_AdminAlteraStatus_Retorna()
    {
        var tenantId = Guid.NewGuid();
        var admin = CriarAdmin(tenantId);
        var aluno = Aluno.Criar("João", tenantId, Guid.NewGuid());
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var result = await _handler.HandleAsync(
            new AlterarStatusAlunoCommand(tenantId, admin.Id, aluno.Id, AlunoStatus.Inativo));

        result.Status.Should().Be(AlunoStatus.Inativo);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TrainerTentaAlterar_LancaAcessoNegadoException()
    {
        var tenantId = Guid.NewGuid();
        var trainer = Usuario.Criar(Guid.NewGuid(), "Trainer", Email.Criar("t@t.com"), tenantId, Role.Trainer);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(trainer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(trainer);

        var act = async () => await _handler.HandleAsync(
            new AlterarStatusAlunoCommand(tenantId, trainer.Id, Guid.NewGuid(), AlunoStatus.Inativo));

        await act.Should().ThrowAsync<AcessoNegadoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AdminDeOutroTenant_LancaAcessoNegadoException()
    {
        var admin = CriarAdmin(Guid.NewGuid());
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        var act = async () => await _handler.HandleAsync(
            new AlterarStatusAlunoCommand(Guid.NewGuid(), admin.Id, Guid.NewGuid(), AlunoStatus.Inativo));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoDeOutroTenant_LancaAcessoNegadoException()
    {
        var tenantId = Guid.NewGuid();
        var admin = CriarAdmin(tenantId);
        var aluno = Aluno.Criar("João", Guid.NewGuid(), Guid.NewGuid());
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var act = async () => await _handler.HandleAsync(
            new AlterarStatusAlunoCommand(tenantId, admin.Id, aluno.Id, AlunoStatus.Inativo));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_LancaAlunoNaoEncontradoException()
    {
        var tenantId = Guid.NewGuid();
        var admin = CriarAdmin(tenantId);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        var act = async () => await _handler.HandleAsync(
            new AlterarStatusAlunoCommand(tenantId, admin.Id, Guid.NewGuid(), AlunoStatus.Inativo));

        await act.Should().ThrowAsync<AlunoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
