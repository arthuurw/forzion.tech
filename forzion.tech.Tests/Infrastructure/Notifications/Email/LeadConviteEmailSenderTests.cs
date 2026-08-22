using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class LeadConviteEmailSenderTests
{
    private readonly Mock<IEmailService> _emailService = new();
    private readonly IOptions<AppSettings> _appSettings = Options.Create(new AppSettings { FrontendBaseUrl = "https://app.forzion.tech" });
    private readonly LeadConviteEmailSender _sender;

    public LeadConviteEmailSenderTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _emailService.Setup(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _sender = new LeadConviteEmailSender(_emailService.Object, _appSettings);
    }

    [Fact]
    public async Task EnviarAsync_ContatoEmailHabilitado_EnviaEDevolveTrue()
    {
        var enviado = await _sender.EnviarAsync(TipoContatoLead.Email, "fulano@lead.com", "Fulano", "Coach", "token-cru-xyz");

        enviado.Should().BeTrue();
        _emailService.Verify(e => e.EnviarAsync(
            "fulano@lead.com",
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("token-cru-xyz") && html.Contains("app.forzion.tech/cadastro/aluno")),
            It.IsAny<CancellationToken>(),
            It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task EnviarAsync_EmailDesabilitado_NaoEnviaEDevolveFalse()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(false);

        var enviado = await _sender.EnviarAsync(TipoContatoLead.Email, "fulano@lead.com", "Fulano", "Coach", "token-cru-xyz");

        enviado.Should().BeFalse();
        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task EnviarAsync_ContatoWhatsApp_NaoEnviaEDevolveFalse()
    {
        var enviado = await _sender.EnviarAsync(TipoContatoLead.WhatsApp, "+5511999998888", "Fulano", "Coach", "token-cru-xyz");

        enviado.Should().BeFalse();
        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task EnviarAsync_TokenCruNuncaAparaceEmLog()
    {
        // Sem logger injetado no sender — não há call-site de log aqui; a garantia é estrutural
        // (nenhuma dependência de ILogger no construtor).
        typeof(LeadConviteEmailSender).GetConstructors().Single().GetParameters()
            .Should().NotContain(p => p.ParameterType.Name.Contains("ILogger"));
    }
}
