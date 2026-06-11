using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.AssinaturaAlunos.CancelarAssinaturaAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.AssinaturaAlunos;

public class CancelarAssinaturaAlunoHandlerTests
{
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CancelarAssinaturaAlunoHandler>> _logger = new();
    private readonly CancelarAssinaturaAlunoHandler _handler;

    public CancelarAssinaturaAlunoHandlerTests()
    {
        _handler = new CancelarAssinaturaAlunoHandler(
            _assinaturaRepo.Object, _unitOfWork.Object, TimeProvider.System, _logger.Object);
    }

    private static AssinaturaAluno CriarAssinaturaAluno() =>
        AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_AssinaturaAlunoAtiva_Cancela()
    {
        var assinatura = CriarAssinaturaAluno();
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new CancelarAssinaturaAlunoCommand(assinatura.Id));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAlunoJaCancelada_RetornaFailure()
    {
        var assinatura = CriarAssinaturaAluno();
        assinatura.Cancelar(DateTime.UtcNow);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new CancelarAssinaturaAlunoCommand(assinatura.Id));

        result.IsSuccess.Should().BeFalse();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAlunoNaoEncontrada_RetornaFailureNotFound()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        var result = await _handler.HandleAsync(new CancelarAssinaturaAlunoCommand(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assinatura_aluno.nao_encontrada");
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
