using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Assinaturas.CancelarAssinatura;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Assinaturas;

public class CancelarAssinaturaHandlerTests
{
    private readonly Mock<IAssinaturaRepository> _assinaturaRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CancelarAssinaturaHandler>> _logger = new();
    private readonly CancelarAssinaturaHandler _handler;

    public CancelarAssinaturaHandlerTests()
    {
        _handler = new CancelarAssinaturaHandler(
            _assinaturaRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    private static Assinatura CriarAssinatura() =>
        Assinatura.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m);

    [Fact]
    public async Task HandleAsync_AssinaturaAtiva_Cancela()
    {
        var assinatura = CriarAssinatura();
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new CancelarAssinaturaCommand(assinatura.Id));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaJaCancelada_RetornaFailure()
    {
        var assinatura = CriarAssinatura();
        assinatura.Cancelar();
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new CancelarAssinaturaCommand(assinatura.Id));

        result.IsSuccess.Should().BeFalse();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaNaoEncontrada_LancaDomainException()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Assinatura?)null);

        var act = async () => await _handler.HandleAsync(new CancelarAssinaturaCommand(Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>().WithMessage("Assinatura não encontrada.");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
