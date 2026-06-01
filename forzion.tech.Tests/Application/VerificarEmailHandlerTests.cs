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
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application;

public class VerificarEmailHandlerTests
{
    private static readonly DateTime Agora = new(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string RawToken = new('a', 64);

    private readonly Mock<IEmailVerificationTokenRepository> _tokenRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(Agora);
    private readonly VerificarEmailCommandValidator _validator = new();
    private readonly VerificarEmailHandler _handler;

    public VerificarEmailHandlerTests()
    {
        _handler = new VerificarEmailHandler(
            _tokenRepo.Object,
            _contaRepo.Object,
            _unitOfWork.Object,
            _time,
            _validator);
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    private static EmailVerificationToken TokenValido(Guid contaId, DateTime criadoEm, DateTime expiraEm) =>
        EmailVerificationToken.Criar(contaId, Hash(RawToken), expiraEm, criadoEm).Value;

    [Fact]
    public async Task HandleAsync_TokenValido_MarcaContaVerificada()
    {
        var conta = Conta.Criar(Email.Criar("aluno@test.com").Value, "hash", TipoConta.Aluno, Agora).Value;
        var token = TokenValido(conta.Id, Agora.AddMinutes(-5), Agora.AddHours(23));

        _tokenRepo.Setup(r => r.BuscarPorHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        await _handler.HandleAsync(new VerificarEmailCommand(RawToken));

        conta.EmailVerificado.Should().BeTrue();
        token.VerifiedAt.Should().NotBeNull();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TokenInexistente_LancaDomainException()
    {
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailVerificationToken?)null);

        var act = async () => await _handler.HandleAsync(new VerificarEmailCommand(RawToken));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task HandleAsync_TokenJaUtilizado_LancaDomainException()
    {
        var token = TokenValido(Guid.NewGuid(), Agora.AddMinutes(-5), Agora.AddHours(23));
        token.MarcarComoVerificado(Agora.AddMinutes(-1));

        _tokenRepo.Setup(r => r.BuscarPorHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var act = async () => await _handler.HandleAsync(new VerificarEmailCommand(RawToken));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task HandleAsync_TokenExpirado_LancaDomainException()
    {
        var token = TokenValido(Guid.NewGuid(), Agora.AddHours(-25), Agora.AddHours(-1));

        _tokenRepo.Setup(r => r.BuscarPorHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var act = async () => await _handler.HandleAsync(new VerificarEmailCommand(RawToken));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task HandleAsync_TokenFormatoInvalido_LancaValidationException()
    {
        var act = async () => await _handler.HandleAsync(new VerificarEmailCommand("curto"));

        await act.Should().ThrowAsync<ValidationException>();
    }
}
