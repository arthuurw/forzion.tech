using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Leads.ResolverConviteLead;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Leads;

public class ResolverConviteLeadHandlerTests
{
    private readonly Mock<ILeadConviteRepository> _conviteRepo = new();
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly ResolverConviteLeadHandler _handler;

    private static readonly Guid TreinadorId = Guid.NewGuid();

    public ResolverConviteLeadHandlerTests()
    {
        _handler = new ResolverConviteLeadHandler(_conviteRepo.Object, _leadRepo.Object, _treinadorRepo.Object, _timeProvider);
    }

    private DateTime Agora => _timeProvider.GetUtcNow().UtcDateTime;

    private Lead NovoLead() =>
        Lead.Criar(
            TreinadorId, "Fulano",
            ContatoLead.Criar(TipoContatoLead.Email, "fulano@lead.com").Value,
            "quero treinar",
            ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value,
            null, LeadSource.Agent, null, null, Agora).Value;

    [Fact]
    public async Task HandleAsync_TokenValido_RetornaDadosDoLeadETreinador()
    {
        var lead = NovoLead();
        var convite = LeadConvite.Criar(lead.Id, TreinadorId, "hash", Agora.AddDays(14), Agora).Value;
        _conviteRepo.Setup(r => r.ObterPorTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(convite);
        _leadRepo.Setup(r => r.ObterComHistoricoAsync(TreinadorId, lead.Id, It.IsAny<CancellationToken>())).ReturnsAsync(lead);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Treinador.Criar(Guid.NewGuid(), "Coach", Agora).Value);

        var result = await _handler.HandleAsync(new ResolverConviteLeadQuery("token-cru"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Nome.Should().Be("Fulano");
        result.Value.TreinadorId.Should().Be(TreinadorId);
    }

    [Fact]
    public async Task HandleAsync_TokenInexistente_RetornaTokenInvalido()
    {
        _conviteRepo.Setup(r => r.ObterPorTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((LeadConvite?)null);

        var result = await _handler.HandleAsync(new ResolverConviteLeadQuery("token-cru"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(LeadConviteErrors.TokenInvalido);
    }

    [Fact]
    public async Task HandleAsync_TokenExpirado_RetornaMesmoErroDoInexistente()
    {
        var lead = NovoLead();
        var expirado = LeadConvite.Criar(lead.Id, TreinadorId, "hash", Agora.AddSeconds(1), Agora).Value;
        _conviteRepo.Setup(r => r.ObterPorTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(expirado);
        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        var result = await _handler.HandleAsync(new ResolverConviteLeadQuery("token-cru"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(LeadConviteErrors.TokenInvalido);
    }

    [Fact]
    public async Task HandleAsync_TokenConsumido_RetornaMesmoErroDoInexistente()
    {
        var lead = NovoLead();
        var convite = LeadConvite.Criar(lead.Id, TreinadorId, "hash", Agora.AddDays(14), Agora).Value;
        convite.Consumir(Agora.AddHours(1));
        _conviteRepo.Setup(r => r.ObterPorTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(convite);

        var result = await _handler.HandleAsync(new ResolverConviteLeadQuery("token-cru"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(LeadConviteErrors.TokenInvalido);
    }

    [Fact]
    public async Task HandleAsync_TokenInvalidado_RetornaMesmoErroDoInexistente()
    {
        var lead = NovoLead();
        var convite = LeadConvite.Criar(lead.Id, TreinadorId, "hash", Agora.AddDays(14), Agora).Value;
        convite.Invalidar(Agora.AddHours(1));
        _conviteRepo.Setup(r => r.ObterPorTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(convite);

        var result = await _handler.HandleAsync(new ResolverConviteLeadQuery("token-cru"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(LeadConviteErrors.TokenInvalido);
    }
}
