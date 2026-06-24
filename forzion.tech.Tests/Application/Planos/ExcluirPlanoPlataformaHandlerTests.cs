using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Planos.ExcluirPlanoPlataforma;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Planos;

public class ExcluirPlanoPlataformaHandlerTests
{
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly ExcluirPlanoPlataformaHandler _handler;

    private static readonly Guid AtorId = Guid.NewGuid();

    public ExcluirPlanoPlataformaHandlerTests()
    {
        _userContext.Setup(u => u.PerfilId).Returns(AtorId);
        _handler = new ExcluirPlanoPlataformaHandler(
            _planoRepo.Object,
            _unitOfWork.Object,
            _timeProvider,
            _logRepo.Object,
            Mock.Of<ILogger<ExcluirPlanoPlataformaHandler>>(),
            _userContext.Object);
    }

    [Fact]
    public async Task HandleAsync_PlanoExiste_InativaEComita()
    {
        var plano = PlanoPlataforma.Criar("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 99.90m, DateTime.UtcNow).Value;
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);

        var result = await _handler.HandleAsync(new ExcluirPlanoPlataformaCommand(plano.Id));

        result.IsSuccess.Should().BeTrue();
        plano.IsAtivo.Should().BeFalse();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PlanoExiste_RegistraLogAuditoria()
    {
        var plano = PlanoPlataforma.Criar("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 99.90m, DateTime.UtcNow).Value;
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);

        await _handler.HandleAsync(new ExcluirPlanoPlataformaCommand(plano.Id));

        _logRepo.Verify(r => r.AdicionarAsync(
            It.Is<LogAprovacao>(l => l.TipoAcao == TipoAcaoAprovacao.InativacaoPlanoPlataforma),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PlanoNaoEncontrado_RetornaFailureNotFound()
    {
        _planoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PlanoPlataforma?)null);

        var result = await _handler.HandleAsync(new ExcluirPlanoPlataformaCommand(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("plano_nao_encontrado");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_PlanoNaoEncontrado_NaoComita()
    {
        _planoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PlanoPlataforma?)null);

        await _handler.HandleAsync(new ExcluirPlanoPlataformaCommand(Guid.NewGuid()));

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
