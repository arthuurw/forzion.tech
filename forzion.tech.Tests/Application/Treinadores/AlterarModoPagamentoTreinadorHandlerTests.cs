using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Application.UseCases.Treinadores.AlterarModoPagamento;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class AlterarModoPagamentoTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IStripeService> _stripe = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IDbContextTransactionProvider> _txProvider = new();
    private readonly Mock<ILogger<AlterarModoPagamentoTreinadorHandler>> _logger = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero));

    public AlterarModoPagamentoTreinadorHandlerTests()
    {
        var tx = new Mock<ITransaction>();
        tx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        tx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _txProvider.Setup(p => p.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        _assinaturaRepo.Setup(r => r.ListarNaoCanceladasPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _vinculoRepo.Setup(r => r.ListarAtivosPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private AlterarModoPagamentoTreinadorHandler CriarHandler() => new(
        _treinadorRepo.Object, _contaRepo.Object, _assinaturaRepo.Object, _pagamentoRepo.Object,
        _vinculoRepo.Object,
        new CriarAssinaturaAlunoService(_pacoteRepo.Object, _assinaturaRepo.Object, Mock.Of<ILogger<CriarAssinaturaAlunoService>>()),
        _stripe.Object, _uow.Object, _txProvider.Object,
        new AlterarModoPagamentoTreinadorCommandValidator(),
        _time, _logger.Object, _logRepo.Object);

    private void SetTreinador(Treinador treinador) =>
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

    private static ContaRecebimento ContaOnboarded()
    {
        var conta = ContaRecebimento.Criar(Guid.NewGuid(), DateTime.UtcNow).Value;
        conta.ConfigurarStripeConnect("acct_x", DateTime.UtcNow);
        conta.ConfirmarOnboarding(DateTime.UtcNow);
        return conta;
    }

    [Fact]
    public async Task ParaExterno_CancelaAssinaturasECancelaPaymentIntentPendente()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "T", _time.GetUtcNow().UtcDateTime, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value;
        SetTreinador(treinador);

        var assinatura = new AssinaturaAlunoBuilder().ComTreinadorId(treinador.Id).Build();
        _assinaturaRepo.Setup(r => r.ListarNaoCanceladasPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([assinatura]);
        var pagamento = Pagamento.Criar(assinatura.Id, 100m, _time.GetUtcNow().UtcDateTime).Value;
        pagamento.DefinirDadosPix("pi_123", "qr", "url", _time.GetUtcNow().UtcDateTime.AddHours(1), _time.GetUtcNow().UtcDateTime);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(treinador.Id, ModoPagamentoAluno.Externo));

        result.IsSuccess.Should().BeTrue();
        treinador.ModoPagamentoAluno.Should().Be(ModoPagamentoAluno.Externo);
        assinatura.Status.Should().Be(AssinaturaAlunoStatus.Cancelada);
        _stripe.Verify(s => s.CancelarPaymentIntentAsync("pi_123", It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        assinatura.DomainEvents.Should().BeEmpty();
        _logRepo.Verify(r => r.AdicionarAsync(It.Is<LogAprovacao>(l => l.TipoAcao == TipoAcaoAprovacao.AlteracaoModoPagamentoTreinador), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ParaExterno_CancelaPaymentIntent_DepoisDoCommit()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "T", _time.GetUtcNow().UtcDateTime, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value;
        SetTreinador(treinador);
        var assinatura = new AssinaturaAlunoBuilder().ComTreinadorId(treinador.Id).Build();
        _assinaturaRepo.Setup(r => r.ListarNaoCanceladasPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([assinatura]);
        var pagamento = Pagamento.Criar(assinatura.Id, 100m, _time.GetUtcNow().UtcDateTime).Value;
        pagamento.DefinirDadosPix("pi_seq", "qr", "url", _time.GetUtcNow().UtcDateTime.AddHours(1), _time.GetUtcNow().UtcDateTime);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var seq = new MockSequence();
        _uow.InSequence(seq).Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _stripe.InSequence(seq).Setup(s => s.CancelarPaymentIntentAsync("pi_seq", It.IsAny<CancellationToken>())).ReturnsAsync(CancelarPaymentIntentResultado.Cancelado);

        var result = await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(treinador.Id, ModoPagamentoAluno.Externo));

        result.IsSuccess.Should().BeTrue();
        _stripe.Verify(s => s.CancelarPaymentIntentAsync("pi_seq", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ParaPlataforma_SemOnboarding_Falha()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "T", _time.GetUtcNow().UtcDateTime, modoPagamentoAluno: ModoPagamentoAluno.Externo).Value;
        SetTreinador(treinador);
        _contaRepo.Setup(r => r.ObterPorTreinadorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);

        var result = await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(treinador.Id, ModoPagamentoAluno.Plataforma));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador.configure_stripe_primeiro");
        treinador.ModoPagamentoAluno.Should().Be(ModoPagamentoAluno.Externo);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ParaPlataforma_ComOnboarding_CriaAssinaturasParaVinculosAtivos()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "T", _time.GetUtcNow().UtcDateTime, modoPagamentoAluno: ModoPagamentoAluno.Externo).Value;
        SetTreinador(treinador);
        _contaRepo.Setup(r => r.ObterPorTreinadorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded());

        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).ComPreco(120m).Build();
        var vinculo = new VinculoTreinadorAlunoBuilder().ComTreinadorId(treinador.Id).ComPacoteId(pacote.Id).Build();
        _vinculoRepo.Setup(r => r.ListarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([vinculo]);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var result = await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(treinador.Id, ModoPagamentoAluno.Plataforma));

        result.IsSuccess.Should().BeTrue();
        treinador.ModoPagamentoAluno.Should().Be(ModoPagamentoAluno.Plataforma);
        result.Value.AssinaturasCriadas.Should().Be(1);
        result.Value.VinculosIgnorados.Should().Be(0);
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.Is<AssinaturaAluno>(a => a.VinculoId == vinculo.Id && a.Valor == 120m), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ParaPlataforma_PacoteInativo_IgnoraVinculo()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "T", _time.GetUtcNow().UtcDateTime, modoPagamentoAluno: ModoPagamentoAluno.Externo).Value;
        SetTreinador(treinador);
        _contaRepo.Setup(r => r.ObterPorTreinadorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded());

        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.Inativar(_time.GetUtcNow().UtcDateTime);
        var vinculo = new VinculoTreinadorAlunoBuilder().ComTreinadorId(treinador.Id).ComPacoteId(pacote.Id).Build();
        _vinculoRepo.Setup(r => r.ListarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([vinculo]);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var result = await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(treinador.Id, ModoPagamentoAluno.Plataforma));

        result.IsSuccess.Should().BeTrue();
        result.Value.AssinaturasCriadas.Should().Be(0);
        result.Value.VinculosIgnorados.Should().Be(1);
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ParaPlataforma_PacotePrecoZero_IgnoraVinculo()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "T", _time.GetUtcNow().UtcDateTime, modoPagamentoAluno: ModoPagamentoAluno.Externo).Value;
        SetTreinador(treinador);
        _contaRepo.Setup(r => r.ObterPorTreinadorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded());

        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).ComPreco(0m).Build();
        var vinculo = new VinculoTreinadorAlunoBuilder().ComTreinadorId(treinador.Id).ComPacoteId(pacote.Id).Build();
        _vinculoRepo.Setup(r => r.ListarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([vinculo]);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var result = await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(treinador.Id, ModoPagamentoAluno.Plataforma));

        result.IsSuccess.Should().BeTrue();
        result.Value.VinculosIgnorados.Should().Be(1);
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DentroDoCooldown_Falha()
    {
        var baseTime = _time.GetUtcNow().UtcDateTime;
        var treinador = Treinador.Criar(Guid.NewGuid(), "T", baseTime, modoPagamentoAluno: ModoPagamentoAluno.Externo).Value;
        treinador.AlterarModoPagamento(ModoPagamentoAluno.Plataforma, baseTime);
        SetTreinador(treinador);
        _contaRepo.Setup(r => r.ObterPorTreinadorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded());
        _time.SetUtcNow(new DateTimeOffset(baseTime.AddDays(10), TimeSpan.Zero));

        var result = await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(treinador.Id, ModoPagamentoAluno.Externo));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador.cooldown_modo_pagamento");
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MesmoModo_Falha()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "T", _time.GetUtcNow().UtcDateTime, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value;
        SetTreinador(treinador);

        var result = await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(treinador.Id, ModoPagamentoAluno.Plataforma));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador.modo_inalterado");
    }

    [Fact]
    public async Task EnumForaDeRange_LancaValidacao_NaoMutaNemComita()
    {
        var act = async () => await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(Guid.NewGuid(), (ModoPagamentoAluno)99));

        await act.Should().ThrowAsync<ValidationException>();
        _treinadorRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ParaExterno_PendenteSemPaymentIntent_ExpiraPagamentoNaoChamaStripe()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "T", _time.GetUtcNow().UtcDateTime, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value;
        SetTreinador(treinador);
        var assinatura = new AssinaturaAlunoBuilder().ComTreinadorId(treinador.Id).Build();
        _assinaturaRepo.Setup(r => r.ListarNaoCanceladasPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([assinatura]);
        var pagamento = Pagamento.Criar(assinatura.Id, 100m, _time.GetUtcNow().UtcDateTime).Value;
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);

        var result = await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(treinador.Id, ModoPagamentoAluno.Externo));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Expirado);
        _stripe.Verify(s => s.CancelarPaymentIntentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ParaExterno_PaymentIntentJaCapturado_LogCriticalESegue()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "T", _time.GetUtcNow().UtcDateTime, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value;
        SetTreinador(treinador);
        var assinatura = new AssinaturaAlunoBuilder().ComTreinadorId(treinador.Id).Build();
        _assinaturaRepo.Setup(r => r.ListarNaoCanceladasPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([assinatura]);
        var pagamento = Pagamento.Criar(assinatura.Id, 100m, _time.GetUtcNow().UtcDateTime).Value;
        pagamento.DefinirDadosPix("pi_cap", "qr", "url", _time.GetUtcNow().UtcDateTime.AddHours(1), _time.GetUtcNow().UtcDateTime);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _stripe.Setup(s => s.CancelarPaymentIntentAsync("pi_cap", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CancelarPaymentIntentResultado.JaCapturado);

        var result = await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(treinador.Id, ModoPagamentoAluno.Externo));

        result.IsSuccess.Should().BeTrue();
        _logger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ParaExterno_PaymentIntentJaCancelado_SemLogCritical()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "T", _time.GetUtcNow().UtcDateTime, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value;
        SetTreinador(treinador);
        var assinatura = new AssinaturaAlunoBuilder().ComTreinadorId(treinador.Id).Build();
        _assinaturaRepo.Setup(r => r.ListarNaoCanceladasPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([assinatura]);
        var pagamento = Pagamento.Criar(assinatura.Id, 100m, _time.GetUtcNow().UtcDateTime).Value;
        pagamento.DefinirDadosPix("pi_cancel", "qr", "url", _time.GetUtcNow().UtcDateTime.AddHours(1), _time.GetUtcNow().UtcDateTime);
        _pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _stripe.Setup(s => s.CancelarPaymentIntentAsync("pi_cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CancelarPaymentIntentResultado.JaCancelado);

        var result = await CriarHandler().HandleAsync(
            new AlterarModoPagamentoTreinadorCommand(treinador.Id, ModoPagamentoAluno.Externo));

        result.IsSuccess.Should().BeTrue();
        _logger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
