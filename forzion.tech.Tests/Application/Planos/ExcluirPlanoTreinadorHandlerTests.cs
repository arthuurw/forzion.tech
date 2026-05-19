using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Planos.ExcluirPlanoTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Planos;

public class ExcluirPlanoTreinadorHandlerTests
{
    private readonly Mock<IPlanoTreinadorRepository> _planoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ExcluirPlanoTreinadorHandler _handler;

    public ExcluirPlanoTreinadorHandlerTests()
    {
        _handler = new ExcluirPlanoTreinadorHandler(_planoRepo.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task HandleAsync_PlanoExiste_InativaEComita()
    {
        var plano = PlanoTreinador.Criar("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 99.90m);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);

        await _handler.HandleAsync(new ExcluirPlanoTreinadorCommand(plano.Id));

        plano.IsAtivo.Should().BeFalse();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PlanoNaoEncontrado_LancaDomainException()
    {
        _planoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PlanoTreinador?)null);

        var act = async () => await _handler.HandleAsync(new ExcluirPlanoTreinadorCommand(Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task HandleAsync_PlanoNaoEncontrado_NaoComita()
    {
        _planoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PlanoTreinador?)null);

        try { await _handler.HandleAsync(new ExcluirPlanoTreinadorCommand(Guid.NewGuid())); } catch { }

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
