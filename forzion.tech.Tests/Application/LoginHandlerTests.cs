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
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ISystemUserRepository> _systemUserRepo = new();
    private readonly Mock<ILogger<LoginHandler>> _logger = new();
    private readonly LoginCommandValidator _validator = new();
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _handler = new LoginHandler(
            _contaRepo.Object,
            _jwtService.Object,
            _passwordHasher.Object,
            _alunoRepo.Object,
            _treinadorRepo.Object,
            _systemUserRepo.Object,
            _validator,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_CredenciaisValidas_RetornaToken()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        conta.MarcarEmailVerificado(DateTime.UtcNow);
        var treinador = Treinador.Criar(conta.Id, "João Trainer", DateTime.UtcNow).Value;
        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify("senha123", "hash")).Returns(true);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _jwtService.Setup(j => j.GerarToken(conta, treinador.Id)).Returns("token.jwt");

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

        var act = async () => await _handler.HandleAsync(new LoginCommand("x@test.com", "Senha123"));
        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
    }

    [Fact]
    public async Task HandleAsync_SenhaErrada_LancaCredenciaisInvalidasException()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify(It.IsAny<string>(), "hash")).Returns(false);

        var act = async () => await _handler.HandleAsync(new LoginCommand("trainer@test.com", "senhaerrada"));
        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
    }

    [Fact]
    public async Task HandleAsync_EmailNormalizado_BuscaEmMinusculo()
    {
        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);

        var act = async () => await _handler.HandleAsync(new LoginCommand("TRAINER@TEST.COM", "Senha123"));
        await act.Should().ThrowAsync<CredenciaisInvalidasException>();

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
    public async Task HandleAsync_LoginAluno_PerfilIdEhIdDoAluno()
    {
        var conta = Conta.Criar(Email.Criar("aluno@test.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        conta.MarcarEmailVerificado(DateTime.UtcNow);
        var aluno = Aluno.Criar(conta.Id, "João Aluno", DateTime.UtcNow).Value;

        _contaRepo.Setup(r => r.ObterPorEmailAsync("aluno@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify("senha123", "hash")).Returns(true);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(aluno);
        _jwtService.Setup(j => j.GerarToken(conta, aluno.Id)).Returns("token.aluno");

        var result = await _handler.HandleAsync(new LoginCommand("aluno@test.com", "senha123"));

        result.Token.Should().Be("token.aluno");
        result.TipoConta.Should().Be(TipoConta.Aluno);
        _jwtService.Verify(j => j.GerarToken(conta, aluno.Id), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_LoginSystemAdmin_PerfilIdEhIdDoSystemUser()
    {
        var conta = Conta.Criar(Email.Criar("admin@test.com").Value, "hash", TipoConta.SystemAdmin, DateTime.UtcNow).Value;
        conta.MarcarEmailVerificado(DateTime.UtcNow);
        var systemUser = SystemUser.Criar(conta.Id, "Admin", DateTime.UtcNow).Value;

        _contaRepo.Setup(r => r.ObterPorEmailAsync("admin@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify("senha123", "hash")).Returns(true);
        _systemUserRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemUser);
        _jwtService.Setup(j => j.GerarToken(conta, systemUser.Id)).Returns("token.admin");

        var result = await _handler.HandleAsync(new LoginCommand("admin@test.com", "senha123"));

        result.Token.Should().Be("token.admin");
        result.TipoConta.Should().Be(TipoConta.SystemAdmin);
        _jwtService.Verify(j => j.GerarToken(conta, systemUser.Id), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PerfilNaoEncontradoParaConta_LancaDomainException()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        conta.MarcarEmailVerificado(DateTime.UtcNow);

        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify("senha123", "hash")).Returns(true);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new LoginCommand("trainer@test.com", "senha123"));

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task HandleAsync_EmailNaoVerificado_LancaEmailNaoVerificadoException()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify("senha123", "hash")).Returns(true);

        var act = async () => await _handler.HandleAsync(new LoginCommand("trainer@test.com", "senha123"));

        await act.Should().ThrowAsync<EmailNaoVerificadoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
