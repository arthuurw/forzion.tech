using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.Agenda;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores.Agenda;

public class ObterPoliticaAgendaHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly ObterPoliticaAgendaHandler _handler;

    public ObterPoliticaAgendaHandlerTests()
    {
        _handler = new ObterPoliticaAgendaHandler(_treinadorRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinadorInexistente_RetornaTreinadorNaoEncontrado()
    {
        var treinadorId = Guid.NewGuid();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var result = await _handler.HandleAsync(treinadorId);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNuncaConfigurouPolitica_DevolveDefaultsDaMigration()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(treinador.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.AntecedenciaMinimaHoras.Should().Be(2);
        result.Value.HorizonteDias.Should().Be(60);
    }
}

public class AtualizarPoliticaAgendaHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly AtualizarPoliticaAgendaHandler _handler;

    public AtualizarPoliticaAgendaHandlerTests()
    {
        _handler = new AtualizarPoliticaAgendaHandler(_treinadorRepo.Object, _unitOfWork.Object, _timeProvider);
    }

    private void SetupTreinador(Treinador? treinador, Guid treinadorId) =>
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

    [Fact]
    public async Task HandleAsync_TreinadorInexistente_RetornaTreinadorNaoEncontrado()
    {
        var treinadorId = Guid.NewGuid();
        SetupTreinador(null, treinadorId);

        var result = await _handler.HandleAsync(new AtualizarPoliticaAgendaCommand(treinadorId, 4, 30));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ValoresValidos_PersisteERetornaPoliticaAtualizada()
    {
        var treinador = new TreinadorBuilder().Build();
        SetupTreinador(treinador, treinador.Id);

        var result = await _handler.HandleAsync(new AtualizarPoliticaAgendaCommand(treinador.Id, 4, 30));

        result.IsSuccess.Should().BeTrue();
        result.Value.AntecedenciaMinimaHoras.Should().Be(4);
        result.Value.HorizonteDias.Should().Be(30);
        treinador.PoliticaAgenda.AntecedenciaMinimaHoras.Should().Be(4);
        treinador.PoliticaAgenda.HorizonteDias.Should().Be(30);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AntecedenciaNegativa_PropagaRejeicaoDoVoSemPersistir()
    {
        var treinador = new TreinadorBuilder().Build();
        SetupTreinador(treinador, treinador.Id);

        var result = await _handler.HandleAsync(new AtualizarPoliticaAgendaCommand(treinador.Id, -1, 30));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(PoliticaAgendaErrors.AntecedenciaMinimaInvalida);
        treinador.PoliticaAgenda.AntecedenciaMinimaHoras.Should().Be(2);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task HandleAsync_HorizonteInvalido_PropagaRejeicaoDoVoSemPersistir(int horizonteDias)
    {
        var treinador = new TreinadorBuilder().Build();
        SetupTreinador(treinador, treinador.Id);

        var result = await _handler.HandleAsync(new AtualizarPoliticaAgendaCommand(treinador.Id, 2, horizonteDias));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(PoliticaAgendaErrors.HorizonteInvalido);
        treinador.PoliticaAgenda.HorizonteDias.Should().Be(60);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
