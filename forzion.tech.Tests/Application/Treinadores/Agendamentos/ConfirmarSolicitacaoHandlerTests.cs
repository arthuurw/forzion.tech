using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.Agendamentos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Tests.Builders;
using forzion.tech.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores.Agendamentos;

public class ConfirmarSolicitacaoHandlerTests
{
    private readonly Mock<ISolicitacaoAgendamentoRepository> _solicitacaoRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDbContextTransactionProvider> _transactionProvider = new();
    private readonly Mock<IDatabaseErrorInspector> _databaseErrorInspector = new();
    private readonly ConfirmarSolicitacaoHandler _handler;

    private static readonly Guid TreinadorId = Guid.NewGuid();

    // O handler usa TimeProvider.System (não um fake) — necessário p/ o retry usar Task.Delay
    // real sem travar o teste. Os horários de fixture, então, são relativos ao relógio real.
    private static readonly DateTime Agora = DateTime.UtcNow;

    public ConfirmarSolicitacaoHandlerTests()
    {
        _transactionProvider.SetupExecuteInTransaction<Result>();

        _handler = new ConfirmarSolicitacaoHandler(
            _solicitacaoRepo.Object, _pacoteRepo.Object, _unitOfWork.Object, _transactionProvider.Object,
            _databaseErrorInspector.Object, TimeProvider.System, Mock.Of<ILogger<ConfirmarSolicitacaoHandler>>());
    }

    private static SolicitacaoAgendamento CriarSolicitacaoPendente(Guid pacoteId, DateTime inicioUtc) =>
        SolicitacaoAgendamento.Criar(
            TreinadorId, pacoteId, Guid.NewGuid(), "slot-hash", inicioUtc, inicioUtc.AddMinutes(60),
            "idem-key", "hash", Agora.AddMinutes(-30)).Value;

    private static Pacote CriarPacote(Guid treinadorId, int capacidadeMaxima)
    {
        var pacote = new PacoteBuilder().ComTreinadorId(treinadorId).Build();
        pacote.AtualizarCatalogoPublico("Categoria", 60, false, Agora, capacidadeMaxima: capacidadeMaxima);
        return pacote;
    }

    [Fact]
    public async Task HandleAsync_SolicitacaoInexistente_RetornaNaoEncontradaSemCommit()
    {
        var solicitacaoId = Guid.NewGuid();
        _solicitacaoRepo.Setup(r => r.ObterPorIdAsync(solicitacaoId, TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SolicitacaoAgendamento?)null);

        var result = await _handler.HandleAsync(TreinadorId, solicitacaoId);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.NaoEncontrada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CapacidadeEsgotada_RetornaConflitoSemMutarStatus()
    {
        var pacote = CriarPacote(TreinadorId, capacidadeMaxima: 1);
        var solicitacao = CriarSolicitacaoPendente(pacote.Id, Agora.AddHours(2));
        _solicitacaoRepo.Setup(r => r.ObterPorIdAsync(solicitacao.Id, TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        _solicitacaoRepo.Setup(r => r.ContarConfirmadasSobrepostasAsync(
                TreinadorId, solicitacao.InicioUtc, solicitacao.FimUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.CapacidadeEsgotada);
        solicitacao.Status.Should().Be(SolicitacaoAgendamentoStatus.PendenteAgente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CapacidadeLivre_ConfirmaEComita()
    {
        var pacote = CriarPacote(TreinadorId, capacidadeMaxima: 2);
        var solicitacao = CriarSolicitacaoPendente(pacote.Id, Agora.AddHours(2));
        _solicitacaoRepo.Setup(r => r.ObterPorIdAsync(solicitacao.Id, TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        _solicitacaoRepo.Setup(r => r.ContarConfirmadasSobrepostasAsync(
                TreinadorId, solicitacao.InicioUtc, solicitacao.FimUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id);

        result.IsSuccess.Should().BeTrue();
        solicitacao.Status.Should().Be(SolicitacaoAgendamentoStatus.Confirmada);
        solicitacao.DecididaPorId.Should().Be(TreinadorId);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TransicaoInvalida_RetornaFalhaSemMutar()
    {
        var pacote = CriarPacote(TreinadorId, capacidadeMaxima: 5);
        var solicitacao = CriarSolicitacaoPendente(pacote.Id, Agora.AddHours(2));
        solicitacao.Confirmar(TreinadorId, Agora);
        _solicitacaoRepo.Setup(r => r.ObterPorIdAsync(solicitacao.Id, TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        _solicitacaoRepo.Setup(r => r.ContarConfirmadasSobrepostasAsync(
                TreinadorId, solicitacao.InicioUtc, solicitacao.FimUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.TransicaoNaoSuportada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SlotJaIniciado_RetornaFalhaSemMutar()
    {
        var pacote = CriarPacote(TreinadorId, capacidadeMaxima: 5);
        var solicitacao = CriarSolicitacaoPendente(pacote.Id, Agora.AddMinutes(-10));
        _solicitacaoRepo.Setup(r => r.ObterPorIdAsync(solicitacao.Id, TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        _solicitacaoRepo.Setup(r => r.ContarConfirmadasSobrepostasAsync(
                TreinadorId, solicitacao.InicioUtc, solicitacao.FimUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.SlotJaIniciado);
        solicitacao.Status.Should().Be(SolicitacaoAgendamentoStatus.PendenteAgente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ConflitoDeSerializacao_RetentaEConclui()
    {
        // Conflito simulado na recontagem (não no commit): o mock reusa a MESMA instância de
        // solicitação entre tentativas, então mutar via Confirmar() antes do conflito faria a
        // 2ª tentativa ver status já Confirmada — divergindo do real (tx abortada não persiste,
        // releitura real veria PendenteAgente de novo).
        var pacote = CriarPacote(TreinadorId, capacidadeMaxima: 2);
        var solicitacao = CriarSolicitacaoPendente(pacote.Id, Agora.AddHours(2));
        _solicitacaoRepo.Setup(r => r.ObterPorIdAsync(solicitacao.Id, TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var conflito = new InvalidOperationException("serialization_failure");
        _databaseErrorInspector.Setup(i => i.EhConflitoDeSerializacao(conflito)).Returns(true);

        var tentativasContagem = 0;
        _solicitacaoRepo.Setup(r => r.ContarConfirmadasSobrepostasAsync(
                TreinadorId, solicitacao.InicioUtc, solicitacao.FimUtc, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                tentativasContagem++;
                return tentativasContagem == 1 ? throw conflito : Task.FromResult(0);
            });

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id);

        result.IsSuccess.Should().BeTrue();
        tentativasContagem.Should().Be(2, "primeira tentativa aborta com 40001, segunda conclui");
        solicitacao.Status.Should().Be(SolicitacaoAgendamentoStatus.Confirmada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ConflitoDeConcorrenciaOtimistaNoCommit_RetentaERetornaTransicaoJaDecidida()
    {
        // Simula Recusar/Cancelar commitando entre o SELECT e o UPDATE desta tx (xmin de
        // solicitacoes_agendamento). O mock reusa a MESMA instância entre tentativas: a 1ª chamada
        // a Confirmar() já mutou Status para Confirmada antes do commit abortar; a 2ª tentativa
        // relê essa instância (releitura real veria Recusada/Cancelada) e Confirmar() rejeita por
        // já não estar mais PendenteAgente — mesmo efeito observável do caminho real.
        var pacote = CriarPacote(TreinadorId, capacidadeMaxima: 2);
        var solicitacao = CriarSolicitacaoPendente(pacote.Id, Agora.AddHours(2));
        _solicitacaoRepo.Setup(r => r.ObterPorIdAsync(solicitacao.Id, TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        _solicitacaoRepo.Setup(r => r.ContarConfirmadasSobrepostasAsync(
                TreinadorId, solicitacao.InicioUtc, solicitacao.FimUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var conflito = new DbUpdateConcurrencyException();
        _databaseErrorInspector.Setup(i => i.EhConflitoDeConcorrenciaOtimista(conflito)).Returns(true);

        var tentativasCommit = 0;
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                tentativasCommit++;
                return tentativasCommit == 1 ? throw conflito : Task.CompletedTask;
            });

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.TransicaoNaoSuportada);
        tentativasCommit.Should().Be(1, "a 2ª tentativa falha antes do commit, ao reavaliar o status já mutado");
        _unitOfWork.Verify(u => u.DescartarAlteracoesPendentes(), Times.Once);
    }
}
