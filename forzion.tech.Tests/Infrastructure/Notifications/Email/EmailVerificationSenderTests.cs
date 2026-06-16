using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class EmailVerificationSenderTests
{
    private static readonly DateTime Agora = new(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();
    private const string EmailDestino = "aluno@example.com";

    private readonly Mock<IEmailVerificationTokenRepository> _tokenRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IEmailBackgroundDispatcher> _dispatcher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(Agora);
    private readonly EmailVerificationSender _sender;

    public EmailVerificationSenderTests()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(true);
        _dispatcher.Setup(d => d.Disparar(It.IsAny<Func<IEmailService, CancellationToken, Task>>()))
            .Callback<Func<IEmailService, CancellationToken, Task>>(f => f(_emailService.Object, CancellationToken.None).GetAwaiter().GetResult());
        _sender = new EmailVerificationSender(
            _tokenRepo.Object,
            _dispatcher.Object,
            _unitOfWork.Object,
            Options.Create(new AppSettings { FrontendBaseUrl = "https://app.test" }),
            _time);
    }

    [Fact]
    public async Task EnviarAsync_PersisteTokenComExpiracao24hECommita()
    {
        EmailVerificationToken? capturado = null;
        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .Callback<EmailVerificationToken, CancellationToken>((t, _) => capturado = t)
            .Returns(Task.CompletedTask);

        await _sender.EnviarAsync(ContaId, EmailDestino);

        capturado.Should().NotBeNull();
        capturado!.ContaId.Should().Be(ContaId);
        capturado.ExpiresAt.Should().Be(Agora.AddHours(24));
        capturado.TokenHash.Should().HaveLength(64);
        capturado.VerifiedAt.Should().BeNull();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnviarAsync_EmailHabilitado_EnviaComLinkDeVerificacao()
    {
        await _sender.EnviarAsync(ContaId, EmailDestino);

        _emailService.Verify(e => e.EnviarAsync(
            EmailDestino,
            "Confirme seu e-mail — forzion.tech",
            It.Is<string>(html => html.Contains("https://app.test/verify-email?token=")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnviarAsync_EmailDesabilitado_PersisteTokenMasNaoEnvia()
    {
        _emailService.SetupGet(e => e.Habilitado).Returns(false);

        await _sender.EnviarAsync(ContaId, EmailDestino);

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(e => e.EnviarAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
