using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Auth.Login;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class LoginHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IJwtService> _jwtService = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ILogger<LoginHandler>> _logger = new();
    private readonly LoginCommandValidator _validator = new();
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _handler = new LoginHandler(
            _contaRepo.Object,
            _jwtService.Object,
            _passwordHasher.Object,
            _validator,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_CredenciaisValidas_RetornaToken()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com"), "hash", TipoConta.Treinador);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify("senha123", "hash")).Returns(true);
        _jwtService.Setup(j => j.GerarToken(conta, conta.Id)).Returns("token.jwt");

        var result = await _handler.HandleAsync(new LoginCommand("trainer@test.com", "senha123"));

        result.Token.Should().Be("token.jwt");
        result.TipoConta.Should().Be(TipoConta.Treinador);
        result.ContaId.Should().Be(conta.Id);
    }

    [Fact]
    public async Task HandleAsync_EmailNaoEncontrado_LancaCredenciaisInvalidasException()
    {
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        var act = async () => await _handler.HandleAsync(new LoginCommand("x@test.com", "senha"));
        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
    }

    [Fact]
    public async Task HandleAsync_SenhaErrada_LancaCredenciaisInvalidasException()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com"), "hash", TipoConta.Treinador);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify(It.IsAny<string>(), "hash")).Returns(false);

        var act = async () => await _handler.HandleAsync(new LoginCommand("trainer@test.com", "errada"));
        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
    }

    [Fact]
    public async Task HandleAsync_EmailNormalizado_BuscaEmMinusculo()
    {
        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        await Assert.ThrowsAsync<CredenciaisInvalidasException>(() =>
            _handler.HandleAsync(new LoginCommand("TRAINER@TEST.COM", "senha")));

        _contaRepo.Verify(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmailVazio_LancaValidationException()
    {
        var act = async () => await _handler.HandleAsync(new LoginCommand("", "senha123"));
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_EmailInvalido_LancaValidationException()
    {
        var act = async () => await _handler.HandleAsync(new LoginCommand("invalido", "senha123"));
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_SenhaVazia_LancaValidationException()
    {
        var act = async () => await _handler.HandleAsync(new LoginCommand("a@b.com", ""));
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
