using FluentAssertions;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Services;

public class NullEmailServiceTests
{
    private readonly NullEmailService _service = new(Mock.Of<ILogger<NullEmailService>>());

    [Fact]
    public void Habilitado_RetornaFalse()
    {
        _service.Habilitado.Should().BeFalse();
    }

    [Fact]
    public async Task EnviarAsync_RetornaCompletedTask()
    {
        var act = async () => await _service.EnviarAsync("a@b.com", "Assunto", "<p>html</p>");
        await act.Should().NotThrowAsync();
    }
}
