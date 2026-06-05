using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Handlers;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class PagamentoTreinadorPagoHandlerTests
{
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly PagamentoTreinadorPagoHandler _handler;

    public PagamentoTreinadorPagoHandlerTests()
    {
        _handler = new PagamentoTreinadorPagoHandler(
            _assinaturaRepo.Object,
            _planoRepo.Object,
            _unitOfWork.Object,
            TimeProvider.System,
            Mock.Of<ILogger<PagamentoTreinadorPagoHandler>>());
    }

    private static AssinaturaTreinador CriarAssinaturaAtiva()
    {
        var a = AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        a.Ativar(DateTime.UtcNow);
        return a;
    }

    private static PagamentoTreinadorPagoEvent CriarEvento(Guid assinaturaId, FinalidadePagamentoTreinador finalidade) =>
        new(Guid.NewGuid(), Guid.NewGuid(), assinaturaId, finalidade, null, DateTime.UtcNow);

    [Fact]
    public async Task HandleAsync_Renovacao_AssinaturaAtiva_RegularizaEAgendaProximaCobranca()
    {
        var assinatura = CriarAssinaturaAtiva();
        var evento = CriarEvento(assinatura.Id, FinalidadePagamentoTreinador.Renovacao);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        await _handler.HandleAsync(evento);

        assinatura.TentativasFalhasConsecutivas.Should().Be(0);
        assinatura.DataProximaCobranca.Should().BeAfter(DateTime.UtcNow.AddDays(20), "próxima cobrança deve ser ~1 mês no futuro");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Renovacao_AssinaturaInadimplente_ReativaEAgenda()
    {
        var assinatura = CriarAssinaturaAtiva();
        assinatura.MarcarInadimplente(DateTime.UtcNow);
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Inadimplente);

        var evento = CriarEvento(assinatura.Id, FinalidadePagamentoTreinador.Renovacao);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        await _handler.HandleAsync(evento);

        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Ativa, "regularização reativa assinatura inadimplente");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Cadastro_Skip_CommitNaoChamado()
    {
        var evento = CriarEvento(Guid.NewGuid(), FinalidadePagamentoTreinador.Cadastro);

        await _handler.HandleAsync(evento);

        _assinaturaRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TrocaPlano_Skip_CommitNaoChamado()
    {
        var evento = CriarEvento(Guid.NewGuid(), FinalidadePagamentoTreinador.TrocaPlano);

        await _handler.HandleAsync(evento);

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Renovacao_AssinaturaNaoEncontrada_RetornaSemCommit()
    {
        var evento = CriarEvento(Guid.NewGuid(), FinalidadePagamentoTreinador.Renovacao);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaTreinador?)null);

        await _handler.HandleAsync(evento);

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TrocaPlano_Ativa_AplicaTrocarPlanoImediato()
    {
        var planoNovo = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 100, 100m, DateTime.UtcNow).Value;
        var assinatura = CriarAssinaturaAtiva();
        var dataProximaCobrancaOriginal = DateTime.UtcNow.AddDays(15);
        assinatura.AgendarProximaCobranca(dataProximaCobrancaOriginal, DateTime.UtcNow);

        var evento = new PagamentoTreinadorPagoEvent(
            Guid.NewGuid(), assinatura.TreinadorId, assinatura.Id,
            FinalidadePagamentoTreinador.TrocaPlano, planoNovo.Id, DateTime.UtcNow);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);

        await _handler.HandleAsync(evento);

        assinatura.PlanoPlataformaId.Should().Be(planoNovo.Id, "plano deve ter sido trocado");
        assinatura.Valor.Should().Be(100m, "valor deve refletir novo plano");
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Ativa, "status não muda para troca de ativa");
        assinatura.DataProximaCobranca.Should().Be(dataProximaCobrancaOriginal, "DataProximaCobranca não deve ser alterada no upgrade de assinatura ativa — ciclo não reinicia");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TrocaPlano_PlanoNaoEncontrado_IgnoraSemCommit()
    {
        var assinatura = CriarAssinaturaAtiva();
        var evento = new PagamentoTreinadorPagoEvent(
            Guid.NewGuid(), assinatura.TreinadorId, assinatura.Id,
            FinalidadePagamentoTreinador.TrocaPlano, Guid.NewGuid(), DateTime.UtcNow);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _planoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlanoPlataforma?)null);

        await _handler.HandleAsync(evento);

        assinatura.PlanoPlataformaId.Should().NotBe(Guid.Empty, "plano original deve permanecer inalterado");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TrocaPlano_Inadimplente_ReativaETroca()
    {
        var planoNovo = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 100, 80m, DateTime.UtcNow).Value;
        var assinatura = CriarAssinaturaAtiva();
        assinatura.MarcarInadimplente(DateTime.UtcNow);

        var evento = new PagamentoTreinadorPagoEvent(
            Guid.NewGuid(), assinatura.TreinadorId, assinatura.Id,
            FinalidadePagamentoTreinador.TrocaPlano, planoNovo.Id, DateTime.UtcNow);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);

        await _handler.HandleAsync(evento);

        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Ativa, "regularização reativa assinatura");
        assinatura.PlanoPlataformaId.Should().Be(planoNovo.Id);
        assinatura.DataProximaCobranca.Should().BeAfter(DateTime.UtcNow.AddDays(20), "ciclo reinicia após regularização");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TrocaPlano_SemPlanoAlvoId_IgnoraSemCommit()
    {
        var assinatura = CriarAssinaturaAtiva();
        var evento = new PagamentoTreinadorPagoEvent(
            Guid.NewGuid(), assinatura.TreinadorId, assinatura.Id,
            FinalidadePagamentoTreinador.TrocaPlano, null, DateTime.UtcNow);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        await _handler.HandleAsync(evento);

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
