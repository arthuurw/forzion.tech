using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public class GerarCobrancaMensalHandlerTests
{
    private readonly Mock<IAssinaturaRepository> _assinaturaRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<GerarCobrancaMensalHandler>> _logger = new();
    private readonly GerarCobrancaMensalHandler _handler;

    private static readonly PixPaymentResult PixResult = new("pi_123", "qrcode", "https://img", DateTime.UtcNow.AddHours(1));

    public GerarCobrancaMensalHandlerTests()
    {
        var paymentSettings = Options.Create(new PaymentSettings { TaxaPlataformaPercent = 5m });

        _handler = new GerarCobrancaMensalHandler(
            _assinaturaRepo.Object, _pagamentoRepo.Object, _treinadorRepo.Object,
            _stripeService.Object, _unitOfWork.Object, paymentSettings, _logger.Object);

        _stripeService.Setup(s => s.CriarPixPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PixResult);
    }

    private static Assinatura CriarAssinatura(Guid treinadorId)
    {
        var a = Assinatura.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m);
        return a;
    }

    private static Treinador CriarTreinadorComOnboarding()
    {
        var t = Treinador.Criar(Guid.NewGuid(), "Carlos");
        t.ConfigurarStripeConnect("acct_123");
        t.ConfirmarOnboarding();
        return t;
    }

    [Fact]
    public async Task HandleAsync_AssinaturaValida_GeraPixERetornaResponse()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinatura(treinador.Id);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.PixQrCode.Should().Be("qrcode");
        result.Value.PixQrCodeUrl.Should().Be("https://img");
        result.Value.MetodoPagamento.Should().Be(MetodoPagamento.Pix);
    }

    [Fact]
    public async Task HandleAsync_TreinadorErrado_LancaAcessoNegado()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinatura(treinador.Id);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var act = async () => await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<forzion.tech.Domain.Exceptions.AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_PagamentoPendenteExistente_RetornaExistente()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinatura(treinador.Id);
        var pagamentoPendente = Pagamento.Criar(assinatura.Id, assinatura.Valor);
        pagamentoPendente.DefinirDadosPix("pi_old", "qr", "url", DateTime.UtcNow.AddHours(1));
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamentoPendente);

        var result = await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.PagamentoId.Should().Be(pagamentoPendente.Id);
        _stripeService.Verify(s => s.CriarPixPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PagamentoZumbi_MarcaFalhouECriaNovoCharge()
    {
        // Zumbi: Pendente sem StripePaymentIntentId — Stripe falhou em tentativa anterior
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinatura(treinador.Id);
        var zumbi = Pagamento.Criar(assinatura.Id, assinatura.Valor); // sem DefinirDadosPix

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(zumbi);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsSuccess.Should().BeTrue();
        zumbi.Status.Should().Be(PagamentoStatus.Falhou);
        result.Value.PixQrCode.Should().Be("qrcode");
        _stripeService.Verify(s => s.CriarPixPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_StripeFalha_MarcaPagamentoFalhouEPropagaExcecao()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinatura(treinador.Id);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _stripeService.Setup(s => s.CriarPixPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Stripe indisponível"));

        Pagamento? pagamentoCriado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()))
            .Callback<Pagamento, CancellationToken>((p, _) => pagamentoCriado = p);

        var act = async () => await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));
        await act.Should().ThrowAsync<InvalidOperationException>();

        pagamentoCriado.Should().NotBeNull();
        pagamentoCriado!.Status.Should().Be(PagamentoStatus.Falhou);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaCancelada_RetornaFailure()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinatura(treinador.Id);
        assinatura.Cancelar();
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemOnboarding_LancaDomainException()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        var assinatura = CriarAssinatura(treinador.Id);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*Stripe*");
    }

    [Fact]
    public async Task HandleAsync_AssinaturaNaoEncontrada_LancaDomainException()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Assinatura?)null);

        var act = async () => await _handler.HandleAsync(new GerarCobrancaMensalCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>().WithMessage("Assinatura não encontrada.");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // --- Método Cartão ---

    private static readonly CartaoPaymentResult CartaoResult = new("pi_cartao_123", "secret_abc");

    [Fact]
    public async Task HandleAsync_MetodoCartao_GeraCartaoERetornaResponse()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinatura(treinador.Id);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _stripeService.Setup(s => s.CriarCartaoPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartaoResult);

        var result = await _handler.HandleAsync(
            new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id, MetodoPagamento.Cartao));

        result.IsSuccess.Should().BeTrue();
        result.Value.MetodoPagamento.Should().Be(MetodoPagamento.Cartao);
        _stripeService.Verify(s => s.CriarCartaoPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MetodoCartao_StripeFalha_MarcaPagamentoFalhouEPropagaExcecao()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinatura(treinador.Id);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _stripeService.Setup(s => s.CriarCartaoPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Stripe cartão indisponível"));

        Pagamento? pagamentoCriado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<Pagamento>(), It.IsAny<CancellationToken>()))
            .Callback<Pagamento, CancellationToken>((p, _) => pagamentoCriado = p);

        var act = async () => await _handler.HandleAsync(
            new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id, MetodoPagamento.Cartao));
        await act.Should().ThrowAsync<InvalidOperationException>();

        pagamentoCriado.Should().NotBeNull();
        pagamentoCriado!.Status.Should().Be(PagamentoStatus.Falhou);
    }

    [Fact]
    public async Task HandleAsync_MetodoCartao_NaoChama_CriarPixPaymentIntent()
    {
        var treinador = CriarTreinadorComOnboarding();
        var assinatura = CriarAssinatura(treinador.Id);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _stripeService.Setup(s => s.CriarCartaoPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartaoResult);

        await _handler.HandleAsync(
            new GerarCobrancaMensalCommand(assinatura.Id, treinador.Id, MetodoPagamento.Cartao));

        _stripeService.Verify(s => s.CriarPixPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid>(),
            It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
