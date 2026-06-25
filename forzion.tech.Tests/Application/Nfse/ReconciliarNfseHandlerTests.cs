using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Nfse.ReconciliarNfse;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Nfse;

public class ReconciliarNfseHandlerTests
{
    private static readonly DateTimeOffset Instante = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly Mock<INotaFiscalRepository> _notaRepo = new();
    private readonly Mock<IEmissorNfseService> _emissor = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(Instante);
    private readonly ReconciliarNfseHandler _handler;

    public ReconciliarNfseHandlerTests()
    {
        _notaRepo.Setup(r => r.ListarPorStatusAsync(
                It.IsAny<NotaFiscalStatus>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var scopeFactory = new ServiceCollection()
            .AddSingleton(_notaRepo.Object)
            .AddSingleton(_unitOfWork.Object)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        _handler = new ReconciliarNfseHandler(
            _notaRepo.Object, _emissor.Object, scopeFactory, _time,
            Mock.Of<ILogger<ReconciliarNfseHandler>>());
    }

    private void SetupStatus(NotaFiscalStatus status, params NotaFiscal[] notas)
    {
        _notaRepo.Setup(r => r.ListarPorStatusAsync(status, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notas);
        foreach (var nota in notas)
            _notaRepo.Setup(r => r.ObterPorIdAsync(nota.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(nota);
    }

    private NotaFiscal NotaCancelamentoSolicitado(string chave = "CHV-1")
    {
        var nota = NotaFiscal.CriarAssinatura(Guid.NewGuid(), Guid.NewGuid(), 50m, Instante.UtcDateTime).Value;
        nota.MarcarEmitida(chave, "100", Instante.UtcDateTime, null, Instante.UtcDateTime);
        nota.SolicitarCancelamento(Instante.UtcDateTime);
        return nota;
    }

    private static NfseStatus Gov(NfseSituacao situacao) =>
        new(situacao, null, null, null, null, null);

    [Fact]
    public async Task GovCancelada_MarcaCanceladaECommita()
    {
        var nota = NotaCancelamentoSolicitado();
        SetupStatus(NotaFiscalStatus.CancelamentoSolicitado, nota);
        _emissor.Setup(e => e.ConsultarAsync("CHV-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Gov(NfseSituacao.Cancelada));

        var result = await _handler.HandleAsync(new ReconciliarNfseCommand());

        result.Value.Consultadas.Should().Be(1);
        result.Value.Atualizadas.Should().Be(1);
        nota.Status.Should().Be(NotaFiscalStatus.Cancelada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GovAindaAutorizada_NaoAltera()
    {
        var nota = NotaCancelamentoSolicitado();
        SetupStatus(NotaFiscalStatus.CancelamentoSolicitado, nota);
        _emissor.Setup(e => e.ConsultarAsync("CHV-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Gov(NfseSituacao.Autorizada));

        var result = await _handler.HandleAsync(new ReconciliarNfseCommand());

        result.Value.Consultadas.Should().Be(1);
        result.Value.Atualizadas.Should().Be(0);
        result.Value.SemAlteracao.Should().Be(1);
        nota.Status.Should().Be(NotaFiscalStatus.CancelamentoSolicitado);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotaSemChave_NaoConsultaGov()
    {
        var pendente = NotaFiscal.CriarAssinatura(Guid.NewGuid(), Guid.NewGuid(), 50m, Instante.UtcDateTime).Value;
        SetupStatus(NotaFiscalStatus.Pendente, pendente);

        var result = await _handler.HandleAsync(new ReconciliarNfseCommand());

        result.Value.Consultadas.Should().Be(0);
        _emissor.Verify(e => e.ConsultarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsultaLancaExcecao_ContaErroESegue()
    {
        var nota = NotaCancelamentoSolicitado();
        SetupStatus(NotaFiscalStatus.CancelamentoSolicitado, nota);
        _emissor.Setup(e => e.ConsultarAsync("CHV-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        var result = await _handler.HandleAsync(new ReconciliarNfseCommand());

        result.Value.Consultadas.Should().Be(1);
        result.Value.Erros.Should().Be(1);
        result.Value.Atualizadas.Should().Be(0);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
