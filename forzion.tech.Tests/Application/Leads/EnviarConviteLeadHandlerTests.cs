using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Leads.EnviarConvite;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Leads;

public class EnviarConviteLeadHandlerTests
{
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly Mock<ILeadConviteRepository> _conviteRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ILeadConviteSender> _sender = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly EnviarConviteLeadHandler _handler;

    private static readonly Guid TreinadorId = Guid.NewGuid();

    public EnviarConviteLeadHandlerTests()
    {
        _handler = new EnviarConviteLeadHandler(
            _leadRepo.Object, _conviteRepo.Object, _treinadorRepo.Object, _sender.Object, _unitOfWork.Object, _timeProvider);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Treinador.Criar(Guid.NewGuid(), "Coach", DateTime.UtcNow).Value);
        _sender.Setup(s => s.EnviarAsync(
                It.IsAny<TipoContatoLead>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private static Lead NovoLead(TipoContatoLead tipo = TipoContatoLead.Email, string valor = "fulano@lead.com") =>
        Lead.Criar(
            TreinadorId,
            "Fulano",
            ContatoLead.Criar(tipo, valor).Value,
            "quero treinar",
            ConsentimentoLead.Criar("Contato comercial", DateTime.UtcNow, DateTime.UtcNow).Value,
            null,
            LeadSource.Agent,
            null,
            null,
            DateTime.UtcNow).Value;

    private void SetupLead(Lead lead) =>
        _leadRepo.Setup(r => r.ObterComHistoricoAsync(TreinadorId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

    [Fact]
    public async Task HandleAsync_ContatoTelefone_RecusaExplicandoQuePrecisaDeEmail()
    {
        var lead = NovoLead(TipoContatoLead.Telefone, "+5511999998888");
        SetupLead(lead);

        var result = await _handler.HandleAsync(new EnviarConviteLeadCommand(TreinadorId, lead.Id));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(forzion.tech.Domain.Shared.Errors.LeadConviteErrors.ContatoNaoSuportaConvite);
        _conviteRepo.Verify(r => r.AdicionarAsync(It.IsAny<LeadConvite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ContatoEmail_GeraTokenPersisteApenasOHashComValidade14Dias()
    {
        var lead = NovoLead();
        SetupLead(lead);
        LeadConvite? capturado = null;
        _conviteRepo.Setup(r => r.AdicionarAsync(It.IsAny<LeadConvite>(), It.IsAny<CancellationToken>()))
            .Callback<LeadConvite, CancellationToken>((c, _) => capturado = c)
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new EnviarConviteLeadCommand(TreinadorId, lead.Id));

        result.IsSuccess.Should().BeTrue();
        capturado.Should().NotBeNull();
        capturado!.TokenHash.Should().HaveLength(64);
        capturado.ExpiraEm.Should().Be(_timeProvider.GetUtcNow().UtcDateTime.AddDays(14));
    }

    [Fact]
    public async Task HandleAsync_ConviteAtivoAnterior_EInvalidadoNaMesmaOperacao()
    {
        var lead = NovoLead();
        SetupLead(lead);
        var anterior = LeadConvite.Criar(lead.Id, TreinadorId, "hash-anterior", _timeProvider.GetUtcNow().UtcDateTime.AddDays(10), _timeProvider.GetUtcNow().UtcDateTime).Value;
        _conviteRepo.Setup(r => r.ObterAtivoPorLeadAsync(TreinadorId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(anterior);

        var result = await _handler.HandleAsync(new EnviarConviteLeadCommand(TreinadorId, lead.Id));

        result.IsSuccess.Should().BeTrue();
        anterior.InvalidadoEm.Should().NotBeNull();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_LeadTerminal_Recusa()
    {
        var lead = NovoLead();
        lead.Descartar(MotivoDescarteLead.SemInteresse, Guid.NewGuid(), null, DateTime.UtcNow);
        SetupLead(lead);

        var result = await _handler.HandleAsync(new EnviarConviteLeadCommand(TreinadorId, lead.Id));

        result.IsFailure.Should().BeTrue();
        _conviteRepo.Verify(r => r.AdicionarAsync(It.IsAny<LeadConvite>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_LeadAnonimizado_Recusa()
    {
        var lead = NovoLead();
        lead.Anonimizar(DateTime.UtcNow);
        SetupLead(lead);

        var result = await _handler.HandleAsync(new EnviarConviteLeadCommand(TreinadorId, lead.Id));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_LeadForaDoCutoffDeExpurgo_EmissaoTiraDoAlcanceDoGcDe180Dias()
    {
        var agoraEmissao = _timeProvider.GetUtcNow().UtcDateTime;
        var lead = Lead.Criar(
            TreinadorId, "Fulano",
            ContatoLead.Criar(TipoContatoLead.Email, "fulano@lead.com").Value,
            "quero treinar",
            ConsentimentoLead.Criar("Contato comercial", agoraEmissao.AddDays(-200), agoraEmissao.AddDays(-200)).Value,
            null, LeadSource.Agent, null, null, agoraEmissao.AddDays(-200)).Value;
        SetupLead(lead);
        var cutoff180Dias = agoraEmissao.AddDays(-180);
        (lead.UltimoToqueEm < cutoff180Dias).Should().BeTrue("lead sem toque há 200d estaria no alcance do GC antes da emissão");

        var result = await _handler.HandleAsync(new EnviarConviteLeadCommand(TreinadorId, lead.Id));

        result.IsSuccess.Should().BeTrue();
        (lead.UltimoToqueEm < cutoff180Dias).Should().BeFalse("a emissao do convite deve tirar o lead do alcance do GC de 180 dias");
    }

    [Fact]
    public async Task HandleAsync_LeadDeOutroTreinador_RetornaNotFound()
    {
        var lead = NovoLead();
        _leadRepo.Setup(r => r.ObterComHistoricoAsync(TreinadorId, lead.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        var result = await _handler.HandleAsync(new EnviarConviteLeadCommand(TreinadorId, lead.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(forzion.tech.Domain.Shared.ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_TokenCru_NuncaChegaAoRepositorioDeConvite_SoOHash()
    {
        var lead = NovoLead();
        SetupLead(lead);
        string? tokenCapturadoNoSender = null;
        _sender.Setup(s => s.EnviarAsync(
                It.IsAny<TipoContatoLead>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TipoContatoLead, string, string, string, string, CancellationToken>((_, _, _, _, token, _) => tokenCapturadoNoSender = token)
            .ReturnsAsync(true);
        LeadConvite? capturado = null;
        _conviteRepo.Setup(r => r.AdicionarAsync(It.IsAny<LeadConvite>(), It.IsAny<CancellationToken>()))
            .Callback<LeadConvite, CancellationToken>((c, _) => capturado = c)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(new EnviarConviteLeadCommand(TreinadorId, lead.Id));

        tokenCapturadoNoSender.Should().NotBeNullOrEmpty();
        capturado!.TokenHash.Should().NotBe(tokenCapturadoNoSender);
    }
}
