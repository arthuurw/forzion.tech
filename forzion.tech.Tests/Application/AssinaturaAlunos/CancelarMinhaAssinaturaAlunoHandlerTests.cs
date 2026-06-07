using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Application.UseCases.AssinaturaAlunos.CancelarMinhaAssinaturaAluno;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.AssinaturaAlunos;

public class CancelarMinhaAssinaturaAlunoHandlerTests
{
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IStripeService> _stripeService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CancelarMinhaAssinaturaAlunoHandler>> _logger = new();
    private readonly Mock<ILogger<ReembolsoArrependimentoService>> _reembolsoLogger = new();
    private readonly CancelarMinhaAssinaturaAlunoHandler _handler;

    private static readonly Guid AlunoId = TestData.NextGuid();

    public CancelarMinhaAssinaturaAlunoHandlerTests()
    {
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var reembolsoService = new ReembolsoArrependimentoService(
            _stripeService.Object, _reembolsoLogger.Object);
        _handler = new CancelarMinhaAssinaturaAlunoHandler(
            _assinaturaRepo.Object, _pagamentoRepo.Object, reembolsoService,
            _unitOfWork.Object, TimeProvider.System, _logger.Object);
    }

    private static AssinaturaAluno CriarAtiva(DateTime? dataInicio = null)
    {
        var inicio = dataInicio ?? TestData.Agora;
        var a = new AssinaturaAlunoBuilder().ComAlunoId(AlunoId).Em(inicio).Build();
        a.Ativar(inicio);
        a.ClearDomainEvents();
        return a;
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAtiva_CancelaECommita()
    {
        var assinatura = CriarAtiva();
        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsSuccess.Should().BeTrue();
        assinatura.Status.Should().Be(forzion.tech.Domain.Enums.AssinaturaAlunoStatus.Cancelada);
        assinatura.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AssinaturaAlunoCanceladaEvent>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SemAssinatura_RetornaFailureComCodigoNaoEncontrada()
    {
        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(CancelarMinhaAssinaturaAlunoHandler.AssinaturaNaoEncontradaErrorCode);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaJaCancelada_RetornaFailureSemCommit()
    {
        var assinatura = new AssinaturaAlunoBuilder().ComAlunoId(AlunoId).Build();
        assinatura.Ativar(TestData.Agora);
        assinatura.Cancelar(TestData.Agora);
        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(CancelarMinhaAssinaturaAlunoHandler.AssinaturaNaoEncontradaErrorCode);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaInadimplente_PermiteCancelar()
    {
        var assinatura = new AssinaturaAlunoBuilder().ComAlunoId(AlunoId).Build();
        assinatura.Ativar(TestData.Agora);
        assinatura.MarcarInadimplente(TestData.Agora);
        assinatura.ClearDomainEvents();
        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsSuccess.Should().BeTrue();
        assinatura.Status.Should().Be(forzion.tech.Domain.Enums.AssinaturaAlunoStatus.Cancelada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_DentroDe7Dias_CriaReembolsoComReverterTransferencia()
    {
        var inicio = DateTime.UtcNow.AddDays(-2);
        var assinatura = CriarAtiva(inicio);
        var pagamento = Pagamento.Criar(assinatura.Id, 150m, inicio).Value;
        pagamento.DefinirDadosPix("pi_cdc", "qr", "url", inicio.AddHours(1), inicio);
        pagamento.MarcarPago(inicio);

        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pagamento]);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsSuccess.Should().BeTrue();
        assinatura.Status.Should().Be(forzion.tech.Domain.Enums.AssinaturaAlunoStatus.Cancelada);
        _stripeService.Verify(s => s.CriarReembolsoAsync("pi_cdc", true, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DentroDe7Dias_ReembolsaPagamentoPagoMaisAntigo()
    {
        var inicio = DateTime.UtcNow.AddDays(-3);
        var assinatura = CriarAtiva(inicio);

        var antigo = Pagamento.Criar(assinatura.Id, 150m, inicio).Value;
        antigo.DefinirDadosPix("pi_antigo", "qr", "url", inicio.AddHours(1), inicio);
        antigo.MarcarPago(inicio);

        var recente = Pagamento.Criar(assinatura.Id, 150m, inicio.AddDays(1)).Value;
        recente.DefinirDadosPix("pi_recente", "qr", "url", inicio.AddDays(1).AddHours(1), inicio.AddDays(1));
        recente.MarcarPago(inicio.AddDays(1));

        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([recente, antigo]);

        await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        _stripeService.Verify(s => s.CriarReembolsoAsync("pi_antigo", true, It.IsAny<CancellationToken>()), Times.Once);
        _stripeService.Verify(s => s.CriarReembolsoAsync("pi_recente", It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PagamentoPagoForaDoPrazo_NaoCriaReembolso()
    {
        var inicio = DateTime.UtcNow.AddDays(-30);
        var assinatura = CriarAtiva(inicio);
        var pagamento = Pagamento.Criar(assinatura.Id, 150m, inicio).Value;
        pagamento.DefinirDadosPix("pi_antigo", "qr", "url", inicio.AddHours(1), inicio);
        pagamento.MarcarPago(inicio);

        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pagamento]);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsSuccess.Should().BeTrue();
        assinatura.Status.Should().Be(forzion.tech.Domain.Enums.AssinaturaAlunoStatus.Cancelada);
        _stripeService.Verify(s => s.CriarReembolsoAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PagamentoPagoMasAssinaturaAntiga_JanelaContaDoPagamentoNaoDaCriacao()
    {
        var dataInicio = DateTime.UtcNow.AddDays(-20);
        var dataPagamento = DateTime.UtcNow.AddDays(-2);
        var assinatura = CriarAtiva(dataInicio);
        var pagamento = Pagamento.Criar(assinatura.Id, 150m, dataInicio).Value;
        pagamento.DefinirDadosPix("pi_pagamento_recente", "qr", "url", dataPagamento.AddHours(1), dataInicio);
        pagamento.MarcarPago(dataPagamento);

        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pagamento]);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsSuccess.Should().BeTrue();
        _stripeService.Verify(s => s.CriarReembolsoAsync("pi_pagamento_recente", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FalhaNoReembolso_CancelaMesmoAssim()
    {
        var inicio = DateTime.UtcNow.AddDays(-1);
        var assinatura = CriarAtiva(inicio);
        var pagamento = Pagamento.Criar(assinatura.Id, 150m, inicio).Value;
        pagamento.DefinirDadosPix("pi_falha", "qr", "url", inicio.AddHours(1), inicio);
        pagamento.MarcarPago(inicio);

        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pagamento]);
        _stripeService.Setup(s => s.CriarReembolsoAsync("pi_falha", true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("stripe down"));

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsSuccess.Should().BeTrue();
        assinatura.Status.Should().Be(forzion.tech.Domain.Enums.AssinaturaAlunoStatus.Cancelada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _reembolsoLogger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommitFalha_NaoReembolsa()
    {
        var inicio = DateTime.UtcNow.AddDays(-1);
        var assinatura = CriarAtiva(inicio);
        var pagamento = Pagamento.Criar(assinatura.Id, 150m, inicio).Value;
        pagamento.DefinirDadosPix("pi_commit_falha", "qr", "url", inicio.AddHours(1), inicio);
        pagamento.MarcarPago(inicio);

        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pagamento]);
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = async () => await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _stripeService.Verify(s => s.CriarReembolsoAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DentroDe7Dias_SemPagamentoPago_NaoReembolsa()
    {
        var inicio = DateTime.UtcNow.AddDays(-2);
        var assinatura = CriarAtiva(inicio);
        var pendente = Pagamento.Criar(assinatura.Id, 150m, inicio).Value;
        pendente.DefinirDadosPix("pi_pend", "qr", "url", inicio.AddHours(1), inicio);

        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pendente]);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsSuccess.Should().BeTrue();
        _stripeService.Verify(s => s.CriarReembolsoAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
