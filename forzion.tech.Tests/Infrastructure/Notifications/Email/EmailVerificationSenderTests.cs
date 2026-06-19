using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class EmailVerificationSenderTests
{
    private static readonly DateTime Agora = new(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();
    private const string EmailDestino = "aluno@example.com";

    private readonly Mock<IEmailVerificationTokenRepository> _tokenRepo = new();
    private readonly Mock<IEmailCriticoDispatcher> _emailCritico = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(Agora);
    private readonly EmailVerificationSender _sender;

    public EmailVerificationSenderTests()
    {
        _sender = new EmailVerificationSender(
            _tokenRepo.Object,
            _emailCritico.Object,
            _unitOfWork.Object,
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
    public async Task EnviarAsync_EnfileiraEmailCriticoComTokenRawCorrespondenteAoHash()
    {
        EmailVerificationToken? token = null;
        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .Callback<EmailVerificationToken, CancellationToken>((t, _) => token = t)
            .Returns(Task.CompletedTask);

        string? segredo = null;
        _emailCritico.Setup(d => d.Enfileirar(EmailCriticoTemplate.VerificarEmail, EmailDestino, It.IsAny<string>()))
            .Callback<EmailCriticoTemplate, string, string>((_, _, s) => segredo = s);

        await _sender.EnviarAsync(ContaId, EmailDestino);

        segredo.Should().NotBeNullOrEmpty();
        Hash(segredo!).Should().Be(token!.TokenHash);
        _emailCritico.Verify(d => d.Enfileirar(EmailCriticoTemplate.VerificarEmail, EmailDestino, It.IsAny<string>()), Times.Once);
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
