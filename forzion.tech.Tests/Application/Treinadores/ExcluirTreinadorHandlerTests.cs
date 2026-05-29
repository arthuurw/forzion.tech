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

        await _handler.HandleAsync(new ExcluirTreinadorCommand(treinador.Id, adminId));

        _treinadorRepo.Verify(r => r.ExcluirComDependenciasAsync(treinador, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorAtivo_LancaDomainException()
    {
        var adminId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        treinador.Aprovar(adminId, TestData.Agora);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(new ExcluirTreinadorCommand(treinador.Id, adminId));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*inativos*");
    }

    [Fact]
    public async Task HandleAsync_TreinadorAguardando_LancaDomainException()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(new ExcluirTreinadorCommand(treinador.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>();
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
