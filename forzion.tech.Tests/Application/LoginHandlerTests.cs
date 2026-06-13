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
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ISystemUserRepository> _systemUserRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<LoginHandler>> _logger = new();
    private readonly LoginCommandValidator _validator = new();
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        _refresh.Setup(s => s.EmitirNovaFamiliaAsync(It.IsAny<Conta>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshEmitido("refresh-raw", Guid.NewGuid()));
        _handler = new LoginHandler(
            _contaRepo.Object,
            _jwtService.Object,
            _refresh.Object,
            _passwordHasher.Object,
            _alunoRepo.Object,
            _treinadorRepo.Object,
            _systemUserRepo.Object,
            _unitOfWork.Object,
            TimeProvider.System,
            _validator,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_CredenciaisValidas_RetornaToken()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        conta.MarcarEmailVerificado(DateTime.UtcNow);
        var treinador = Treinador.Criar(conta.Id, "João Trainer", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify("senha123", "hash")).Returns(true);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _jwtService.Setup(j => j.GerarToken(conta, treinador.Id, It.IsAny<string>(), It.IsAny<Guid>())).Returns("token.jwt");

        var result = await _handler.HandleAsync(new LoginCommand("trainer@test.com", "senha123"));

        result.Token.Should().Be("token.jwt");
        result.TipoConta.Should().Be(TipoConta.Treinador);
        result.ContaId.Should().Be(conta.Id);
        result.Nome.Should().Be("João Trainer");
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
    public async Task HandleAsync_EmailDesconhecido_e_SenhaErrada_ProduzemErroIdentico()
    {
        // Anti-enumeração: e-mail inexistente e senha errada DEVEM ser indistinguíveis
        // (mesmo tipo + mensagem) p/ não revelar se a conta existe. Falha se alguém
        // diferenciar os ramos (ex.: lançar "usuário não encontrado" no email desconhecido).
        _contaRepo.Setup(r => r.ObterPorEmailAsync("desconhecido@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conta?)null);
        var actDesconhecido = async () => await _handler.HandleAsync(new LoginCommand("desconhecido@test.com", "Senha123"));
        var exDesconhecido = (await actDesconhecido.Should().ThrowAsync<CredenciaisInvalidasException>()).Which;

        var conta = Conta.Criar(Email.Criar("existe@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        _contaRepo.Setup(r => r.ObterPorEmailAsync("existe@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify(It.IsAny<string>(), "hash")).Returns(false);
        var actSenha = async () => await _handler.HandleAsync(new LoginCommand("existe@test.com", "Senha123"));
        var exSenha = (await actSenha.Should().ThrowAsync<CredenciaisInvalidasException>()).Which;

        exSenha.GetType().Should().Be(exDesconhecido.GetType());
        exSenha.Message.Should().Be(exDesconhecido.Message);
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
        _jwtService.Setup(j => j.GerarToken(conta, aluno.Id, It.IsAny<string>(), It.IsAny<Guid>())).Returns("token.aluno");

        var result = await _handler.HandleAsync(new LoginCommand("aluno@test.com", "senha123"));

        result.Token.Should().Be("token.aluno");
        result.TipoConta.Should().Be(TipoConta.Aluno);
        result.Nome.Should().Be("João Aluno");
        _jwtService.Verify(j => j.GerarToken(conta, aluno.Id, It.IsAny<string>(), It.IsAny<Guid>()), Times.Once);
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
        _jwtService.Setup(j => j.GerarToken(conta, systemUser.Id, It.IsAny<string>(), It.IsAny<Guid>())).Returns("token.admin");

        var result = await _handler.HandleAsync(new LoginCommand("admin@test.com", "senha123"));

        result.Token.Should().Be("token.admin");
        result.TipoConta.Should().Be(TipoConta.SystemAdmin);
        result.Nome.Should().Be("Admin");
        _jwtService.Verify(j => j.GerarToken(conta, systemUser.Id, It.IsAny<string>(), It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PerfilNaoEncontradoParaConta_LancaInvalidOperationException()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        conta.MarcarEmailVerificado(DateTime.UtcNow);

        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify("senha123", "hash")).Returns(true);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new LoginCommand("trainer@test.com", "senha123"));

        // Inconsistência de dados → InvalidOperationException (não DomainException) → mapeia p/ 500, não 422.
        (await act.Should().ThrowAsync<InvalidOperationException>())
            .And.Should().NotBeAssignableTo<DomainException>();
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
    public async Task HandleAsync_TreinadorAguardandoAprovacao_LancaTreinadorAguardandoAprovacaoException()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        conta.MarcarEmailVerificado(DateTime.UtcNow);
        var treinador = Treinador.Criar(conta.Id, "João Trainer", DateTime.UtcNow).Value; // AguardandoAprovacao

        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify("senha123", "hash")).Returns(true);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(new LoginCommand("trainer@test.com", "senha123"));

        await act.Should().ThrowAsync<TreinadorAguardandoAprovacaoException>();
        _jwtService.Verify(j => j.GerarToken(It.IsAny<Conta>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorAguardandoPagamento_LancaTreinadorPagamentoPendenteException()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        conta.MarcarEmailVerificado(DateTime.UtcNow);
        var treinador = Treinador.Criar(conta.Id, "João Trainer", DateTime.UtcNow, null, Guid.NewGuid(), ModoPagamentoAluno.Plataforma, aguardandoPagamento: true).Value;

        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify("senha123", "hash")).Returns(true);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(new LoginCommand("trainer@test.com", "senha123"));

        await act.Should().ThrowAsync<TreinadorPagamentoPendenteException>();
        _jwtService.Verify(j => j.GerarToken(It.IsAny<Conta>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorInativo_LancaTreinadorInativoException()
    {
        var conta = Conta.Criar(Email.Criar("trainer@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        conta.MarcarEmailVerificado(DateTime.UtcNow);
        var treinador = Treinador.Criar(conta.Id, "João Trainer", DateTime.UtcNow).Value;
        treinador.Reprovar(Guid.NewGuid(), DateTime.UtcNow); // → Inativo

        _contaRepo.Setup(r => r.ObterPorEmailAsync("trainer@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        _passwordHasher.Setup(p => p.Verify("senha123", "hash")).Returns(true);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(new LoginCommand("trainer@test.com", "senha123"));

        await act.Should().ThrowAsync<TreinadorInativoException>();
        _jwtService.Verify(j => j.GerarToken(It.IsAny<Conta>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
