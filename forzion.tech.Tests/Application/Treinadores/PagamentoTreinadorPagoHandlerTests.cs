using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class PagamentoTreinadorPagoHandlerTests
{
    private static readonly DateTimeOffset Instante = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Agora = Instante.UtcDateTime;

    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(Instante);
    private readonly PagamentoTreinadorPagoHandler _handler;

    public PagamentoTreinadorPagoHandlerTests()
    {
        _handler = new PagamentoTreinadorPagoHandler(
            _assinaturaRepo.Object,
            _planoRepo.Object,
            _unitOfWork.Object,
            _time,
            Mock.Of<ILogger<PagamentoTreinadorPagoHandler>>());
    }

    private static AssinaturaTreinador CriarAssinaturaAtiva()
    {
        var a = AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, Agora).Value;
        a.Ativar(Agora);
        return a;
    }

    private static PagamentoTreinadorPagoEvent CriarEvento(Guid assinaturaId, FinalidadePagamentoTreinador finalidade) =>
        new(Guid.NewGuid(), Guid.NewGuid(), assinaturaId, finalidade, null, Agora);

    [Fact]
    public async Task HandleAsync_Renovacao_AssinaturaAtiva_RegularizaEAgendaProximaCobranca()
    {
        var assinatura = CriarAssinaturaAtiva();
        var evento = CriarEvento(assinatura.Id, FinalidadePagamentoTreinador.Renovacao);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        await _handler.HandleAsync(evento);

        assinatura.TentativasFalhasConsecutivas.Should().Be(0);
        assinatura.DataProximaCobranca.Should().Be(Agora.AddMonths(1), "próxima cobrança é exatamente 1 mês após o instante de processamento");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Renovacao_AssinaturaInadimplente_ReativaEAgenda()
    {
        var assinatura = CriarAssinaturaAtiva();
        assinatura.MarcarInadimplente(Agora);
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
        var planoNovo = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 100, 100m, Agora).Value;
        var assinatura = CriarAssinaturaAtiva();
        var dataProximaCobrancaOriginal = Agora.AddDays(15);
        assinatura.AgendarProximaCobranca(dataProximaCobrancaOriginal, Agora);

        var evento = new PagamentoTreinadorPagoEvent(
            Guid.NewGuid(), assinatura.TreinadorId, assinatura.Id,
            FinalidadePagamentoTreinador.TrocaPlano, planoNovo.Id, Agora);

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
            FinalidadePagamentoTreinador.TrocaPlano, Guid.NewGuid(), Agora);

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
        var planoNovo = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 100, 80m, Agora).Value;
        var assinatura = CriarAssinaturaAtiva();
        assinatura.MarcarInadimplente(Agora);

        var evento = new PagamentoTreinadorPagoEvent(
            Guid.NewGuid(), assinatura.TreinadorId, assinatura.Id,
            FinalidadePagamentoTreinador.TrocaPlano, planoNovo.Id, Agora);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _planoRepo.Setup(r => r.ObterPorIdAsync(planoNovo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planoNovo);

        await _handler.HandleAsync(evento);

        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Ativa, "regularização reativa assinatura");
        assinatura.PlanoPlataformaId.Should().Be(planoNovo.Id);
        assinatura.DataProximaCobranca.Should().Be(Agora.AddMonths(1), "ciclo reinicia exatamente 1 mês após a regularização");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TrocaPlano_SemPlanoAlvoId_IgnoraSemCommit()
    {
        var assinatura = CriarAssinaturaAtiva();
        var evento = new PagamentoTreinadorPagoEvent(
            Guid.NewGuid(), assinatura.TreinadorId, assinatura.Id,
            FinalidadePagamentoTreinador.TrocaPlano, null, Agora);

        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        await _handler.HandleAsync(evento);

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Contratacao_Skip_AssinaturaRepoCommitNuncaChamados()
    {
        var evento = CriarEvento(Guid.NewGuid(), FinalidadePagamentoTreinador.Contratacao);

        await _handler.HandleAsync(evento);

        _assinaturaRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
