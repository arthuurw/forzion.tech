using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.Agendamentos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores.Agendamentos;

public class RecusarSolicitacaoHandlerTests
{
    private readonly Mock<ISolicitacaoAgendamentoRepository> _solicitacaoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));
    private readonly RecusarSolicitacaoHandler _handler;

    private static readonly Guid TreinadorId = Guid.NewGuid();

    public RecusarSolicitacaoHandlerTests()
    {
        _handler = new RecusarSolicitacaoHandler(_solicitacaoRepo.Object, _unitOfWork.Object, _timeProvider);
    }

    private static SolicitacaoAgendamento CriarPendente(DateTime agora) =>
        SolicitacaoAgendamento.Criar(
            TreinadorId, Guid.NewGuid(), Guid.NewGuid(), "slot-hash", agora.AddHours(2), agora.AddHours(3),
            "idem-key", "hash", agora).Value;

    private void SetupSolicitacao(SolicitacaoAgendamento? solicitacao, Guid solicitacaoId) =>
        _solicitacaoRepo.Setup(r => r.ObterPorIdAsync(solicitacaoId, TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

    [Fact]
    public async Task HandleAsync_PendenteComMotivo_RecusaEComita()
    {
        var solicitacao = CriarPendente(_timeProvider.GetUtcNow().UtcDateTime);
        SetupSolicitacao(solicitacao, solicitacao.Id);

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id, "Horário conflita com outra sessão");

        result.IsSuccess.Should().BeTrue();
        solicitacao.Status.Should().Be(SolicitacaoAgendamentoStatus.Recusada);
        solicitacao.Motivo.Should().Be("Horário conflita com outra sessão");
        solicitacao.DecididaPorId.Should().Be(TreinadorId);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SemMotivo_RecusaComMotivoNulo()
    {
        var solicitacao = CriarPendente(_timeProvider.GetUtcNow().UtcDateTime);
        SetupSolicitacao(solicitacao, solicitacao.Id);

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id, motivo: null);

        result.IsSuccess.Should().BeTrue();
        solicitacao.Status.Should().Be(SolicitacaoAgendamentoStatus.Recusada);
        solicitacao.Motivo.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_MotivoMuitoLongo_RetornaFalhaSemMutarNemCommit()
    {
        var solicitacao = CriarPendente(_timeProvider.GetUtcNow().UtcDateTime);
        SetupSolicitacao(solicitacao, solicitacao.Id);

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id, new string('x', 501));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.MotivoMuitoLongo);
        solicitacao.Status.Should().Be(SolicitacaoAgendamentoStatus.PendenteAgente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TransicaoInvalida_RetornaFalhaSemMutar()
    {
        var solicitacao = CriarPendente(_timeProvider.GetUtcNow().UtcDateTime);
        solicitacao.Confirmar(TreinadorId, _timeProvider.GetUtcNow().UtcDateTime);
        SetupSolicitacao(solicitacao, solicitacao.Id);

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id, null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.TransicaoNaoSuportada);
        solicitacao.Status.Should().Be(SolicitacaoAgendamentoStatus.Confirmada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SolicitacaoDeOutroTreinadorOuInexistente_RetornaNaoEncontradaNuncaForbidden()
    {
        var solicitacaoId = Guid.NewGuid();
        SetupSolicitacao(null, solicitacaoId);

        var result = await _handler.HandleAsync(TreinadorId, solicitacaoId, null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.NaoEncontrada);
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}

public class CancelarSolicitacaoHandlerTests
{
    private readonly Mock<ISolicitacaoAgendamentoRepository> _solicitacaoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero));
    private readonly CancelarSolicitacaoHandler _handler;

    private static readonly Guid TreinadorId = Guid.NewGuid();

    public CancelarSolicitacaoHandlerTests()
    {
        _handler = new CancelarSolicitacaoHandler(_solicitacaoRepo.Object, _unitOfWork.Object, _timeProvider);
    }

    private static SolicitacaoAgendamento CriarConfirmada(DateTime agora)
    {
        var solicitacao = SolicitacaoAgendamento.Criar(
            TreinadorId, Guid.NewGuid(), Guid.NewGuid(), "slot-hash", agora.AddHours(2), agora.AddHours(3),
            "idem-key", "hash", agora).Value;
        solicitacao.Confirmar(TreinadorId, agora);
        return solicitacao;
    }

    private void SetupSolicitacao(SolicitacaoAgendamento? solicitacao, Guid solicitacaoId) =>
        _solicitacaoRepo.Setup(r => r.ObterPorIdAsync(solicitacaoId, TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(solicitacao);

    [Fact]
    public async Task HandleAsync_Confirmada_CancelaEDevolveCapacidade()
    {
        var solicitacao = CriarConfirmada(_timeProvider.GetUtcNow().UtcDateTime);
        SetupSolicitacao(solicitacao, solicitacao.Id);

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id, "Aluno desmarcou");

        result.IsSuccess.Should().BeTrue();
        solicitacao.Status.Should().Be(SolicitacaoAgendamentoStatus.Cancelada);
        solicitacao.Motivo.Should().Be("Aluno desmarcou");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AindaPendente_RetornaFalhaSemMutar()
    {
        var solicitacao = SolicitacaoAgendamento.Criar(
            TreinadorId, Guid.NewGuid(), Guid.NewGuid(), "slot-hash",
            _timeProvider.GetUtcNow().UtcDateTime.AddHours(2), _timeProvider.GetUtcNow().UtcDateTime.AddHours(3),
            "idem-key", "hash", _timeProvider.GetUtcNow().UtcDateTime).Value;
        SetupSolicitacao(solicitacao, solicitacao.Id);

        var result = await _handler.HandleAsync(TreinadorId, solicitacao.Id, null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.TransicaoNaoSuportada);
        solicitacao.Status.Should().Be(SolicitacaoAgendamentoStatus.PendenteAgente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SolicitacaoDeOutroTreinadorOuInexistente_RetornaNaoEncontradaNuncaForbidden()
    {
        var solicitacaoId = Guid.NewGuid();
        SetupSolicitacao(null, solicitacaoId);

        var result = await _handler.HandleAsync(TreinadorId, solicitacaoId, null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.NaoEncontrada);
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
