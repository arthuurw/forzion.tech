using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Leads.CriarLeadManual;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Leads;

public class CriarLeadManualHandlerTests
{
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly CriarLeadManualHandler _handler;

    public CriarLeadManualHandlerTests()
    {
        _handler = new CriarLeadManualHandler(_leadRepo.Object, _unitOfWork.Object, _timeProvider);
    }

    private static CriarLeadManualCommand ComandoValido(Guid treinadorId) =>
        new(treinadorId, "Fulano", TipoContatoLead.Email, "fulano@lead.com", "quero treinar", "Contato comercial");

    [Fact]
    public async Task HandleAsync_ComandoValido_CriaLeadManualComStatusNovoESemIdempotencyKey()
    {
        var treinadorId = Guid.NewGuid();
        Lead? capturado = null;
        _leadRepo.Setup(r => r.AdicionarAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, CancellationToken>((l, _) => capturado = l)
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(ComandoValido(treinadorId));

        result.IsSuccess.Should().BeTrue();
        capturado.Should().NotBeNull();
        capturado!.Source.Should().Be(LeadSource.Manual);
        capturado.Status.Should().Be(LeadStatus.Novo);
        capturado.IdempotencyKey.Should().BeNull();
        capturado.TreinadorId.Should().Be(treinadorId);
        capturado.DomainEvents.Should().ContainSingle(e => e is forzion.tech.Domain.Events.LeadCriadoEvent);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ContatoInvalido_RecusaSemPersistir()
    {
        var treinadorId = Guid.NewGuid();
        var command = ComandoValido(treinadorId) with { ContatoValor = "nao-e-email" };

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        _leadRepo.Verify(r => r.AdicionarAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_FinalidadeDeConsentimentoAusente_Recusa()
    {
        var treinadorId = Guid.NewGuid();
        var command = ComandoValido(treinadorId) with { ConsentimentoFinalidade = " " };

        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        _leadRepo.Verify(r => r.AdicionarAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
