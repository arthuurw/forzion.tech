using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.Agenda;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores.Agenda;

public class CriarBloqueioAgendaHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IBloqueioAgendaRepository> _bloqueioRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly CriarBloqueioAgendaHandler _handler;

    public CriarBloqueioAgendaHandlerTests()
    {
        _handler = new CriarBloqueioAgendaHandler(_treinadorRepo.Object, _bloqueioRepo.Object, _unitOfWork.Object, _timeProvider);
    }

    private void SetupTreinador(Treinador? treinador, Guid treinadorId) =>
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

    [Fact]
    public async Task HandleAsync_TreinadorInexistente_RetornaTreinadorNaoEncontrado()
    {
        var treinadorId = Guid.NewGuid();
        SetupTreinador(null, treinadorId);

        var result = await _handler.HandleAsync(new CriarBloqueioAgendaCommand(
            treinadorId, TipoBloqueio.Pontual, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, null, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
        _bloqueioRepo.Verify(r => r.AdicionarAsync(It.IsAny<BloqueioAgenda>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TipoPontualValido_PersisteERetornaBloqueioCriado()
    {
        var treinador = new TreinadorBuilder().Build();
        SetupTreinador(treinador, treinador.Id);
        var inicio = new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc);
        var fim = new DateTime(2026, 8, 10, 10, 0, 0, DateTimeKind.Utc);

        var result = await _handler.HandleAsync(new CriarBloqueioAgendaCommand(
            treinador.Id, TipoBloqueio.Pontual, inicio, fim, null, null, null, "Recesso"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoBloqueio.Pontual);
        result.Value.InicioUtc.Should().Be(inicio);
        result.Value.FimUtc.Should().Be(fim);
        result.Value.Motivo.Should().Be("Recesso");
        _bloqueioRepo.Verify(r => r.AdicionarAsync(It.IsAny<BloqueioAgenda>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TipoRecorrenteValido_PersisteERetornaBloqueioCriado()
    {
        var treinador = new TreinadorBuilder().Build();
        SetupTreinador(treinador, treinador.Id);

        var result = await _handler.HandleAsync(new CriarBloqueioAgendaCommand(
            treinador.Id, TipoBloqueio.RecorrenteSemanal, null, null, 1, new TimeOnly(12, 0), new TimeOnly(13, 0), null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoBloqueio.RecorrenteSemanal);
        result.Value.DiaSemana.Should().Be(1);
        result.Value.HoraInicio.Should().Be(new TimeOnly(12, 0));
        result.Value.HoraFim.Should().Be(new TimeOnly(13, 0));
        _bloqueioRepo.Verify(r => r.AdicionarAsync(It.IsAny<BloqueioAgenda>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PontualComInicioMaiorQueFim_PropagaFalhaDoDominioSemPersistir()
    {
        var treinador = new TreinadorBuilder().Build();
        SetupTreinador(treinador, treinador.Id);
        var inicio = new DateTime(2026, 8, 10, 10, 0, 0, DateTimeKind.Utc);
        var fim = new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc);

        var result = await _handler.HandleAsync(new CriarBloqueioAgendaCommand(
            treinador.Id, TipoBloqueio.Pontual, inicio, fim, null, null, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(BloqueioAgendaErrors.IntervaloPontualInvalido);
        _bloqueioRepo.Verify(r => r.AdicionarAsync(It.IsAny<BloqueioAgenda>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RecorrenteComDiaSemanaForaDoIntervalo_PropagaFalhaDoDominioSemPersistir()
    {
        var treinador = new TreinadorBuilder().Build();
        SetupTreinador(treinador, treinador.Id);

        var result = await _handler.HandleAsync(new CriarBloqueioAgendaCommand(
            treinador.Id, TipoBloqueio.RecorrenteSemanal, null, null, 7, new TimeOnly(12, 0), new TimeOnly(13, 0), null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(BloqueioAgendaErrors.DiaSemanaInvalido);
        _bloqueioRepo.Verify(r => r.AdicionarAsync(It.IsAny<BloqueioAgenda>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

public class RemoverBloqueioAgendaHandlerTests
{
    private readonly Mock<IBloqueioAgendaRepository> _bloqueioRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly RemoverBloqueioAgendaHandler _handler;

    public RemoverBloqueioAgendaHandlerTests()
    {
        _handler = new RemoverBloqueioAgendaHandler(_bloqueioRepo.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task HandleAsync_BloqueioExistenteDoTreinador_RemoveERetornaSucesso()
    {
        var treinadorId = Guid.NewGuid();
        var bloqueio = BloqueioAgenda.CriarPontual(treinadorId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, DateTime.UtcNow).Value;
        _bloqueioRepo.Setup(r => r.ObterPorIdAsync(bloqueio.Id, treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(bloqueio);

        var result = await _handler.HandleAsync(treinadorId, bloqueio.Id);

        result.IsSuccess.Should().BeTrue();
        _bloqueioRepo.Verify(r => r.Remover(bloqueio), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_BloqueioDeOutroTreinador_RetornaNotFoundNuncaForbidden()
    {
        var treinadorId = Guid.NewGuid();
        var bloqueioId = Guid.NewGuid();
        _bloqueioRepo.Setup(r => r.ObterPorIdAsync(bloqueioId, treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync((BloqueioAgenda?)null);

        var result = await _handler.HandleAsync(treinadorId, bloqueioId);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(BloqueioAgendaErrors.NaoEncontrado);
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        _bloqueioRepo.Verify(r => r.Remover(It.IsAny<BloqueioAgenda>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_BloqueioInexistente_RetornaNotFound()
    {
        var treinadorId = Guid.NewGuid();
        var bloqueioId = Guid.NewGuid();
        _bloqueioRepo.Setup(r => r.ObterPorIdAsync(bloqueioId, treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync((BloqueioAgenda?)null);

        var result = await _handler.HandleAsync(treinadorId, bloqueioId);

        result.Error!.Should().Be(BloqueioAgendaErrors.NaoEncontrado);
    }
}

public class ListarBloqueiosAgendaHandlerTests
{
    private readonly Mock<IBloqueioAgendaRepository> _bloqueioRepo = new();
    private readonly ListarBloqueiosAgendaHandler _handler;

    public ListarBloqueiosAgendaHandlerTests()
    {
        _handler = new ListarBloqueiosAgendaHandler(_bloqueioRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_DevolveSoOsBloqueiosDoTreinadorMapeados()
    {
        var treinadorId = Guid.NewGuid();
        var bloqueio = BloqueioAgenda.CriarPontual(treinadorId, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "Feriado", DateTime.UtcNow).Value;
        _bloqueioRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BloqueioAgenda>)[bloqueio]);

        var result = await _handler.HandleAsync(treinadorId);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(bloqueio.Id);
        result[0].Motivo.Should().Be("Feriado");
        _bloqueioRepo.Verify(r => r.ListarPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
