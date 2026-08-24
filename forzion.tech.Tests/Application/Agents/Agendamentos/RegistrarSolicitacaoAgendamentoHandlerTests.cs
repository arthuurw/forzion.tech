using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Agents.Agendamentos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Services;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Agents.Agendamentos;

public class RegistrarSolicitacaoAgendamentoHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IBloqueioAgendaRepository> _bloqueioRepo = new();
    private readonly Mock<ISolicitacaoAgendamentoRepository> _solicitacaoRepo = new();
    private readonly Mock<ILeadRepository> _leadRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDatabaseErrorInspector> _databaseErrorInspector = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 23, 0, 0, 0, TimeSpan.Zero));
    private readonly RegistrarSolicitacaoAgendamentoHandler _handler;

    // Sao Paulo (fuso padrão do TreinadorBuilder), horário Monday 08:00-09:00 local, duração 60min:
    // primeiro slot derivado a partir de "agora" = 2026-08-23 é 2026-08-24T11:00:00Z.
    private static readonly DateTime InicioSlotValido = new(2026, 8, 24, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FimSlotValido = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    public RegistrarSolicitacaoAgendamentoHandlerTests()
    {
        var resolvedor = new ResolvedorLeadAgendamento(_leadRepo.Object);
        _handler = new RegistrarSolicitacaoAgendamentoHandler(
            _treinadorRepo.Object, _pacoteRepo.Object, _bloqueioRepo.Object, _solicitacaoRepo.Object, resolvedor,
            _unitOfWork.Object, _timeProvider, _databaseErrorInspector.Object);
        _bloqueioRepo.Setup(r => r.ListarVigentesAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BloqueioAgenda>)[]);
        _solicitacaoRepo.Setup(r => r.ContarConfirmadasSobrepostasAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _leadRepo.Setup(r => r.ObterReutilizavelPorContatoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);
    }

    private static Treinador CriarTreinadorPublicado()
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio Teste", null, null, DateTime.UtcNow);
        treinador.PerfilPublico.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(9, 0), DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        return treinador;
    }

    private (Treinador Treinador, Pacote Pacote) SetupTenant(
        int capacidadeMaxima = 3, bool ativo = true, bool publico = true, int? duracaoMinutos = 60)
    {
        var treinador = CriarTreinadorPublicado();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.AtualizarCatalogoPublico("Categoria", duracaoMinutos, false, DateTime.UtcNow, capacidadeMaxima: capacidadeMaxima);
        if (publico)
            pacote.TornarPublico(DateTime.UtcNow);
        if (!ativo)
            pacote.Inativar(DateTime.UtcNow);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        return (treinador, pacote);
    }

    private static RegistrarSolicitacaoAgendamentoCommand ComandoValido(
        Guid tenantId,
        Guid serviceId,
        string? slotId = null,
        string name = "Fulano",
        string contactType = "email",
        string contactValue = "fulano@lead.com",
        bool consentGranted = true,
        string consentPurpose = "Contato comercial",
        DateTime? consentGrantedAt = null,
        string idempotencyKey = "chave-1",
        string? originUserAgent = null,
        string? originAssistant = null) =>
        new(tenantId, serviceId, slotId ?? SlotId.Calcular(tenantId, serviceId, InicioSlotValido),
            name, contactType, contactValue, consentGranted, consentPurpose, consentGrantedAt, idempotencyKey, originUserAgent, originAssistant);

    // --- AGF4-01 / AGF4-14: caminho feliz ---

    [Fact]
    public async Task HandleAsync_DadosValidos_CriaSolicitacaoPendenteEProjetaStatusLiteral()
    {
        var (treinador, pacote) = SetupTenant();
        SolicitacaoAgendamento? capturada = null;
        _solicitacaoRepo.Setup(r => r.AdicionarAsync(It.IsAny<SolicitacaoAgendamento>(), It.IsAny<CancellationToken>()))
            .Callback<SolicitacaoAgendamento, CancellationToken>((s, _) => capturada = s)
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id, pacote.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("pending-agent");
        result.Value.BookingRequestId.Should().Be(capturada!.Id.ToString());
        capturada.TreinadorId.Should().Be(treinador.Id);
        capturada.PacoteId.Should().Be(pacote.Id);
        capturada.InicioUtc.Should().Be(InicioSlotValido, "InicioUtc vem do slot derivado, nunca do command");
        capturada.FimUtc.Should().Be(FimSlotValido);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ComandoNaoTemCampoDeData_ApenasSlotIdELead()
    {
        typeof(RegistrarSolicitacaoAgendamentoCommand).GetProperties().Select(p => p.Name)
            .Should().NotContain(["InicioUtc", "FimUtc", "StartsAt", "EndsAt"],
                "nenhum campo de data pode ser lido do command — o instante vem sempre do slot derivado no servidor");
    }

    // --- AGF4-06: consentimento não concedido ⇒ validation_failed ---

    [Fact]
    public async Task HandleAsync_ConsentGrantedFalso_RetornaValidacaoENadaPersiste()
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), Guid.NewGuid(), consentGranted: false));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoAgenteErrors.ConsentimentoNaoConcedido);
        _solicitacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<SolicitacaoAgendamento>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _treinadorRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- AGF4-05: tenant não encontrado/inativo/não publicado ⇒ tenant_not_found ---

    [Fact]
    public async Task HandleAsync_TenantInexistente_RetornaTreinadorNaoEncontrado()
    {
        var tenantId = Guid.NewGuid();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var result = await _handler.HandleAsync(ComandoValido(tenantId, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    // --- AGF4-04: serviço não existe / não público / sem duração ⇒ service_not_found ---

    [Fact]
    public async Task HandleAsync_ServicoInexistente_RetornaPacoteNaoEncontrado()
    {
        var treinador = CriarTreinadorPublicado();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        var serviceId = Guid.NewGuid();
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(serviceId, It.IsAny<CancellationToken>())).ReturnsAsync((Pacote?)null);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id, serviceId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(PacoteErrors.NaoEncontrado);
    }

    // --- D-H: pacote inativo ⇒ mesmo erro de serviço não encontrado ---

    [Fact]
    public async Task HandleAsync_PacoteInativo_RetornaServicoNaoEncontrado()
    {
        var (treinador, pacote) = SetupTenant(ativo: false);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id, pacote.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(PacoteErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_PacoteNaoPublico_RetornaServicoNaoEncontrado()
    {
        var (treinador, pacote) = SetupTenant(publico: false);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id, pacote.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(PacoteErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_PacoteSemDuracaoMinutos_RetornaServicoNaoEncontrado()
    {
        var (treinador, pacote) = SetupTenant(duracaoMinutos: null);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id, pacote.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(PacoteErrors.NaoEncontrado);
    }

    // --- AGF4-02: slotId não derivável ⇒ slot_not_found ---

    [Fact]
    public async Task HandleAsync_SlotIdInexistente_RetornaSlotNaoEncontrado()
    {
        var (treinador, pacote) = SetupTenant();

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id, pacote.Id, slotId: "slot-que-nunca-existiu"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoAgenteErrors.SlotNaoEncontrado);
    }

    // --- AGF4-03: slot lotado ⇒ slot_unavailable ---

    [Fact]
    public async Task HandleAsync_SlotLotado_RetornaSlotIndisponivel()
    {
        var (treinador, pacote) = SetupTenant(capacidadeMaxima: 1);
        _solicitacaoRepo.Setup(r => r.ContarConfirmadasSobrepostasAsync(treinador.Id, pacote.Id, InicioSlotValido, FimSlotValido, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id, pacote.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoAgenteErrors.SlotIndisponivel);
        _solicitacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<SolicitacaoAgendamento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- AGF4-09: mesma chave, argumentos diferentes ⇒ idempotency_conflict ---

    [Fact]
    public async Task HandleAsync_MesmaChaveArgumentosDiferentes_RetornaIdempotencyConflict()
    {
        var (treinador, pacote) = SetupTenant();
        var existente = SolicitacaoAgendamento.Criar(
            treinador.Id, pacote.Id, Guid.NewGuid(), SlotId.Calcular(treinador.Id, pacote.Id, InicioSlotValido),
            InicioSlotValido, FimSlotValido, "chave-repetida", "hash-diferente", DateTime.UtcNow).Value;
        _solicitacaoRepo.Setup(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-repetida", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existente);

        var result = await _handler.HandleAsync(ComandoValido(treinador.Id, pacote.Id, idempotencyKey: "chave-repetida"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoAgenteErrors.IdempotencyConflito);
        _solicitacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<SolicitacaoAgendamento>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- AGF4-08: mesma chave, mesmos argumentos ⇒ devolve o registro existente sem criar segundo ---

    [Fact]
    public async Task HandleAsync_MesmaChaveMesmosArgumentos_RetornaRegistroExistenteSemPersistirNovo()
    {
        var (treinador, pacote) = SetupTenant();
        var comando = ComandoValido(treinador.Id, pacote.Id, idempotencyKey: "chave-repetida");
        var hash = IdempotenciaAgendamento.Calcular(pacote.Id, comando.SlotId, comando.Name, TipoContatoLead.Email, comando.ContactValue, comando.ConsentPurpose);
        var existente = SolicitacaoAgendamento.Criar(
            treinador.Id, pacote.Id, Guid.NewGuid(), comando.SlotId, InicioSlotValido, FimSlotValido, "chave-repetida", hash, DateTime.UtcNow).Value;
        _solicitacaoRepo.Setup(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-repetida", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existente);

        var result = await _handler.HandleAsync(comando);

        result.IsSuccess.Should().BeTrue();
        result.Value.BookingRequestId.Should().Be(existente.Id.ToString());
        result.Value.Status.Should().Be("pending-agent");
        _solicitacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<SolicitacaoAgendamento>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- AGF4-07: idempotencyKey ausente/vazia ⇒ validation_failed (campo required no contrato) ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_IdempotencyKeyAusenteOuVazia_RetornaValidacao(string idempotencyKeyVazia)
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), Guid.NewGuid(), idempotencyKey: idempotencyKeyVazia));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.IdempotencyKeyObrigatoria);
        _treinadorRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- AGF4-07: name > 200 ⇒ validation_failed ---

    [Fact]
    public async Task HandleAsync_NomeMuitoLongo_RetornaValidacao()
    {
        var nomeMuitoLongo = new string('a', 201);

        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), Guid.NewGuid(), name: nomeMuitoLongo));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(LeadErrors.NomeMuitoLongo);
        _treinadorRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- AGF4-07: contact.type fora de phone/email/whatsapp ⇒ validation_failed ---

    [Fact]
    public async Task HandleAsync_TipoContatoInvalido_RetornaValidacao()
    {
        var result = await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), Guid.NewGuid(), contactType: "sms"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoAgenteErrors.TipoContatoInvalido);
        _treinadorRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- AGF4-07: slotId ausente/vazio/em branco ⇒ validation_failed ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_SlotIdAusenteOuVazio_RetornaValidacao(string? slotIdVazio)
    {
        var comando = ComandoValido(Guid.NewGuid(), Guid.NewGuid()) with { SlotId = slotIdVazio! };

        var result = await _handler.HandleAsync(comando);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoErrors.SlotIdObrigatorio);
        _treinadorRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- AGF4-10: corrida de idempotência resolvida pela violação de unicidade, nunca 500 ---

    [Fact]
    public async Task HandleAsync_CommitViolaUnicidadeEVencedorTemMesmoHash_RetornaRegistroDoVencedorSemPropagarExcecao()
    {
        var (treinador, pacote) = SetupTenant();
        var comando = ComandoValido(treinador.Id, pacote.Id, idempotencyKey: "chave-corrida");
        var hash = IdempotenciaAgendamento.Calcular(pacote.Id, comando.SlotId, comando.Name, TipoContatoLead.Email, comando.ContactValue, comando.ConsentPurpose);
        var vencedor = SolicitacaoAgendamento.Criar(
            treinador.Id, pacote.Id, Guid.NewGuid(), comando.SlotId, InicioSlotValido, FimSlotValido, "chave-corrida", hash, DateTime.UtcNow).Value;

        _solicitacaoRepo.SetupSequence(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-corrida", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SolicitacaoAgendamento?)null)
            .ReturnsAsync(vencedor);
        var excecaoDeUnicidade = new InvalidOperationException("23505");
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(excecaoDeUnicidade);
        _databaseErrorInspector.Setup(d => d.EhViolacaoDeUnicidade(excecaoDeUnicidade)).Returns(true);

        var result = await _handler.HandleAsync(comando);

        result.IsSuccess.Should().BeTrue();
        result.Value.BookingRequestId.Should().Be(vencedor.Id.ToString());
    }

    [Fact]
    public async Task HandleAsync_CommitViolaUnicidadeEVencedorTemHashDiferente_RetornaConflito()
    {
        var (treinador, pacote) = SetupTenant();
        var comando = ComandoValido(treinador.Id, pacote.Id, idempotencyKey: "chave-corrida");
        var vencedor = SolicitacaoAgendamento.Criar(
            treinador.Id, pacote.Id, Guid.NewGuid(), comando.SlotId, InicioSlotValido, FimSlotValido, "chave-corrida", "hash-diferente", DateTime.UtcNow).Value;

        _solicitacaoRepo.SetupSequence(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-corrida", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SolicitacaoAgendamento?)null)
            .ReturnsAsync(vencedor);
        var excecaoDeUnicidade = new InvalidOperationException("23505");
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(excecaoDeUnicidade);
        _databaseErrorInspector.Setup(d => d.EhViolacaoDeUnicidade(excecaoDeUnicidade)).Returns(true);

        var result = await _handler.HandleAsync(comando);

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(SolicitacaoAgendamentoAgenteErrors.IdempotencyConflito);
    }

    [Fact]
    public async Task HandleAsync_CommitViolaUnicidadeMasRereleituraNaoAchaNinguem_RelancaExcecaoOriginal()
    {
        var (treinador, pacote) = SetupTenant();
        var comando = ComandoValido(treinador.Id, pacote.Id, idempotencyKey: "chave-corrida");

        _solicitacaoRepo.Setup(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-corrida", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SolicitacaoAgendamento?)null);
        var excecaoDeUnicidade = new InvalidOperationException("23505");
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(excecaoDeUnicidade);
        _databaseErrorInspector.Setup(d => d.EhViolacaoDeUnicidade(excecaoDeUnicidade)).Returns(true);

        var act = async () => await _handler.HandleAsync(comando);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HandleAsync_CommitFalhaPorMotivoNaoRelacionadoAUnicidade_PropagaExcecaoSemReconsultar()
    {
        var (treinador, pacote) = SetupTenant();
        var comando = ComandoValido(treinador.Id, pacote.Id, idempotencyKey: "chave-conexao");

        _solicitacaoRepo.Setup(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-conexao", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SolicitacaoAgendamento?)null);
        var excecaoDeConexao = new InvalidOperationException("timeout de conexao");
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(excecaoDeConexao);
        _databaseErrorInspector.Setup(d => d.EhViolacaoDeUnicidade(excecaoDeConexao)).Returns(false);

        var act = async () => await _handler.HandleAsync(comando);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _solicitacaoRepo.Verify(r => r.ObterPorIdempotencyKeyAsync(treinador.Id, "chave-conexao", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
