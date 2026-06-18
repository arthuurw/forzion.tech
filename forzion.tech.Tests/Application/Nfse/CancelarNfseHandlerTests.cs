using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Application.UseCases.Nfse.CancelarNfse;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Nfse;

public class CancelarNfseHandlerTests
{
    private static readonly DateTimeOffset Instante = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Agora = Instante.UtcDateTime;

    private readonly Mock<INotaFiscalRepository> _notaRepo = new();
    private readonly Mock<IOutboxEnfileirador> _enfileirador = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CancelarNfseHandler _handler;

    public CancelarNfseHandlerTests()
    {
        _handler = new CancelarNfseHandler(
            _notaRepo.Object,
            _enfileirador.Object,
            _unitOfWork.Object,
            new FakeTimeProvider(Instante),
            Mock.Of<ILogger<CancelarNfseHandler>>());
    }

    private static NotaFiscal NotaEmitida(Guid pagamentoTreinadorId)
    {
        var nota = NotaFiscal.CriarAssinatura(Guid.NewGuid(), pagamentoTreinadorId, 99.90m, Agora).Value;
        nota.MarcarEmitida("CHV-1", "10", Agora, null, Agora);
        return nota;
    }

    [Fact]
    public async Task Estorno_NotaEmitida_SolicitaCancelamentoEEnfileira()
    {
        var pagamentoId = Guid.NewGuid();
        var nota = NotaEmitida(pagamentoId);
        _notaRepo.Setup(r => r.ObterPorPagamentoTreinadorAsync(pagamentoId, It.IsAny<CancellationToken>())).ReturnsAsync(nota);
        string? chave = null;
        _enfileirador.Setup(e => e.Enfileirar("fx:cancelar_nfse", It.IsAny<CancelarNfsePayload>(), It.IsAny<string>()))
            .Callback<string, CancelarNfsePayload, string>((_, _, c) => chave = c);

        await _handler.HandleAsync(new PagamentoTreinadorEstornadoEvent(pagamentoId, Guid.NewGuid(), 99.90m, Agora));

        nota.Status.Should().Be(NotaFiscalStatus.CancelamentoSolicitado);
        chave.Should().Be($"fx:cancelar_nfse:{nota.Id}");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Disputa_NotaEmitida_SolicitaCancelamento()
    {
        var pagamentoId = Guid.NewGuid();
        var nota = NotaEmitida(pagamentoId);
        _notaRepo.Setup(r => r.ObterPorPagamentoTreinadorAsync(pagamentoId, It.IsAny<CancellationToken>())).ReturnsAsync(nota);

        await _handler.HandleAsync(new PagamentoTreinadorEmDisputaEvent(pagamentoId, Guid.NewGuid(), 99.90m, Agora));

        nota.Status.Should().Be(NotaFiscalStatus.CancelamentoSolicitado);
        _enfileirador.Verify(e => e.Enfileirar("fx:cancelar_nfse", It.IsAny<CancelarNfsePayload>(), It.IsAny<string>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EstornoAntesDaEmissao_RegistraCancelamentoPendentePreEmissao()
    {
        var pagamentoId = Guid.NewGuid();
        var nota = NotaFiscal.CriarAssinatura(Guid.NewGuid(), pagamentoId, 99.90m, Agora).Value;
        _notaRepo.Setup(r => r.ObterPorPagamentoTreinadorAsync(pagamentoId, It.IsAny<CancellationToken>())).ReturnsAsync(nota);

        await _handler.HandleAsync(new PagamentoTreinadorEstornadoEvent(pagamentoId, Guid.NewGuid(), 99.90m, Agora));

        nota.Status.Should().Be(NotaFiscalStatus.Pendente);
        nota.CancelamentoPendentePreEmissao.Should().BeTrue();
        nota.MotivoCancelamentoPendente.Should().NotBeNullOrEmpty();
        _enfileirador.Verify(e => e.Enfileirar(It.IsAny<string>(), It.IsAny<CancelarNfsePayload>(), It.IsAny<string>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotaJaCancelada_NaoAplica()
    {
        var pagamentoId = Guid.NewGuid();
        var nota = NotaEmitida(pagamentoId);
        nota.SolicitarCancelamento(Agora);
        nota.MarcarCancelada(Agora);
        _notaRepo.Setup(r => r.ObterPorPagamentoTreinadorAsync(pagamentoId, It.IsAny<CancellationToken>())).ReturnsAsync(nota);

        await _handler.HandleAsync(new PagamentoTreinadorEstornadoEvent(pagamentoId, Guid.NewGuid(), 99.90m, Agora));

        nota.CancelamentoPendentePreEmissao.Should().BeFalse();
        _enfileirador.Verify(e => e.Enfileirar(It.IsAny<string>(), It.IsAny<CancelarNfsePayload>(), It.IsAny<string>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SemNota_Ignora()
    {
        var pagamentoId = Guid.NewGuid();
        _notaRepo.Setup(r => r.ObterPorPagamentoTreinadorAsync(pagamentoId, It.IsAny<CancellationToken>())).ReturnsAsync((NotaFiscal?)null);

        await _handler.HandleAsync(new PagamentoTreinadorEstornadoEvent(pagamentoId, Guid.NewGuid(), 99.90m, Agora));

        _enfileirador.Verify(e => e.Enfileirar(It.IsAny<string>(), It.IsAny<CancelarNfsePayload>(), It.IsAny<string>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
