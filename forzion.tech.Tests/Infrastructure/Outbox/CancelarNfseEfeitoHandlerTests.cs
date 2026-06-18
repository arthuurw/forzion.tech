using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Outbox.Handlers;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Outbox;

public class CancelarNfseEfeitoHandlerTests
{
    private static readonly DateTimeOffset Instante = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Agora = Instante.UtcDateTime;
    private static readonly DateTime Emissao = Agora.AddDays(-1);

    private readonly Mock<INotaFiscalRepository> _notaRepo = new();
    private readonly Mock<IEmissorNfseService> _emissor = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CancelarNfseEfeitoHandler _handler;

    public CancelarNfseEfeitoHandlerTests()
    {
        _handler = new CancelarNfseEfeitoHandler(
            _notaRepo.Object,
            _emissor.Object,
            Options.Create(new NfseSettings { PrazoCancelamentoDias = 90 }),
            _unitOfWork.Object,
            new FakeTimeProvider(Instante),
            Mock.Of<ILogger<CancelarNfseEfeitoHandler>>());
    }

    private NotaFiscal NotaSolicitada(DateTime emissao)
    {
        var nota = NotaFiscal.CriarAssinatura(Guid.NewGuid(), Guid.NewGuid(), 99.90m, emissao).Value;
        nota.MarcarEmitida("CHV-1", "10", emissao, null, emissao);
        nota.SolicitarCancelamento(emissao);
        _notaRepo.Setup(r => r.ObterPorIdAsync(nota.Id, It.IsAny<CancellationToken>())).ReturnsAsync(nota);
        return nota;
    }

    private static string Payload(Guid notaId) =>
        JsonSerializer.Serialize(new CancelarNfsePayload(notaId, "Cancelamento por estorno do pagamento ao prestador."));

    [Fact]
    public async Task Sucesso_MarcaCancelada()
    {
        var nota = NotaSolicitada(Emissao);
        _emissor.Setup(e => e.CancelarAsync("CHV-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NfseResultado(true, "CHV-1", null, Agora, null, null, null));

        await _handler.ExecutarAsync(Payload(nota.Id));

        nota.Status.Should().Be(NotaFiscalStatus.Cancelada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rejeicao_MarcaCancelamentoExpirado()
    {
        var nota = NotaSolicitada(Emissao);
        _emissor.Setup(e => e.CancelarAsync("CHV-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NfseResultado(false, null, null, null, null, "E8001", "prazo expirado"));

        await _handler.ExecutarAsync(Payload(nota.Id));

        nota.Status.Should().Be(NotaFiscalStatus.CancelamentoExpirado);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrazoExpiradoLocal_NaoChamaEmissorEMarcaExpirado()
    {
        var nota = NotaSolicitada(Agora.AddDays(-120));

        await _handler.ExecutarAsync(Payload(nota.Id));

        nota.Status.Should().Be(NotaFiscalStatus.CancelamentoExpirado);
        _emissor.Verify(e => e.CancelarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Excecao_PropagaParaRetrySemTransicao()
    {
        var nota = NotaSolicitada(Emissao);
        _emissor.Setup(e => e.CancelarAsync("CHV-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("5xx"));

        var act = () => _handler.ExecutarAsync(Payload(nota.Id));

        await act.Should().ThrowAsync<HttpRequestException>();
        nota.Status.Should().Be(NotaFiscalStatus.CancelamentoSolicitado);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StatusNaoSolicitado_Ignora()
    {
        var nota = NotaFiscal.CriarAssinatura(Guid.NewGuid(), Guid.NewGuid(), 99.90m, Emissao).Value;
        nota.MarcarEmitida("CHV-1", "10", Emissao, null, Emissao);
        _notaRepo.Setup(r => r.ObterPorIdAsync(nota.Id, It.IsAny<CancellationToken>())).ReturnsAsync(nota);

        await _handler.ExecutarAsync(Payload(nota.Id));

        _emissor.Verify(e => e.CancelarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotaInexistente_Ignora()
    {
        var id = Guid.NewGuid();
        _notaRepo.Setup(r => r.ObterPorIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((NotaFiscal?)null);

        await _handler.ExecutarAsync(Payload(id));

        _emissor.Verify(e => e.CancelarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
