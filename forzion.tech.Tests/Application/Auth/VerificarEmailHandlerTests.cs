using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Auth.VerificarEmail;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using DomainEmail = forzion.tech.Domain.ValueObjects.Email;

namespace forzion.tech.Tests.Application.Auth;

/// <summary>
/// VerificarEmail handler — pontos críticos:
///   - Token raw nunca consultado direto: handler aplica SHA-256 antes do lookup.
///   - Token inválido / expirado / já-verificado → DomainException.
///   - **Replay (F23)**: chamar 2x com o MESMO raw token — 2ª chamada falha
///     com "inválido ou já utilizado". Cobre o gap específico apontado no review:
///     `MarcarComoVerificado` rejeita re-uso via `VerifiedAt.HasValue`, e o
///     handler chega no estado `VerifiedAt != null` na 2ª tentativa.
///   - Sucesso: `Conta.EmailVerificado = true` + `Token.VerifiedAt` setado, no
///     mesmo commit.
/// </summary>
public class VerificarEmailHandlerTests
{
    private readonly Mock<IEmailVerificationTokenRepository> _tokenRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly VerificarEmailCommandValidator _validator = new();
    private readonly VerificarEmailHandler _handler;

    private const string RawToken = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2";

    public VerificarEmailHandlerTests()
    {
        _handler = new VerificarEmailHandler(
            _tokenRepo.Object, _contaRepo.Object, _unitOfWork.Object, _timeProvider, _validator);
    }

    private static string ComputeHash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private EmailVerificationToken BuildToken(Guid? contaId = null, TimeSpan? ttl = null, DateTime? verifiedAt = null)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var token = EmailVerificationToken.Criar(
            contaId ?? Guid.NewGuid(),
            ComputeHash(RawToken),
            agora.Add(ttl ?? TimeSpan.FromHours(24)),
            agora).Value;
        if (verifiedAt.HasValue)
            token.MarcarComoVerificado(verifiedAt.Value);
        return token;
    }

    private static Conta BuildConta() =>
        Conta.Criar(DomainEmail.Criar("user@example.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_TokenInexistente_LancaDomainException()
    {
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailVerificationToken?)null);

        var act = async () => await _handler.HandleAsync(new VerificarEmailCommand(RawToken));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*inválido*");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TokenJaVerificado_LancaDomainException()
    {
        var token = BuildToken(verifiedAt: _timeProvider.GetUtcNow().UtcDateTime);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var act = async () => await _handler.HandleAsync(new VerificarEmailCommand(RawToken));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*inválido ou já utilizado*");
    }

    [Fact]
    public async Task HandleAsync_TokenExpirado_LancaDomainException()
    {
        var token = BuildToken(ttl: TimeSpan.FromMinutes(1));
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));

        var act = async () => await _handler.HandleAsync(new VerificarEmailCommand(RawToken));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*expirado*");
    }

    [Fact]
    public async Task HandleAsync_TokenValido_MarcaEmailEContaVerificadosNoMesmoCommit()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        await _handler.HandleAsync(new VerificarEmailCommand(RawToken));

        conta.EmailVerificado.Should().BeTrue();
        conta.VerificadoEm.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        token.VerifiedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Replay_MesmoTokenDuasVezes_SegundaFalha_F23()
    {
        // F23 explícito: o atacante que copia o link do e-mail tenta usar 2x.
        // 1ª chamada marca VerifiedAt; 2ª chamada chega no `token.VerifiedAt.HasValue`
        // e DEVE falhar antes de tocar a Conta.
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        await _handler.HandleAsync(new VerificarEmailCommand(RawToken));

        var act = async () => await _handler.HandleAsync(new VerificarEmailCommand(RawToken));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*inválido ou já utilizado*");
    }

    [Fact]
    public async Task HandleAsync_ContaNaoEncontrada_LancaDomainException()
    {
        var token = BuildToken();
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        var act = async () => await _handler.HandleAsync(new VerificarEmailCommand(RawToken));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*Conta não encontrada*");
    }

    [Theory]
    [InlineData("", "*obrigatório*")]
    [InlineData("short", "*inválido*")]
    public async Task HandleAsync_TokenInvalido_FalhaValidacao(string token, string messageContains)
    {
        var act = async () => await _handler.HandleAsync(new VerificarEmailCommand(token));
        await act.Should().ThrowAsync<ValidationException>().WithMessage(messageContains);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
