using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.CancelarMinhaAssinaturaTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class CancelarMinhaAssinaturaTreinadorHandlerTests
{
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IPagamentoTreinadorRepository> _pagamentoRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 5, 24, 12, 0, 0, TimeSpan.Zero));
    private readonly Mock<ILogger<CancelarMinhaAssinaturaTreinadorHandler>> _logger = new();
    private readonly CancelarMinhaAssinaturaTreinadorHandler _handler;

    private static readonly Guid TreinadorId = Guid.NewGuid();

    public CancelarMinhaAssinaturaTreinadorHandlerTests()
    {
        _handler = new CancelarMinhaAssinaturaTreinadorHandler(
            _assinaturaRepo.Object, _vinculoRepo.Object, _pagamentoRepo.Object,
            _stripeService.Object, _unitOfWork.Object, _timeProvider, _logger.Object);
    }

    private AssinaturaTreinador CriarAtiva(DateTime dataInicio)
    {
        var a = AssinaturaTreinador.Criar(TreinadorId, Guid.NewGuid(), 50m, dataInicio).Value;
        a.Ativar(dataInicio);
        return a;
    }

    private static PagamentoTreinador CriarPago(AssinaturaTreinador assinatura, DateTime agora)
    {
        var p = PagamentoTreinador.Criar(
            assinatura.TreinadorId, assinatura.Id, assinatura.Valor,
            FinalidadePagamentoTreinador.Cadastro, agora).Value;
        p.DefinirDadosPix("pi_treinador_pago", "qr", "url", agora.AddHours(1), agora);
        p.MarcarPago(agora);
        return p;
    }

    [Fact]
    public async Task HandleAsync_ComVinculosAtivos_RetornaOffboardingNecessario()
    {
        var assinatura = CriarAtiva(_timeProvider.GetUtcNow().UtcDateTime);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _vinculoRepo.Setup(r => r.TemVinculosAtivosAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaTreinadorCommand(TreinadorId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("offboarding_necessario");
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Ativa);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _stripeService.Verify(s => s.CriarReembolsoAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CdcElegivel_ReembolsaSemReverterTransferenciaECancela()
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var assinatura = CriarAtiva(agora.AddDays(-3));
        var pago = CriarPago(assinatura, agora.AddDays(-3));

        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _vinculoRepo.Setup(r => r.TemVinculosAtivosAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _pagamentoRepo.Setup(r => r.ObterPagoPorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pago);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaTreinadorCommand(TreinadorId));

        result.IsSuccess.Should().BeTrue();
        result.Value.CanceladaEm.Should().Be(agora);
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Cancelada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _stripeService.Verify(s => s.CriarReembolsoAsync("pi_treinador_pago", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ForaDoPrazo7Dias_CancelaSemReembolso()
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var assinatura = CriarAtiva(agora.AddDays(-30));

        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _vinculoRepo.Setup(r => r.TemVinculosAtivosAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaTreinadorCommand(TreinadorId));

        result.IsSuccess.Should().BeTrue();
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Cancelada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _stripeService.Verify(s => s.CriarReembolsoAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _pagamentoRepo.Verify(r => r.ObterPagoPorAssinaturaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SemAssinatura_RetornaNotFound()
    {
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaTreinador?)null);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaTreinadorCommand(TreinadorId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(CancelarMinhaAssinaturaTreinadorHandler.AssinaturaNaoEncontradaErrorCode);
        result.Error.Type.Should().Be(forzion.tech.Domain.Shared.ErrorType.NotFound);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CdcElegivelMasFalhaStripe_CancelaMesmoAssim()
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var assinatura = CriarAtiva(agora.AddDays(-2));
        var pago = CriarPago(assinatura, agora.AddDays(-2));

        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _vinculoRepo.Setup(r => r.TemVinculosAtivosAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _pagamentoRepo.Setup(r => r.ObterPagoPorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pago);
        _stripeService.Setup(s => s.CriarReembolsoAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Stripe indisponível"));

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaTreinadorCommand(TreinadorId));

        result.IsSuccess.Should().BeTrue();
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Cancelada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
