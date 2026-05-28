using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Auth.RedefinirSenha;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Auth;

/// <summary>
/// Cobre o segundo passo do fluxo de password reset. Pontos críticos:
///   - Token raw nunca tocado pelo BD: handler aplica SHA-256 antes do lookup.
///   - Token inexistente / inválido / expirado / já-usado → DomainException.
///   - Replay (mesmo token raw em duas chamadas) → 2ª falha estritamente.
///   - Sucesso atualiza Conta.PasswordHash e marca o token como usado no mesmo commit.
/// </summary>
public class RedefinirSenhaHandlerTests
{
    private readonly Mock<IPasswordResetTokenRepository> _tokenRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly RedefinirSenhaCommandValidator _validator = new();
    private readonly RedefinirSenhaHandler _handler;

    // Raw token plausível: 64 chars hex (mesmo formato que EsqueceuSenhaHandler gera).
    private const string RawToken = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2";

    public RedefinirSenhaHandlerTests()
    {
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(s => $"hash:{s}");
        _handler = new RedefinirSenhaHandler(
            _tokenRepo.Object, _contaRepo.Object, _hasher.Object,
            _unitOfWork.Object, _timeProvider, _validator);
    }

    private static string ComputeHash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private PasswordResetToken BuildToken(Guid? contaId = null, TimeSpan? ttl = null, DateTime? usedAt = null)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var token = PasswordResetToken.Criar(
            contaId ?? Guid.NewGuid(),
            ComputeHash(RawToken),
            agora.Add(ttl ?? TimeSpan.FromMinutes(30)),
            agora);
        if (usedAt.HasValue)
            token.MarcarComoUsado(usedAt.Value);
        return token;
    }

    private static Conta BuildConta() =>
        Conta.Criar(Email.Criar("user@example.com"), "old-hash", TipoConta.Aluno, DateTime.UtcNow);

    [Fact]
    public async Task HandleAsync_TokenInexistente_LancaDomainException()
    {
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordResetToken?)null);

        var act = async () => await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha1"));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*inválido*");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TokenJaUsado_LancaDomainException()
    {
        var token = BuildToken(usedAt: _timeProvider.GetUtcNow().UtcDateTime);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var act = async () => await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha1"));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*inválido ou já utilizado*");
    }

    [Fact]
    public async Task HandleAsync_TokenExpirado_LancaDomainException()
    {
        var token = BuildToken(ttl: TimeSpan.FromMinutes(1));
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2)); // token agora expirado

        var act = async () => await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha1"));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*expirado*");
    }

    [Fact]
    public async Task HandleAsync_TokenValido_AtualizaSenhaEMarcaUsado()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha1"));

        conta.PasswordHash.Should().Be("hash:NovaSenha1");
        token.UsedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Replay_MesmoTokenDuasVezes_SegundaFalha()
    {
        // Cenário crítico de segurança: atacante intercepta o link e tenta usar duas vezes.
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(token.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        // 1ª chamada: sucesso
        await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha1"));

        // 2ª chamada: token.UsedAt agora é não-nulo → DomainException
        var act = async () => await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "OutraSenha1"));
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

        var act = async () => await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, "NovaSenha1"));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*Conta não encontrada*");
    }

    [Theory]
    [InlineData("", "*obrigatório*")]
    [InlineData("short", "*inválido*")]
    public async Task HandleAsync_TokenInvalido_FalhaValidacao(string token, string messageContains)
    {
        var act = async () => await _handler.HandleAsync(new RedefinirSenhaCommand(token, "NovaSenha1"));
        await act.Should().ThrowAsync<ValidationException>().WithMessage(messageContains);
    }

    [Theory]
    [InlineData("curta1A", "*8 caracteres*")]
    [InlineData("semuppercase1", "*maiúscula*")]
    [InlineData("SEMLOWERCASE1", "*minúscula*")]
    [InlineData("SemDigitoAa", "*dígito*")]
    public async Task HandleAsync_SenhaFraca_FalhaValidacao(string senha, string messageContains)
    {
        var act = async () => await _handler.HandleAsync(new RedefinirSenhaCommand(RawToken, senha));
        await act.Should().ThrowAsync<ValidationException>().WithMessage(messageContains);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
