using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

/// <summary>
/// Cobre o início do fluxo de password reset: geração de token + envio de e-mail.
/// Pontos críticos:
///   - Conta inexistente NÃO revela ausência (silently ignora — defesa anti-enumeração).
///   - Token persistido como HASH SHA-256, nunca em texto claro.
///   - Link enviado contém o token RAW (Url-friendly).
///   - Expiração 1h apartir do `TimeProvider`.
///   - `IEmailService.Habilitado = false` cria o token mas NÃO envia (modo dev).
/// </summary>
public class EsqueceuSenhaHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IPasswordResetTokenRepository> _tokenRepo = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<ILogger<EsqueceuSenhaHandler>> _logger = new();
    private readonly EsqueceuSenhaHandler _handler;

    public EsqueceuSenhaHandlerTests()
    {
        var settings = Options.Create(new AppSettings { FrontendBaseUrl = "https://forzion.test" });
        _emailService.SetupGet(s => s.Habilitado).Returns(true);

        _handler = new EsqueceuSenhaHandler(
            _contaRepo.Object, _tokenRepo.Object, _emailService.Object,
            _unitOfWork.Object, settings, _timeProvider, _logger.Object);
    }

    private static Conta BuildConta(string email = "user@example.com") =>
        Conta.Criar(DomainEmail.Criar(email), "hash", TipoConta.Aluno, DateTime.UtcNow);

    [Fact]
    public async Task HandleAsync_ContaInexistente_RetornaSilenciosamenteSemEnviarEmail()
    {
        // Defesa anti-enumeração: requests pra emails inexistentes NÃO devem revelar isso.
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(new EsqueceuSenhaCommand("ghost@example.com"));

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailService.Verify(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContaExistente_GeraTokenHashedComExpiracao1h_EEnviaEmail()
    {
        var conta = BuildConta();
        _contaRepo.Setup(r => r.ObterPorEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        PasswordResetToken? captured = null;
        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(new EsqueceuSenhaCommand("user@example.com"));

        captured.Should().NotBeNull();
        captured!.ContaId.Should().Be(conta.Id);
        captured.TokenHash.Should().HaveLength(64); // SHA-256 hex
        captured.TokenHash.Should().MatchRegex("^[0-9a-f]{64}$"); // lowercase hex
        captured.ExpiresAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddHours(1));
        captured.UsedAt.Should().BeNull();

        _emailService.Verify(s => s.EnviarAsync(
            "user@example.com",
            It.Is<string>(a => a.Contains("Redefinição de senha", StringComparison.Ordinal)),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmailDesabilitado_GeraTokenMasNaoEnvia()
    {
        // Modo dev: gera token (UX completa) mas pula o envio externo.
        _emailService.SetupGet(s => s.Habilitado).Returns(false);
        var conta = BuildConta();
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        await _handler.HandleAsync(new EsqueceuSenhaCommand("user@example.com"));

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(s => s.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
        // Cada solicitação produz um RAW token novo (RandomNumberGenerator) e portanto
        // hashes distintos. Sem isso, o atacante que comprometeu UMA tabela snapshot
        // conseguiria reusar tokens entre solicitações.
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
}
