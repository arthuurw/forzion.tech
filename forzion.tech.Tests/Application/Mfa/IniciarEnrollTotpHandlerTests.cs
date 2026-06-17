using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Mfa;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Mfa;

public class IniciarEnrollTotpHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContaId = Guid.NewGuid();

    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaRepository> _contaRepository = new();
    private readonly Mock<IContaMfaRepository> _contaMfaRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly OtpNetTotpService _totp = new();
    private readonly MfaSecretProtector _protector = new(new byte[32]);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Agora));

    public IniciarEnrollTotpHandlerTests()
    {
        _userContext.Setup(u => u.ContaId).Returns(ContaId);
        var conta = Conta.Criar(Email.Criar("user@test.com").Value, "hash", TipoConta.Aluno, Agora).Value;
        _contaRepository.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
    }

    private IniciarEnrollTotpHandler CriarHandler() => new(
        _userContext.Object, _contaRepository.Object, _contaMfaRepository.Object,
        _totp, _protector, _unitOfWork.Object, _time);

    [Fact]
    public async Task Iniciar_PersistePendenteCifrado_RetornaUriESecret()
    {
        ContaMfa? salvo = null;
        _contaMfaRepository.Setup(r => r.BuscarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ContaMfa?)null);
        _contaMfaRepository.Setup(r => r.AdicionarAsync(It.IsAny<ContaMfa>(), It.IsAny<CancellationToken>()))
            .Callback<ContaMfa, CancellationToken>((m, _) => salvo = m);

        var result = await CriarHandler().HandleAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.SecretBase32.Should().NotBeNullOrWhiteSpace();
        result.Value.OtpauthUri.Should().StartWith("otpauth://totp/");
        salvo.Should().NotBeNull();
        salvo!.Habilitado.Should().BeFalse();
        salvo.TotpSecretCifrado.Should().NotBe(result.Value.SecretBase32);
        _protector.Revelar(salvo.TotpSecretCifrado!).Should().Be(result.Value.SecretBase32);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Iniciar_EnrollPendenteExistente_ReemitePelaAtualizacao()
    {
        var pendente = ContaMfa.Criar(ContaId, _protector.Proteger("ANTIGOANTIGO"), Agora).Value;
        _contaMfaRepository.Setup(r => r.BuscarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(pendente);

        var result = await CriarHandler().HandleAsync();

        result.IsSuccess.Should().BeTrue();
        _protector.Revelar(pendente.TotpSecretCifrado!).Should().Be(result.Value.SecretBase32);
        _contaMfaRepository.Verify(r => r.AdicionarAsync(It.IsAny<ContaMfa>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Iniciar_JaHabilitado_Falha()
    {
        var habilitado = ContaMfa.Criar(ContaId, "cifrado==", Agora).Value;
        habilitado.Confirmar(1, Agora);
        _contaMfaRepository.Setup(r => r.BuscarPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(habilitado);

        var result = await CriarHandler().HandleAsync();

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(MfaErrors.JaConfirmado.Code);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
