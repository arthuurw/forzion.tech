using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Application.UseCases.Treinadores.IniciarPagamentoPlano;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class IniciarPagamentoPlanoHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<IPagamentoTreinadorRepository> _pagamentoRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDbContextTransactionProvider> _transactionProvider = new();
    private readonly Mock<ILogger<IniciarPagamentoPlanoHandler>> _logger = new();
    private readonly IniciarPagamentoPlanoHandler _handler;

    private sealed class NoopTransaction : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static readonly PixPaymentResult PixResult = new("pi_plat_123", "qrcode", "https://img", DateTime.UtcNow.AddHours(1));
    private static readonly CartaoPaymentResult CartaoResult = new("pi_plat_cartao_123", "secret_abc");

    public IniciarPagamentoPlanoHandlerTests()
    {
        _transactionProvider.Setup(p => p.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NoopTransaction());

        _stripeService.Setup(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PixResult);
        _stripeService.Setup(s => s.CriarCartaoPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartaoResult);

        var criarPagamentoService = new CriarPagamentoComIntentService(
            _unitOfWork.Object, _transactionProvider.Object, TimeProvider.System,
            Mock.Of<ILogger<CriarPagamentoComIntentService>>());

        _handler = new IniciarPagamentoPlanoHandler(
            _treinadorRepo.Object, _assinaturaRepo.Object, _pagamentoRepo.Object,
            _stripeService.Object, criarPagamentoService,
            TimeProvider.System, _logger.Object);
    }

    private static Treinador TreinadorAguardandoPagamento() =>
        Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow, planoPlataformaId: Guid.NewGuid(), aguardandoPagamento: true).Value;

    private static AssinaturaTreinador AssinaturaPendente(Guid treinadorId) =>
        AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 99m, DateTime.UtcNow).Value;

    private void Arrange(Treinador treinador, AssinaturaTreinador assinatura, PagamentoTreinador? pendente = null)
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pendente);
    }

    [Fact]
    public async Task HandleAsync_HappyPathPix_CriaPagamentoComValorDaAssinaturaEFinalidadeCadastro()
    {
        var treinador = TreinadorAguardandoPagamento();
        var assinatura = AssinaturaPendente(treinador.Id);
        Arrange(treinador, assinatura);

        PagamentoTreinador? adicionado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()))
            .Callback<PagamentoTreinador, CancellationToken>((p, _) => adicionado = p);

        var result = await _handler.HandleAsync(new IniciarPagamentoPlanoCommand(treinador.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.MetodoPagamento.Should().Be(MetodoPagamento.Pix);
        result.Value.PixQrCode.Should().Be("qrcode");
        result.Value.StripePaymentIntentId.Should().Be("pi_plat_123");

        adicionado.Should().NotBeNull();
        adicionado!.Valor.Should().Be(assinatura.Valor);
        adicionado.Finalidade.Should().Be(FinalidadePagamentoTreinador.Cadastro);
        adicionado.StripePaymentIntentId.Should().NotBeNullOrEmpty(
            because: "o pagamento só é persistido após o Stripe retornar o intent id");

        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            assinatura.Valor, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_HappyPathCartao_GeraCartaoERetornaClientSecret()
    {
        var treinador = TreinadorAguardandoPagamento();
        var assinatura = AssinaturaPendente(treinador.Id);
        Arrange(treinador, assinatura);

        var result = await _handler.HandleAsync(new IniciarPagamentoPlanoCommand(treinador.Id, MetodoPagamento.Cartao));

        result.IsSuccess.Should().BeTrue();
        result.Value.MetodoPagamento.Should().Be(MetodoPagamento.Cartao);
        result.Value.ClientSecret.Should().Be("secret_abc");
        _stripeService.Verify(s => s.CriarCartaoPlataformaPaymentIntentAsync(
            assinatura.Valor, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoAguardandoPagamento_RetornaFailureSemChamarStripe()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new IniciarPagamentoPlanoCommand(treinador.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador_nao_aguardando_pagamento");
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaExcecao()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new IniciarPagamentoPlanoCommand(Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_SemAssinaturaPendente_RetornaFailure()
    {
        var treinador = TreinadorAguardandoPagamento();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaTreinador?)null);

        var result = await _handler.HandleAsync(new IniciarPagamentoPlanoCommand(treinador.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assinatura_treinador_invalida");
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaNaoPendente_RetornaFailure()
    {
        var treinador = TreinadorAguardandoPagamento();
        var assinatura = AssinaturaPendente(treinador.Id);
        assinatura.Ativar(DateTime.UtcNow);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new IniciarPagamentoPlanoCommand(treinador.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assinatura_treinador_invalida");
    }

    [Fact]
    public async Task HandleAsync_PagamentoPendenteComIntent_RetornaExistenteSemNovaChamadaStripe()
    {
        var treinador = TreinadorAguardandoPagamento();
        var assinatura = AssinaturaPendente(treinador.Id);
        var existente = PagamentoTreinador.Criar(
            treinador.Id, assinatura.Id, assinatura.Valor, FinalidadePagamentoTreinador.Cadastro, DateTime.UtcNow).Value;
        existente.DefinirDadosPix("pi_old", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        Arrange(treinador, assinatura, existente);

        var result = await _handler.HandleAsync(new IniciarPagamentoPlanoCommand(treinador.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.PagamentoId.Should().Be(existente.Id);
        result.Value.StripePaymentIntentId.Should().Be("pi_old");
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _pagamentoRepo.Verify(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PagamentoZumbi_MarcaFalhouECriaNovoCharge()
    {
        var treinador = TreinadorAguardandoPagamento();
        var assinatura = AssinaturaPendente(treinador.Id);
        var zumbi = PagamentoTreinador.Criar(
            treinador.Id, assinatura.Id, assinatura.Valor, FinalidadePagamentoTreinador.Cadastro, DateTime.UtcNow).Value;
        Arrange(treinador, assinatura, zumbi);

        var result = await _handler.HandleAsync(new IniciarPagamentoPlanoCommand(treinador.Id));

        result.IsSuccess.Should().BeTrue();
        zumbi.Status.Should().Be(PagamentoStatus.Falhou);
        result.Value.StripePaymentIntentId.Should().Be("pi_plat_123");
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
