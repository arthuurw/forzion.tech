using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Agents.Leads;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Agents.Leads;

public class RegistrarLeadAgenteHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDatabaseErrorInspector> _databaseErrorInspector = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly RegistrarLeadAgenteHandler _handler;

    public RegistrarLeadAgenteHandlerTests()
    {
        _handler = new RegistrarLeadAgenteHandler(_treinadorRepo.Object, _leadRepo.Object, _unitOfWork.Object, _timeProvider, _databaseErrorInspector.Object);
    }

    private static Treinador CriarTreinadorPublicado()
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio Teste", null, null, DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        return treinador;
    }

    private static RegistrarLeadAgenteCommand ComandoValido(
        Guid tenantId,
        string name = "Fulano",
        string contactType = "email",
        string contactValue = "fulano@lead.com",
        string? interest = "quero treinar",
        bool consentGranted = true,
        string consentPurpose = "Contato comercial",
        DateTime? consentGrantedAt = null,
        string? idempotencyKey = "chave-1",
        string? originUserAgent = null,
        string? originAssistant = null) =>
        new(tenantId, name, contactType, contactValue, interest, consentGranted, consentPurpose, consentGrantedAt, idempotencyKey, originUserAgent, originAssistant);

    private void SetupTreinador(Treinador? treinador, Guid tenantId) =>
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

    // --- AGF2-01: caminho feliz ---

    [Fact]
    public async Task HandleAsync_DadosValidos_PersisteLeadDeAgenteEProjetaStagedLead()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        Lead? capturado = null;
        _leadRepo.Setup(r => r.AdicionarAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, CancellationToken>((l, _) => capturado = l)
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Source.Should().Be("agent");
        result.Value.Status.Should().Be("pending");
        capturado.Should().NotBeNull();
        capturado!.Source.Should().Be(LeadSource.Agent);
        capturado.Status.Should().Be(LeadStatus.Novo);
        capturado.TreinadorId.Should().Be(treinador.Id);
        result.Value.LeadId.Should().Be(capturado.Id.ToString());
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Resposta_ContemApenasLeadIdSourceStatus()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id, name: "Nome Sigiloso", contactValue: "sigiloso@lead.com", interest: "informação confidencial"));

        var json = JsonSerializer.Serialize(result.Value);
        typeof(StagedLead).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(["LeadId", "Source", "Status"]);
        json.Should().NotContain("Nome Sigiloso").And.NotContain("sigiloso@lead.com").And.NotContain("confidencial");
    }

    // --- AGF2-03: tenant inexistente / inativo / não publicado ---

    [Fact]
    public async Task HandleAsync_TenantInexistente_RetornaTreinadorNaoEncontrado()
    {
        var tenantId = Guid.NewGuid();
        SetupTreinador(null, tenantId);

        var result = await _handler.HandleAsync(ComandoValido(tenantId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_TenantInativo_RetornaMesmoErroDoInexistente()
    {
        var treinador = CriarTreinadorPublicado();
        treinador.Inativar(DateTime.UtcNow);
        SetupTreinador(treinador, treinador.Id);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id));

        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_TenantNaoPublicado_RetornaMesmoErroDoInexistente()
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        SetupTreinador(treinador, treinador.Id);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id));

        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    // --- AGF2-04: consentimento não concedido ---

    [Fact]
    public async Task HandleAsync_ConsentGrantedFalso_RetornaValidacaoENadaPersiste()
    {
        var tenantId = Guid.NewGuid();

        var result = await _handler.HandleAsync(ComandoValido(tenantId, consentGranted: false));

        result.IsFailure.Should().BeTrue();
        _leadRepo.Verify(r => r.AdicionarAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _treinadorRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- AGF2-05: limites de tamanho / campos obrigatórios ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_NomeAusente_RetornaValidacao(string nome)
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), name: nome));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_NomeAcimaDe200_RetornaValidacao()
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), name: new string('a', 201)));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_InteresseAcimaDe1000_RetornaValidacao()
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), interest: new string('a', 1001)));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_PurposeAcimaDe500_RetornaValidacao()
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), consentPurpose: new string('a', 501)));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_IdempotencyKeyAcimaDe200_RetornaValidacao()
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), idempotencyKey: new string('a', 201)));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_UserAgentAcimaDe500_RetornaValidacao()
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), originUserAgent: new string('a', 501)));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_AssistenteAcimaDe100_RetornaValidacao()
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), originAssistant: new string('a', 101)));
        result.IsFailure.Should().BeTrue();
    }

    // --- AGF2-06 / AGF2-07: contato ---

    [Fact]
    public async Task HandleAsync_ContatoEmailInvalido_RetornaValidacao()
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), contactType: "email", contactValue: "nao-e-email"));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_ContatoTelefoneInvalido_RetornaValidacao()
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), contactType: "phone", contactValue: "abc"));
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_ContatoEmailComMaiusculasEEspacos_PersisteNormalizado()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        Lead? capturado = null;
        _leadRepo.Setup(r => r.AdicionarAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, CancellationToken>((l, _) => capturado = l)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(ComandoValido(treinador.Id, contactType: "email", contactValue: "  FULANO@LEAD.COM  "));

        capturado!.Contato.Valor.Should().Be("fulano@lead.com");
    }

    [Fact]
    public async Task HandleAsync_ContatoTelefoneLocalBrasileiro_PersisteNormalizadoParaE164()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        Lead? capturado = null;
        _leadRepo.Setup(r => r.AdicionarAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .Callback<Lead, CancellationToken>((l, _) => capturado = l)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(ComandoValido(treinador.Id, contactType: "phone", contactValue: "(11) 99999-9999"));

        capturado!.Contato.Valor.Should().Be("5511999999999");
    }

    // --- AGF2-08 / AGF2-09: idempotência ---

    [Fact]
    public async Task HandleAsync_MesmaChaveMesmosArgumentos_RetornaLeadExistenteSemPersistirNovo()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        var comando = ComandoValido(treinador.Id, idempotencyKey: "chave-repetida");
        var hash = IdempotenciaLead.Calcular(comando.Name, TipoContatoLead.Email, comando.ContactValue, comando.Interest, comando.ConsentPurpose);
        var existente = Lead.Criar(
            treinador.Id, comando.Name, ContatoLead.Criar(TipoContatoLead.Email, comando.ContactValue).Value,
            comando.Interest, ConsentimentoLead.Criar(comando.ConsentPurpose, DateTime.UtcNow, DateTime.UtcNow).Value,
            null, LeadSource.Agent, comando.IdempotencyKey, hash, DateTime.UtcNow).Value;
        _leadRepo.Setup(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-repetida", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existente);

        var result = await _handler.HandleAsync(comando);

        result.IsSuccess.Should().BeTrue();
        result.Value.LeadId.Should().Be(existente.Id.ToString());
        _leadRepo.Verify(r => r.AdicionarAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MesmaChaveArgumentosDiferentes_RetornaConflito()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        var existente = Lead.Criar(
            treinador.Id, "Outro Nome", ContatoLead.Criar(TipoContatoLead.Email, "outro@lead.com").Value,
            null, ConsentimentoLead.Criar("Outra finalidade", DateTime.UtcNow, DateTime.UtcNow).Value,
            null, LeadSource.Agent, "chave-repetida", "hash-diferente", DateTime.UtcNow).Value;
        _leadRepo.Setup(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-repetida", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existente);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id, idempotencyKey: "chave-repetida"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(LeadAgenteErrors.IdempotencyConflito);
        _leadRepo.Verify(r => r.AdicionarAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChaveConsultadaComTreinadorIdDoTenant_NaoOutroTenant()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);

        await _handler.HandleAsync(ComandoValido(treinador.Id, idempotencyKey: "chave-1"));

        _leadRepo.Verify(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- corrida de idempotência resolvida pelo índice único (23505) ---

    [Fact]
    public async Task HandleAsync_CommitViolaUnicidadeEVencedorTemMesmoHash_RetornaLeadDoVencedorSemPropagarExcecao()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        var comando = ComandoValido(treinador.Id, idempotencyKey: "chave-corrida");
        var hash = IdempotenciaLead.Calcular(comando.Name, TipoContatoLead.Email, comando.ContactValue, comando.Interest, comando.ConsentPurpose);
        var vencedor = Lead.Criar(
            treinador.Id, comando.Name, ContatoLead.Criar(TipoContatoLead.Email, comando.ContactValue).Value,
            comando.Interest, ConsentimentoLead.Criar(comando.ConsentPurpose, DateTime.UtcNow, DateTime.UtcNow).Value,
            null, LeadSource.Agent, comando.IdempotencyKey, hash, DateTime.UtcNow).Value;

        _leadRepo.SetupSequence(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-corrida", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null)
            .ReturnsAsync(vencedor);
        var excecaoDeUnicidade = new InvalidOperationException("23505");
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(excecaoDeUnicidade);
        _databaseErrorInspector.Setup(d => d.EhViolacaoDeUnicidade(excecaoDeUnicidade)).Returns(true);

        var result = await _handler.HandleAsync(comando);

        result.IsSuccess.Should().BeTrue();
        result.Value.LeadId.Should().Be(vencedor.Id.ToString());
    }

    [Fact]
    public async Task HandleAsync_CommitViolaUnicidadeEVencedorTemHashDiferente_RetornaConflito()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        var comando = ComandoValido(treinador.Id, idempotencyKey: "chave-corrida");
        var vencedor = Lead.Criar(
            treinador.Id, "Outro Nome", ContatoLead.Criar(TipoContatoLead.Email, "outro@lead.com").Value,
            null, ConsentimentoLead.Criar("Outra finalidade", DateTime.UtcNow, DateTime.UtcNow).Value,
            null, LeadSource.Agent, comando.IdempotencyKey, "hash-diferente", DateTime.UtcNow).Value;

        _leadRepo.SetupSequence(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-corrida", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null)
            .ReturnsAsync(vencedor);
        var excecaoDeUnicidade = new InvalidOperationException("23505");
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(excecaoDeUnicidade);
        _databaseErrorInspector.Setup(d => d.EhViolacaoDeUnicidade(excecaoDeUnicidade)).Returns(true);

        var result = await _handler.HandleAsync(comando);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(LeadAgenteErrors.IdempotencyConflito);
    }

    [Fact]
    public async Task HandleAsync_CommitViolaUnicidadeMasRreconsultaNaoAchaNinguem_PropagaExcecaoOriginal()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        var comando = ComandoValido(treinador.Id, idempotencyKey: "chave-corrida");

        _leadRepo.Setup(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-corrida", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);
        var excecaoDeUnicidade = new InvalidOperationException("23505");
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(excecaoDeUnicidade);
        _databaseErrorInspector.Setup(d => d.EhViolacaoDeUnicidade(excecaoDeUnicidade)).Returns(true);

        var act = async () => await _handler.HandleAsync(comando);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HandleAsync_CommitFalhaPorMotivoNaoRelacionadoAUnicidade_PropagaExcecaoSemReconsultar()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        var comando = ComandoValido(treinador.Id, idempotencyKey: "chave-corrida");

        _leadRepo.Setup(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-corrida", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);
        var excecaoDeConexao = new InvalidOperationException("timeout de conexao");
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(excecaoDeConexao);
        _databaseErrorInspector.Setup(d => d.EhViolacaoDeUnicidade(excecaoDeConexao)).Returns(false);

        var act = async () => await _handler.HandleAsync(comando);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _leadRepo.Verify(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-corrida", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
