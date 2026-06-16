using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class ContaRegistradaEmailHandlerTests
{
    private readonly Mock<EmailVerificationSender> _sender =
        new(null!, null!, null!, null!, null!);
    private readonly ContaRegistradaEmailHandler _handler;

    public ContaRegistradaEmailHandlerTests()
    {
        _sender.Setup(s => s.EnviarAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new ContaRegistradaEmailHandler(_sender.Object);
    }

    [Fact]
    public async Task HandleAsync_DelegaParaSenderComContaIdEEmail()
    {
        var evento = new ContaRegistradaEvent(Guid.NewGuid(), "novo@example.com", DateTime.UtcNow);

        await _handler.HandleAsync(evento);

        _sender.Verify(s => s.EnviarAsync(evento.ContaId, "novo@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EventoNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);

        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }
}
