using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.ObterTreino;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class ObterTreinoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<ILogger<ObterTreinoHandler>> _logger = new();
    private readonly ObterTreinoHandler _handler;

    public ObterTreinoHandlerTests()
    {
        _handler = new ObterTreinoHandler(_treinoRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinoExistente_RetornaTreino()
    {
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var result = await _handler.HandleAsync(new ObterTreinoQuery(treino.Id));

        result.TreinoId.Should().Be(treino.Id);
        result.Nome.Should().Be("Treino A");
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        var treinoId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treinoId, It.IsAny<CancellationToken>())).ReturnsAsync((Treino?)null);

        var act = async () => await _handler.HandleAsync(new ObterTreinoQuery(treinoId));

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
