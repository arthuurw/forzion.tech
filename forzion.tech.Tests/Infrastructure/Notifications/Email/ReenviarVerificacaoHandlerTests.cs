using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Notifications.Email;
using EmailVO = forzion.tech.Domain.ValueObjects.Email;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class ReenviarVerificacaoHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<EmailVerificationSender> _sender =
        new(null!, null!, null!, null!, null!);
    private readonly ReenviarVerificacaoHandler _handler;

    public ReenviarVerificacaoHandlerTests()
    {
        _sender.Setup(s => s.EnviarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new ReenviarVerificacaoHandler(
            _contaRepo.Object, _sender.Object, Mock.Of<ILogger<ReenviarVerificacaoHandler>>());
    }

    private static void VerifySenderNunca(Mock<EmailVerificationSender> sender) =>
        sender.Verify(s => s.EnviarAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task HandleAsync_ContaInexistente_NaoEnvia()
    {
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(new ReenviarVerificacaoCommand("ausente@example.com"));

        VerifySenderNunca(_sender);
    }

    [Fact]
    public async Task HandleAsync_ContaJaVerificada_NaoEnvia()
    {
        var conta = Conta.Criar(EmailVO.Criar("ja@example.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        conta.MarcarEmailVerificado(DateTime.UtcNow);
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        await _handler.HandleAsync(new ReenviarVerificacaoCommand("ja@example.com"));

        VerifySenderNunca(_sender);
    }

    [Fact]
    public async Task HandleAsync_ContaNaoVerificada_Envia()
    {
        var conta = Conta.Criar(EmailVO.Criar("pendente@example.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        await _handler.HandleAsync(new ReenviarVerificacaoCommand("pendente@example.com"));

        _sender.Verify(s => s.EnviarAsync(conta.Id, conta.Email.Value, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NormalizaEmailAntesDeBuscar()
    {
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(new ReenviarVerificacaoCommand("  USER@Example.COM  "));

        _contaRepo.Verify(r => r.ObterPorEmailAsync("user@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }
}
