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

public class BuscarLeadsPorContatoHandlerTests
{
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logAprovacaoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero));
    private readonly BuscarLeadsPorContatoHandler _handler;
    private static readonly Guid AdminId = Guid.NewGuid();

    public BuscarLeadsPorContatoHandlerTests()
    {
        _handler = new BuscarLeadsPorContatoHandler(_leadRepo.Object, _logAprovacaoRepo.Object, _unitOfWork.Object, _timeProvider);
    }

    private static Lead NovoLead(Guid treinadorId, TipoContatoLead tipo, string valor) =>
        Lead.Criar(
            treinadorId, "Fulano",
            ContatoLead.Criar(tipo, valor).Value,
            null,
            ConsentimentoLead.Criar("Contato comercial", DateTime.UtcNow, DateTime.UtcNow).Value,
            null, LeadSource.Agent, null, null, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_BuscaPorEmail_NormalizaAntesDeBuscar()
    {
        var lead = NovoLead(Guid.NewGuid(), TipoContatoLead.Email, "fulano@lead.com");
        _leadRepo.Setup(r => r.BuscarPorContatoCrossTenantAsync("fulano@lead.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead]);

        var result = await _handler.HandleAsync(new BuscarLeadsPorContatoQuery("  Fulano@Lead.com  ", AdminId));

        result.Should().ContainSingle(i => i.Id == lead.Id);
        _leadRepo.Verify(r => r.BuscarPorContatoCrossTenantAsync("fulano@lead.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_BuscaPorTelefoneEmFormatoDiferente_CasaComORegistroNormalizado()
    {
        var lead = NovoLead(Guid.NewGuid(), TipoContatoLead.Telefone, "5511999998888");
        _leadRepo.Setup(r => r.BuscarPorContatoCrossTenantAsync("5511999998888", It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead]);

        var result = await _handler.HandleAsync(new BuscarLeadsPorContatoQuery("(11) 99999-8888", AdminId));

        result.Should().ContainSingle(i => i.Id == lead.Id);
    }

    [Fact]
    public async Task HandleAsync_SemResultado_RetornaListaVazia()
    {
        _leadRepo.Setup(r => r.BuscarPorContatoCrossTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.HandleAsync(new BuscarLeadsPorContatoQuery("naoexiste@lead.com", AdminId));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ContatoMalFormado_RetornaListaVaziaSemErro()
    {
        var result = await _handler.HandleAsync(new BuscarLeadsPorContatoQuery("abc", AdminId));

        result.Should().BeEmpty();
        _leadRepo.Verify(r => r.BuscarPorContatoCrossTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- AUD-37: busca admin cross-tenant deixa rastro de auditoria ---

    [Fact]
    public async Task HandleAsync_RegistraAuditoriaAtribuidaAoAdminRealComContatoMascarado()
    {
        var lead = NovoLead(Guid.NewGuid(), TipoContatoLead.Email, "fulano@lead.com");
        _leadRepo.Setup(r => r.BuscarPorContatoCrossTenantAsync("fulano@lead.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync([lead]);
        LogAprovacao? logCapturado = null;
        _logAprovacaoRepo.Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
            .Callback<LogAprovacao, CancellationToken>((l, _) => logCapturado = l)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(new BuscarLeadsPorContatoQuery("fulano@lead.com", AdminId));

        logCapturado.Should().NotBeNull();
        logCapturado!.TipoAcao.Should().Be(TipoAcaoAprovacao.BuscaLeadPorContato);
        logCapturado.RealizadoPorId.Should().Be(AdminId, "o ator do log é o admin, nunca o titular buscado");
        logCapturado.EntidadeId.Should().Be(AdminId);
        logCapturado.Observacao.Should().NotBeNullOrEmpty();
        logCapturado.Observacao.Should().NotContain("fulano@lead.com", "o contato nunca pode ir cru na observação");
        logCapturado.Observacao.Should().Be("f***@lead.com");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ContatoMalFormado_AindaAssimRegistraAuditoria()
    {
        LogAprovacao? logCapturado = null;
        _logAprovacaoRepo.Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
            .Callback<LogAprovacao, CancellationToken>((l, _) => logCapturado = l)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(new BuscarLeadsPorContatoQuery("abc", AdminId));

        logCapturado.Should().NotBeNull("a tentativa de busca é auditável mesmo quando o contato não normaliza");
    }

    [Fact]
    public async Task HandleAsync_FalhaAoRegistrarAuditoria_NaoEEngolidaEmSilencio()
    {
        _logAprovacaoRepo.Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("falha ao persistir"));

        var act = async () => await _handler.HandleAsync(new BuscarLeadsPorContatoQuery("fulano@lead.com", AdminId));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
