using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.Leads;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Admin.Leads;

public class AnonimizarLeadHandlerTests
{
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logAprovacaoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly AnonimizarLeadHandler _handler;

    private static readonly Guid AdminId = Guid.NewGuid();

    public AnonimizarLeadHandlerTests()
    {
        _handler = new AnonimizarLeadHandler(_leadRepo.Object, _logAprovacaoRepo.Object, _unitOfWork.Object, _timeProvider);
    }

    private static Lead NovoLead() =>
        Lead.Criar(
            Guid.NewGuid(), "Fulano",
            ContatoLead.Criar(TipoContatoLead.Email, "fulano@lead.com").Value,
            null,
            ConsentimentoLead.Criar("Contato comercial", DateTime.UtcNow, DateTime.UtcNow).Value,
            null, LeadSource.Agent, null, null, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_LeadEncontrado_AnonimizaEGravaLogComAdminComoAtorELeadComoAlvo()
    {
        var lead = NovoLead();
        _leadRepo.Setup(r => r.ObterPorIdCrossTenantAsync(lead.Id, It.IsAny<CancellationToken>())).ReturnsAsync(lead);
        LogAprovacao? logCapturado = null;
        _logAprovacaoRepo.Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
            .Callback<LogAprovacao, CancellationToken>((l, _) => logCapturado = l)
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new AnonimizarLeadCommand(lead.Id, AdminId));

        result.IsSuccess.Should().BeTrue();
        lead.Anonimizado.Should().BeTrue();
        logCapturado.Should().NotBeNull();
        logCapturado!.TipoAcao.Should().Be(TipoAcaoAprovacao.AnonimizacaoLead);
        logCapturado.RealizadoPorId.Should().Be(AdminId);
        logCapturado.EntidadeId.Should().Be(lead.Id);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_LeadNaoEncontrado_RetornaNotFound()
    {
        _leadRepo.Setup(r => r.ObterPorIdCrossTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Lead?)null);

        var result = await _handler.HandleAsync(new AnonimizarLeadCommand(Guid.NewGuid(), AdminId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(forzion.tech.Domain.Shared.ErrorType.NotFound);
        _logAprovacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_LeadJaAnonimizado_EhIdempotenteSemSegundaEscrita()
    {
        var lead = NovoLead();
        lead.Anonimizar(DateTime.UtcNow);
        _leadRepo.Setup(r => r.ObterPorIdCrossTenantAsync(lead.Id, It.IsAny<CancellationToken>())).ReturnsAsync(lead);

        var result = await _handler.HandleAsync(new AnonimizarLeadCommand(lead.Id, AdminId));

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
