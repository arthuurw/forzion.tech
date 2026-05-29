using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.ExcluirTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Treinadores;

public class ExcluirTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ILogger<ExcluirTreinadorHandler>> _logger = new();
    private readonly ExcluirTreinadorHandler _handler;

    public ExcluirTreinadorHandlerTests()
    {
        _handler = new ExcluirTreinadorHandler(_treinadorRepo.Object, _logger.Object);
    }

    private static Treinador CriarTreinadorInativo(Guid adminId)
    {
        var t = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        t.Inativar(TestData.Agora, adminId);
        return t;
    }

    [Fact]
    public async Task HandleAsync_TreinadorInativo_ExcluiComDependencias()
    {
        var adminId = Guid.NewGuid();
        var treinador = CriarTreinadorInativo(adminId);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new ExcluirTreinadorCommand(treinador.Id, adminId));

        result.IsSuccess.Should().BeTrue();
        _treinadorRepo.Verify(r => r.ExcluirComDependenciasAsync(treinador, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorAtivo_RetornaFalha()
    {
        var adminId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        treinador.Aprovar(adminId, TestData.Agora);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new ExcluirTreinadorCommand(treinador.Id, adminId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("inativos");
    }

    [Fact]
    public async Task HandleAsync_TreinadorAguardando_RetornaFalha()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new ExcluirTreinadorCommand(treinador.Id, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaTreinadorNaoEncontradoException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new ExcluirTreinadorCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
