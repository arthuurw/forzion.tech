using FluentAssertions;
using forzion.tech.Infrastructure.Notifications.WhatsApp;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.WhatsApp;

public class NullWhatsAppNotifierTests
{
    private readonly NullWhatsAppNotifier _notifier = new(Mock.Of<ILogger<NullWhatsAppNotifier>>());

    [Fact]
    public async Task SendAsync_NaoLancaExcecao()
    {
        var act = async () => await _notifier.SendAsync("5511999999999", "qualquer mensagem");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_RetornaCompletedTask()
    {
        var task = _notifier.SendAsync("5511999999999", "qualquer mensagem");
        await task;
        task.IsCompletedSuccessfully.Should().BeTrue();
    }
}
