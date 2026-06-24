using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.TrocarEmail;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.ContaTestes;

public class ConfirmarTrocaEmailHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<ITrocaEmailTokenRepository> _tokenRepo = new();
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<ITrustedDeviceRepository> _trustedDevice = new();
    private readonly Mock<ITokenRevogadoRepository> _tokenRevogado = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<ILogger<ConfirmarTrocaEmailHandler>> _logger = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 6, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly ConfirmarTrocaEmailHandler _handler;

    private static readonly Guid ContaId = Guid.NewGuid();
    private const string RawCodigo = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2";

    public ConfirmarTrocaEmailHandlerTests()
    {
        _handler = new ConfirmarTrocaEmailHandler(
            _contaRepo.Object,
            _tokenRepo.Object,
            _refresh.Object,
            _trustedDevice.Object,
            _tokenRevogado.Object,
            _logRepo.Object,
            _unitOfWork.Object,
            _timeProvider,
            new ConfirmarTrocaEmailCommandValidator(),
            _logger.Object);
    }

    private static string ComputeHash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private TrocaEmailToken BuildToken(Guid? contaId = null, string novoEmail = "novo@test.com", TimeSpan? ttl = null, DateTime? usadoEm = null)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var token = TrocaEmailToken.Criar(
            contaId ?? ContaId,
            novoEmail,
            ComputeHash(RawCodigo),
            agora.Add(ttl ?? TimeSpan.FromMinutes(30)),
            agora).Value;
        if (usadoEm.HasValue)
            token.MarcarComoUsado(usadoEm.Value);
        return token;
    }

    private Conta BuildConta(string email = "atual@test.com") =>
        Conta.Criar(Email.Criar(email).Value, "hash", TipoConta.Aluno, _timeProvider.GetUtcNow().UtcDateTime).Value;

    private ConfirmarTrocaEmailCommand BuildCommand(string codigo = RawCodigo, Guid? jti = null) =>
        new(ContaId, jti ?? Guid.Empty, _timeProvider.GetUtcNow().UtcDateTime.AddHours(1), codigo);

    [Fact]
    public async Task HandleAsync_CodigoInexistente_RetornaFalhaGenericaEmailInalterado()
    {
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrocaEmailToken?)null);

        var result = await _handler.HandleAsync(BuildCommand());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("troca_email.codigo_invalido");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CodigoJaUsado_RetornaFalhaGenericaEmailInalterado()
    {
        var token = BuildToken(usadoEm: _timeProvider.GetUtcNow().UtcDateTime);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(ComputeHash(RawCodigo), It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await _handler.HandleAsync(BuildCommand());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("troca_email.codigo_invalido");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CodigoExpirado_RetornaFalhaEmailInalterado()
    {
        var token = BuildToken(ttl: TimeSpan.FromMinutes(1));
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(ComputeHash(RawCodigo), It.IsAny<CancellationToken>())).ReturnsAsync(token);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));

        var result = await _handler.HandleAsync(BuildCommand());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("troca_email.codigo_expirado");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CodigoValido_TrocaEmailEMarcaTokenUsado()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id, novoEmail: "novo@test.com");
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(ComputeHash(RawCodigo), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("novo@test.com", It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var result = await _handler.HandleAsync(new ConfirmarTrocaEmailCommand(conta.Id, Guid.Empty, _timeProvider.GetUtcNow().UtcDateTime.AddHours(1), RawCodigo));

        result.IsSuccess.Should().BeTrue();
        conta.Email.Value.Should().Be("novo@test.com");
        conta.EmailVerificado.Should().BeTrue();
        token.UsadoEm.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CodigoValido_RevogaDispositivosConfiaveisERefreshTokens()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(ComputeHash(RawCodigo), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var result = await _handler.HandleAsync(new ConfirmarTrocaEmailCommand(conta.Id, Guid.Empty, _timeProvider.GetUtcNow().UtcDateTime.AddHours(1), RawCodigo));

        result.IsSuccess.Should().BeTrue();
        _trustedDevice.Verify(r => r.RemoverPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()), Times.Once);
        _refresh.Verify(s => s.RevogarTodasPorContaAsync(conta.Id, MotivoRevogacaoFamilia.TrocaEmail, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CodigoValido_CarimbaEpochDeSessao()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(ComputeHash(RawCodigo), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var result = await _handler.HandleAsync(new ConfirmarTrocaEmailCommand(conta.Id, Guid.Empty, _timeProvider.GetUtcNow().UtcDateTime.AddHours(1), RawCodigo));

        result.IsSuccess.Should().BeTrue();
        conta.SessoesInvalidasAntesDeUtc.Should().Be(_timeProvider.GetUtcNow());
    }

    [Fact]
    public async Task HandleAsync_JtiCorrenteValido_FazBlacklistDoJti()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id);
        var jti = Guid.NewGuid();
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(ComputeHash(RawCodigo), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var result = await _handler.HandleAsync(new ConfirmarTrocaEmailCommand(
            conta.Id, jti, _timeProvider.GetUtcNow().UtcDateTime.AddHours(1), RawCodigo));

        result.IsSuccess.Should().BeTrue();
        _tokenRevogado.Verify(r => r.AdicionarAsync(It.Is<TokenRevogado>(t => t.Jti == jti), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NovoEmailEmUsoNaMomentoConfirmacao_RetornaFalha()
    {
        var conta = BuildConta();
        var outraConta = BuildConta("novo@test.com");
        var token = BuildToken(contaId: conta.Id, novoEmail: "novo@test.com");
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(ComputeHash(RawCodigo), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("novo@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(outraConta);

        var result = await _handler.HandleAsync(new ConfirmarTrocaEmailCommand(conta.Id, Guid.Empty, _timeProvider.GetUtcNow().UtcDateTime.AddHours(1), RawCodigo));

        result.IsFailure.Should().BeTrue();
        conta.Email.Value.Should().Be("atual@test.com");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContaNaoEncontradaAposTokenValido_LancaEstadoInconsistente()
    {
        var token = BuildToken(contaId: ContaId);
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(ComputeHash(RawCodigo), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var act = async () => await _handler.HandleAsync(BuildCommand());
        await act.Should().ThrowAsync<EstadoInconsistenteException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_CodigoVazio_LancaValidationException()
    {
        var act = async () => await _handler.HandleAsync(new ConfirmarTrocaEmailCommand(ContaId, Guid.Empty, DateTime.UtcNow, ""));
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_CodigoValido_RegistraLogEmailAlterado()
    {
        var conta = BuildConta();
        var token = BuildToken(contaId: conta.Id, novoEmail: "novo@test.com");
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(ComputeHash(RawCodigo), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _contaRepo.Setup(r => r.ObterPorEmailAsync("novo@test.com", It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var result = await _handler.HandleAsync(new ConfirmarTrocaEmailCommand(conta.Id, Guid.Empty, _timeProvider.GetUtcNow().UtcDateTime.AddHours(1), RawCodigo));

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(It.Is<LogAprovacao>(l => l.TipoAcao == TipoAcaoAprovacao.EmailAlterado), It.IsAny<CancellationToken>()), Times.Once);
    }
}
