using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class EsqueceuSenhaHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IPasswordResetTokenRepository> _tokenRepo = new();
    private readonly Mock<IEmailCriticoDispatcher> _emailCritico = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<ILogger<EsqueceuSenhaHandler>> _logger = new();
    private readonly EsqueceuSenhaHandler _handler;

    public EsqueceuSenhaHandlerTests()
    {
        _handler = new EsqueceuSenhaHandler(
            _contaRepo.Object, _tokenRepo.Object, _emailCritico.Object,
            _unitOfWork.Object, _timeProvider, _logger.Object);
    }

    private static Conta BuildConta(string email = "user@example.com") =>
        Conta.Criar(DomainEmail.Criar(email).Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_ContaInexistente_RetornaSilenciosamenteSemEnfileirar()
    {
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(new EsqueceuSenhaCommand("ghost@example.com"));

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailCritico.Verify(d => d.Enfileirar(It.IsAny<EmailCriticoTemplate>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContaExistente_GeraTokenHashedComExpiracao1h_EEnfileiraTokenRaw()
    {
        var conta = BuildConta();
        _contaRepo.Setup(r => r.ObterPorEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        PasswordResetToken? captured = null;
        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        string? segredo = null;
        _emailCritico.Setup(d => d.Enfileirar(EmailCriticoTemplate.RedefinirSenha, "user@example.com", It.IsAny<string>()))
            .Callback<EmailCriticoTemplate, string, string>((_, _, s) => segredo = s);

        await _handler.HandleAsync(new EsqueceuSenhaCommand("user@example.com"));

        captured.Should().NotBeNull();
        captured!.ContaId.Should().Be(conta.Id);
        captured.TokenHash.Should().MatchRegex("^[0-9a-f]{64}$");
        captured.ExpiresAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddHours(1));
        captured.UsedAt.Should().BeNull();

        segredo.Should().NotBeNullOrEmpty();
        Hash(segredo!).Should().Be(captured.TokenHash);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NormalizaEmailParaLowercaseAntesDeBuscar()
    {
        var conta = BuildConta();
        _contaRepo.Setup(r => r.ObterPorEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        await _handler.HandleAsync(new EsqueceuSenhaCommand("  USER@Example.COM  "));

        _contaRepo.Verify(r => r.ObterPorEmailAsync("user@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_TokensDoisRequestsConsecutivos_SaoUnicos()
    {
        var conta = BuildConta();
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var hashes = new List<string>();
        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((t, _) => hashes.Add(t.TokenHash))
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(new EsqueceuSenhaCommand("user@example.com"));
        await _handler.HandleAsync(new EsqueceuSenhaCommand("user@example.com"));

        hashes.Should().HaveCount(2);
        hashes[0].Should().NotBe(hashes[1]);
    }

    [Fact]
    public async Task HandleAsync_PersisteEComitaComCancellationTokenDaRequest()
    {
        var conta = BuildConta();
        _contaRepo.Setup(r => r.ObterPorEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        using var cts = new CancellationTokenSource();

        await _handler.HandleAsync(new EsqueceuSenhaCommand("user@example.com"), cts.Token);

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<PasswordResetToken>(), cts.Token), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(cts.Token), Times.Once);
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
