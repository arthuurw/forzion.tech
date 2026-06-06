using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.TrocarPlanoTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.E2E;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class TrocarPlanoTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IPagamentoTreinadorRepository> _pagamentoRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDbContextTransactionProvider> _transactionProvider = new();
    private readonly TrocarPlanoTreinadorHandler _handler;

    private sealed class NoopTransaction : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static readonly PixPaymentResult PixResult = new("pi_troca_123", "qrcode", "https://img", DateTime.UtcNow.AddHours(1));

    public TrocarPlanoTreinadorHandlerTests()
    {
        _transactionProvider.Setup(p => p.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NoopTransaction());
        _stripeService.Setup(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PixResult);

        _handler = new TrocarPlanoTreinadorHandler(
            _treinadorRepo.Object, _assinaturaRepo.Object, _planoRepo.Object,
            _pagamentoRepo.Object, _stripeService.Object, _unitOfWork.Object,
            _transactionProvider.Object, TimeProvider.System,
            Mock.Of<ILogger<TrocarPlanoTreinadorHandler>>());
    }

    private static Treinador CriarTreinador(Guid? planoId = null)
    {
        var result = Treinador.Criar(Guid.NewGuid(), "Carlos Trainer", DateTime.UtcNow, null, planoId ?? Guid.NewGuid());
        return result.Value;
    }

    private static AssinaturaTreinador CriarAssinaturaAtiva(Guid treinadorId, Guid planoId, decimal valor = 50m)
    {
        var a = AssinaturaTreinador.Criar(treinadorId, planoId, valor, DateTime.UtcNow).Value;
        a.Ativar(DateTime.UtcNow);
        a.AgendarProximaCobranca(DateTime.UtcNow.AddDays(20), DateTime.UtcNow);
        return a;
    }

    private static PlanoPlataforma CriarPlano(decimal preco, TierPlano tier = TierPlano.Pro)
    {
        var p = PlanoPlataforma.Criar($"Plano {tier}", tier, 50, preco, DateTime.UtcNow).Value;
        return p;
    }

    [Fact]
    public async Task HandleAsync_Upgrade_GeraProracaoERetornaCheckout()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(50m, TierPlano.Basic);
        var planoNovo = CriarPlano(100m, TierPlano.Pro);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, planoAtual.Id, 50m);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoNovo.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoTrocaPlano.Upgrade);
        result.Value.PagamentoId.Should().NotBeNull();
        result.Value.ValorPagamento.Should().BeGreaterThan(0, "proração positiva com dias restantes no ciclo");
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Upgrade_ZeroDiasRestantes_AplicaImediato_SemPagamento()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(50m, TierPlano.Basic);
        var planoNovo = CriarPlano(100m, TierPlano.Pro);
        var assinatura = AssinaturaTreinador.Criar(treinador.Id, planoAtual.Id, 50m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);
        // DataProximaCobranca = agora (0 dias restantes)
        assinatura.AgendarProximaCobranca(DateTime.UtcNow.AddSeconds(1), DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoNovo.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoTrocaPlano.UpgradeImediato, "upgrade sem cobrança deve retornar UpgradeImediato, não Downgrade");
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Upgrade_PendenteParaPlanoDiferente_DescartaAntesECriaNovo()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(30m, TierPlano.Basic);
        var planoA = CriarPlano(80m, TierPlano.Pro);
        var planoB = CriarPlano(100m, TierPlano.ProPlus);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, planoAtual.Id, 30m);
        var pendenteA = PagamentoTreinador.Criar(treinador.Id, assinatura.Id, 50m, FinalidadePagamentoTreinador.TrocaPlano, DateTime.UtcNow, MetodoPagamento.Pix, planoA.Id).Value;
        pendenteA.DefinirDadosPix("pi_planoA", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoB.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoB);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendenteA);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoB.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoTrocaPlano.Upgrade);
        pendenteA.Status.Should().Be(PagamentoStatus.Falhou, "pending para plano diferente deve ser descartado para evitar aplicação de plano errado via webhook tardio");
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once, "novo intent criado para PlanoB");
    }

    [Fact]
    public async Task HandleAsync_Inadimplente_PendenteParaPlanoDiferente_DescartaAntesECriaNovo()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(30m, TierPlano.Basic);
        var planoA = CriarPlano(80m, TierPlano.Pro);
        var planoB = CriarPlano(100m, TierPlano.ProPlus);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, planoAtual.Id, 30m);
        assinatura.MarcarInadimplente(DateTime.UtcNow);

        var pendenteA = PagamentoTreinador.Criar(treinador.Id, assinatura.Id, 80m, FinalidadePagamentoTreinador.TrocaPlano, DateTime.UtcNow, MetodoPagamento.Pix, planoA.Id).Value;
        pendenteA.DefinirDadosPix("pi_inadimplente_planoA", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoB.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoB);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendenteA);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoB.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoTrocaPlano.InadimplenteRegularizacao);
        pendenteA.Status.Should().Be(PagamentoStatus.Falhou, "pending para plano diferente deve ser descartado");
    }

    [Fact]
    public async Task HandleAsync_Downgrade_AgendaERetornaDataEfetivacao()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(100m, TierPlano.Pro);
        var planoNovo = CriarPlano(50m, TierPlano.Basic);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, planoAtual.Id, 100m);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoNovo.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoTrocaPlano.Downgrade);
        result.Value.DataEfetivacao.Should().NotBeNull("downgrade agendado tem data de efetivação");
        result.Value.PagamentoId.Should().BeNull("downgrade não gera pagamento");
        assinatura.PlanoPlataformaIdAgendado.Should().Be(planoNovo.Id);
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Inadimplente ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Inadimplente_CobraPlanoCheioPararRegularizar()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(50m, TierPlano.Basic);
        var planoNovo = CriarPlano(80m, TierPlano.Pro);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, planoAtual.Id, 50m);
        assinatura.MarcarInadimplente(DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoNovo.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoTrocaPlano.InadimplenteRegularizacao);
        result.Value.ValorPagamento.Should().Be(80m, "cobra o preço cheio do novo plano");
        result.Value.PagamentoId.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_MesmoPlano_RetornaFailure()
    {
        var treinador = CriarTreinador();
        var plano = CriarPlano(50m);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, plano.Id, 50m);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, plano.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("mesmo_plano");
    }

    [Fact]
    public async Task HandleAsync_PlanoElite_RetornaFailure()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(50m, TierPlano.Basic);
        var planoElite = CriarPlano(500m, TierPlano.Elite);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, planoAtual.Id, 50m);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoElite.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoElite);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoElite.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("plano_plataforma.elite_indisponivel");
    }

    [Fact]
    public async Task HandleAsync_AssinaturaCancelada_RetornaFailure()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(50m, TierPlano.Basic);
        var planoNovo = CriarPlano(100m, TierPlano.Pro);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, planoAtual.Id, 50m);
        assinatura.Cancelar(DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoNovo.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assinatura_treinador.ja_cancelada");
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaExcecao()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_PlanoNaoEncontrado_RetornaFailure()
    {
        var treinador = CriarTreinador();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlanoPlataforma?)null);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("plano_plataforma_nao_encontrado");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_Upgrade_ProracaoCalculadaComDiasRestantes()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(30m, TierPlano.Basic);
        var planoNovo = CriarPlano(90m, TierPlano.Pro);
        var agora = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var assinatura = AssinaturaTreinador.Criar(treinador.Id, planoAtual.Id, 30m, agora).Value;
        assinatura.Ativar(agora);
        assinatura.AgendarProximaCobranca(agora.AddDays(15), agora); // 15 dias restantes — (90-30)*15/30 = R$30

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(agora);
        var handler = new TrocarPlanoTreinadorHandler(
            _treinadorRepo.Object, _assinaturaRepo.Object, _planoRepo.Object,
            _pagamentoRepo.Object, _stripeService.Object, _unitOfWork.Object,
            _transactionProvider.Object, fakeTime,
            Mock.Of<ILogger<TrocarPlanoTreinadorHandler>>());

        PagamentoTreinador? pagamentoAdicionado = null;
        _pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()))
            .Callback<PagamentoTreinador, CancellationToken>((p, _) => pagamentoAdicionado = p);

        var result = await handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoNovo.Id));

        result.IsSuccess.Should().BeTrue();
        pagamentoAdicionado.Should().NotBeNull();
        pagamentoAdicionado!.Valor.Should().Be(30.00m, "proração de 15/30 dias com diferença R$60");
    }

    [Fact]
    public async Task HandleAsync_Upgrade_MetodoCartao_GeraCartaoERetornaClientSecret()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(50m, TierPlano.Basic);
        var planoNovo = CriarPlano(100m, TierPlano.Pro);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, planoAtual.Id, 50m);

        var cartaoResult = new CartaoPaymentResult("pi_cartao_troca", "secret_troca");
        _stripeService.Setup(s => s.CriarCartaoPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cartaoResult);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoNovo.Id, MetodoPagamento.Cartao));

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoTrocaPlano.Upgrade);
        result.Value.ClientSecret.Should().Be("secret_troca");
        result.Value.PixQrCode.Should().BeNull();
        _stripeService.Verify(s => s.CriarCartaoPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Inadimplente_MetodoCartao_GeraCartaoERetornaClientSecret()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(50m, TierPlano.Basic);
        var planoNovo = CriarPlano(80m, TierPlano.Pro);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, planoAtual.Id, 50m);
        assinatura.MarcarInadimplente(DateTime.UtcNow);

        var cartaoResult = new CartaoPaymentResult("pi_cartao_inadimplente", "secret_inadimplente");
        _stripeService.Setup(s => s.CriarCartaoPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cartaoResult);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoNovo.Id, MetodoPagamento.Cartao));

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoTrocaPlano.InadimplenteRegularizacao);
        result.Value.ClientSecret.Should().Be("secret_inadimplente");
        _stripeService.Verify(s => s.CriarCartaoPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Upgrade_PendenteParaMesmoPlanoComIntent_Idempotente()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(50m, TierPlano.Basic);
        var planoNovo = CriarPlano(100m, TierPlano.Pro);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, planoAtual.Id, 50m);
        var pendenteExistente = PagamentoTreinador.Criar(
            treinador.Id, assinatura.Id, 50m, FinalidadePagamentoTreinador.TrocaPlano,
            DateTime.UtcNow, MetodoPagamento.Pix, planoNovo.Id).Value;
        pendenteExistente.DefinirDadosPix("pi_existente", "qr_old", "url_old", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendenteExistente);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoNovo.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.PagamentoId.Should().Be(pendenteExistente.Id, "idempotência: reutiliza o intent em curso");
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _pagamentoRepo.Verify(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Inadimplente_PendenteParaMesmoPlanoComIntent_Idempotente()
    {
        var treinador = CriarTreinador();
        var planoAtual = CriarPlano(50m, TierPlano.Basic);
        var planoNovo = CriarPlano(80m, TierPlano.Pro);
        var assinatura = CriarAssinaturaAtiva(treinador.Id, planoAtual.Id, 50m);
        assinatura.MarcarInadimplente(DateTime.UtcNow);
        var pendenteExistente = PagamentoTreinador.Criar(
            treinador.Id, assinatura.Id, 80m, FinalidadePagamentoTreinador.TrocaPlano,
            DateTime.UtcNow, MetodoPagamento.Pix, planoNovo.Id).Value;
        pendenteExistente.DefinirDadosPix("pi_inad_existente", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendenteExistente);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoNovo.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoTrocaPlano.InadimplenteRegularizacao);
        result.Value.PagamentoId.Should().Be(pendenteExistente.Id);
        _stripeService.Verify(s => s.CriarPixPlataformaPaymentIntentAsync(
            It.IsAny<decimal>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PlanoInativo_RetornaFailure()
    {
        var treinador = CriarTreinador();
        var planoInativo = PlanoPlataforma.Criar("Plano Inativo", TierPlano.Basic, 50, 50m, DateTime.UtcNow).Value;
        planoInativo.Inativar(DateTime.UtcNow);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoInativo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoInativo);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, planoInativo.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("plano_plataforma_inativo");
    }

    [Fact]
    public async Task HandleAsync_SemAssinatura_RetornaFailure()
    {
        var treinador = CriarTreinador();
        var plano = CriarPlano(50m);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);
        _assinaturaRepo.Setup(r => r.ObterAtualPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaTreinador?)null);

        var result = await _handler.HandleAsync(new TrocarPlanoTreinadorCommand(treinador.Id, plano.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("assinatura_treinador_nao_encontrada");
    }
}
