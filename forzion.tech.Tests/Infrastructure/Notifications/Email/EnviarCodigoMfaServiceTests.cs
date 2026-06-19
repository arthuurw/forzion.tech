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
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class EnviarCodigoMfaServiceTests
{
    private readonly Mock<IMfaChallengeRepository> _challengeRepo = new();
    private readonly Mock<IEmailCriticoDispatcher> _emailCritico = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 6, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly EnviarCodigoMfaService _service;

    public EnviarCodigoMfaServiceTests()
    {
        _service = new EnviarCodigoMfaService(
            _challengeRepo.Object, _emailCritico.Object, _unitOfWork.Object, _timeProvider);
    }

    private static Conta BuildConta(string email = "user@example.com") =>
        Conta.Criar(DomainEmail.Criar(email).Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;

    [Fact]
    public async Task EnviarAsync_PersisteChallengeHasheadoEEnfileiraCodigoCorrespondente()
    {
        var conta = BuildConta();
        MfaChallenge? captured = null;
        _challengeRepo.Setup(r => r.AdicionarAsync(It.IsAny<MfaChallenge>(), It.IsAny<CancellationToken>()))
            .Callback<MfaChallenge, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);

        string? segredo = null;
        _emailCritico.Setup(d => d.Enfileirar(EmailCriticoTemplate.CodigoMfa, "user@example.com", It.IsAny<string>()))
            .Callback<EmailCriticoTemplate, string, string>((_, _, s) => segredo = s);

        await _service.EnviarAsync(conta, MfaProposito.StepUp);

        captured.Should().NotBeNull();
        captured!.ContaId.Should().Be(conta.Id);
        captured.Proposito.Should().Be(MfaProposito.StepUp);
        captured.ExpiraEm.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddMinutes(10));
        captured.UsadoEm.Should().BeNull();
        captured.CodigoHash.Should().MatchRegex("^[0-9a-f]{64}$");

        segredo.Should().NotBeNullOrEmpty();
        Hash(segredo!).Should().Be(captured.CodigoHash);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnviarAsync_DoisEnvios_GeramHashesDistintos()
    {
        var conta = BuildConta();
        var hashes = new List<string>();
        _challengeRepo.Setup(r => r.AdicionarAsync(It.IsAny<MfaChallenge>(), It.IsAny<CancellationToken>()))
            .Callback<MfaChallenge, CancellationToken>((c, _) => hashes.Add(c.CodigoHash))
            .Returns(Task.CompletedTask);

        await _service.EnviarAsync(conta, MfaProposito.StepUp);
        await _service.EnviarAsync(conta, MfaProposito.StepUp);

        hashes.Should().HaveCount(2);
        hashes[0].Should().NotBe(hashes[1]);
    }

    [Fact]
    public async Task EnviarAsync_ContaNula_LancaArgumentNullException()
    {
        var act = async () => await _service.EnviarAsync(null!, MfaProposito.StepUp);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnviarAsync_PersisteEComitaComCancellationTokenDaRequest()
    {
        var conta = BuildConta();
        using var cts = new CancellationTokenSource();

        await _service.EnviarAsync(conta, MfaProposito.StepUp, cts.Token);

        _challengeRepo.Verify(r => r.AdicionarAsync(It.IsAny<MfaChallenge>(), cts.Token), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(cts.Token), Times.Once);
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
