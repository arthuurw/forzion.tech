using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using EmailVO = forzion.tech.Domain.ValueObjects.Email;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class TreinadorReprovadoEmailHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<TreinadorReprovadoEmailHandler>> _logger = new();
    private readonly TreinadorReprovadoEmailHandler _handler;

    private static readonly TreinadorReprovadoEvent Evento = new(
        Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

    public TreinadorReprovadoEmailHandlerTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new TreinadorReprovadoEmailHandler(
            _treinadorRepo.Object, _contaRepo.Object, _emailService.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitado_NaoEnvia()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(false);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_NaoEnvia()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContaNaoEncontrada_NaoEnvia()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Ana");
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(treinador.ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_HappyPath_EnviaEmailCorreto()
    {
        var conta = Conta.Criar(EmailVO.FromDatabase("ana@example.com"), "hash", TipoConta.Treinador);
        var treinador = Treinador.Criar(conta.Id, "Ana");

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(treinador.ContaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        await _handler.HandleAsync(Evento);

        _emailService.Verify(e => e.EnviarAsync(
            "ana@example.com",
            "Atualização do seu cadastro — forzion.tech",
            It.Is<string>(html => html.Contains("Ana")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
