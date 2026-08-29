using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Agents.Disponibilidade;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Services;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Agents.Disponibilidade;

public class ConsultarDisponibilidadeAgenteHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IBloqueioAgendaRepository> _bloqueioRepo = new();
    private readonly Mock<ISolicitacaoAgendamentoRepository> _solicitacaoRepo = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 23, 0, 0, 0, TimeSpan.Zero));
    private readonly ConsultarDisponibilidadeAgenteHandler _handler;

    public ConsultarDisponibilidadeAgenteHandlerTests()
    {
        _handler = new ConsultarDisponibilidadeAgenteHandler(_treinadorRepo.Object, _pacoteRepo.Object, _bloqueioRepo.Object, _solicitacaoRepo.Object, _timeProvider);
        SetupSemBloqueios();
        SetupSemConfirmadas();
    }

    private static Treinador CriarTreinadorPublicado()
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio Teste", null, null, DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        return treinador;
    }

    private void SetupTreinador(Treinador? treinador, Guid tenantId) =>
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

    private void SetupPacote(Pacote? pacote, Guid serviceId) =>
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(serviceId, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

    private void SetupSemBloqueios() =>
        _bloqueioRepo.Setup(r => r.ListarVigentesAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BloqueioAgenda>)[]);

    private void SetupSemConfirmadas() =>
        _solicitacaoRepo.Setup(r => r.ListarConfirmadasNoIntervaloAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SolicitacaoAgendamento>)[]);

    private static ConsultarDisponibilidadeQuery Query(Guid tenantId, Guid serviceId, DateTime from, DateTime to) =>
        new(tenantId, serviceId, from, to);

    // --- AGF3-11: tenant inexistente/inativo/não publicado colapsam no mesmo erro ---

    [Fact]
    public async Task HandleAsync_TenantInexistente_RetornaTreinadorNaoEncontrado()
    {
        var tenantId = Guid.NewGuid();
        SetupTreinador(null, tenantId);

        var result = await _handler.HandleAsync(Query(tenantId, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_TenantInativo_RetornaMesmoErroDoInexistente()
    {
        var treinador = CriarTreinadorPublicado();
        treinador.Inativar(DateTime.UtcNow);
        SetupTreinador(treinador, treinador.Id);

        var result = await _handler.HandleAsync(Query(treinador.Id, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1)));

        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_TenantNaoPublicado_RetornaMesmoErroDoInexistente()
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        SetupTreinador(treinador, treinador.Id);

        var result = await _handler.HandleAsync(Query(treinador.Id, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(1)));

        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    // --- AGF3-12: serviço inexistente/não público/sem duração colapsam num erro distinto do de tenant ---

    [Fact]
    public async Task HandleAsync_ServicoInexistente_RetornaPacoteNaoEncontradoDistintoDeTenant()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        var serviceId = Guid.NewGuid();
        SetupPacote(null, serviceId);

        var result = await _handler.HandleAsync(Query(treinador.Id, serviceId, DateTime.UtcNow, DateTime.UtcNow.AddDays(1)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(PacoteErrors.NaoEncontrado);
        result.Error.Should().NotBe(TreinadorErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_ServicoNaoPublico_RetornaPacoteNaoEncontrado()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.AtualizarCatalogoPublico("Categoria", 60, false, DateTime.UtcNow);
        SetupPacote(pacote, pacote.Id);

        var result = await _handler.HandleAsync(Query(treinador.Id, pacote.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(1)));

        result.Error!.Should().Be(PacoteErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_ServicoSemDuracao_RetornaPacoteNaoEncontrado()
    {
        var treinador = CriarTreinadorPublicado();
        SetupTreinador(treinador, treinador.Id);
        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.AtualizarCatalogoPublico("Categoria", null, false, DateTime.UtcNow);
        pacote.TornarPublico(DateTime.UtcNow);
        SetupPacote(pacote, pacote.Id);

        var result = await _handler.HandleAsync(Query(treinador.Id, pacote.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(1)));

        result.Error!.Should().Be(PacoteErrors.NaoEncontrado);
    }

    // --- AGF3-01/04: caminho feliz — shape projetado ---

    [Fact]
    public async Task HandleAsync_ServicoValido_ProjetaSlotComOsCincoCamposEValoresEsperados()
    {
        var treinador = CriarTreinadorPublicado();
        treinador.PerfilPublico.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(9, 0), DateTime.UtcNow);
        SetupTreinador(treinador, treinador.Id);
        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.AtualizarCatalogoPublico("Categoria", 60, false, DateTime.UtcNow, capacidadeMaxima: 3);
        pacote.TornarPublico(DateTime.UtcNow);
        SetupPacote(pacote, pacote.Id);

        var from = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc);
        var result = await _handler.HandleAsync(Query(treinador.Id, pacote.Id, from, to));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        var slot = result.Value[0];
        var inicioUtcEsperado = new DateTime(2026, 8, 24, 11, 0, 0, DateTimeKind.Utc);
        slot.SlotId.Should().Be(SlotId.Calcular(treinador.Id, pacote.Id, inicioUtcEsperado));
        slot.ServiceId.Should().Be(pacote.Id.ToString());
        slot.StartsAt.Should().Be(new DateTimeOffset(2026, 8, 24, 11, 0, 0, TimeSpan.Zero));
        slot.EndsAt.Should().Be(new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero));
        slot.CapacityRemaining.Should().Be(3);
    }

    // --- janela auxiliar cobre a cauda do último slot ---

    [Fact]
    public async Task HandleAsync_BloqueioComecaDepoisDeToUtcMasSobrepoeUltimoSlot_RemoveOSlot()
    {
        var treinador = CriarTreinadorPublicado();
        treinador.PerfilPublico.AdicionarHorario((int)DayOfWeek.Monday, new TimeOnly(22, 0), new TimeOnly(23, 0), DateTime.UtcNow);
        SetupTreinador(treinador, treinador.Id);
        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.AtualizarCatalogoPublico("Categoria", 60, false, DateTime.UtcNow);
        pacote.TornarPublico(DateTime.UtcNow);
        SetupPacote(pacote, pacote.Id);

        // Turno 22:00-23:00 local (America/Sao_Paulo, UTC-3) de segunda 2026-08-24 produz um único
        // slot: 01:00-02:00 UTC de terça 2026-08-25. `to` corta em 01:30, no MEIO do slot.
        var from = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 8, 25, 1, 30, 0, DateTimeKind.Utc);
        var duracaoMinutos = 60;

        // Bloqueio começa em 01:45 (DEPOIS de `to`=01:30) mas termina em 01:50, ainda dentro do
        // slot [01:00,02:00). Só visível pra quem consulta bloqueios até to+duracaoMinutos=02:30 —
        // uma implementação que consulta só até `to` não o enxerga.
        var inicioBloqueio = new DateTime(2026, 8, 25, 1, 45, 0, DateTimeKind.Utc);
        var fimBloqueio = new DateTime(2026, 8, 25, 1, 50, 0, DateTimeKind.Utc);
        var bloqueio = BloqueioAgenda.CriarPontual(treinador.Id, inicioBloqueio, fimBloqueio, null, DateTime.UtcNow).Value;

        _bloqueioRepo.Setup(r => r.ListarVigentesAsync(
                treinador.Id, from, It.Is<DateTime>(ate => ate >= to.AddMinutes(duracaoMinutos)), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BloqueioAgenda>)[bloqueio]);

        var result = await _handler.HandleAsync(Query(treinador.Id, pacote.Id, from, to));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty("o bloqueio sobrepõe a cauda do único slot, mesmo começando depois de ToUtc");
    }

    [Fact]
    public async Task HandleAsync_ConfirmadaComecaDepoisDeToUtcMasSobrepoeUltimoSlot_AbateACapacidade()
    {
        var treinador = CriarTreinadorPublicado();
        treinador.PerfilPublico.AdicionarHorario((int)DayOfWeek.Monday, new TimeOnly(22, 0), new TimeOnly(23, 0), DateTime.UtcNow);
        SetupTreinador(treinador, treinador.Id);
        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.AtualizarCatalogoPublico("Categoria", 60, false, DateTime.UtcNow, capacidadeMaxima: 1);
        pacote.TornarPublico(DateTime.UtcNow);
        SetupPacote(pacote, pacote.Id);

        var from = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 8, 25, 1, 30, 0, DateTimeKind.Utc);
        var duracaoMinutos = 60;

        // Confirmada começa em 01:45 (DEPOIS de `to`=01:30) mas termina em 01:50, dentro do slot
        // [01:00,02:00) — só visível pra quem consulta confirmadas até to+duracaoMinutos=02:30.
        var inicioConfirmada = new DateTime(2026, 8, 25, 1, 45, 0, DateTimeKind.Utc);
        var fimConfirmada = new DateTime(2026, 8, 25, 1, 50, 0, DateTimeKind.Utc);
        var confirmada = SolicitacaoAgendamento.Criar(
            treinador.Id, pacote.Id, Guid.NewGuid(), "slot-outro", inicioConfirmada, fimConfirmada,
            "idem-key", "hash", DateTime.UtcNow).Value;
        confirmada.Confirmar(Guid.NewGuid(), DateTime.UtcNow);

        _solicitacaoRepo.Setup(r => r.ListarConfirmadasNoIntervaloAsync(
                treinador.Id, from, It.Is<DateTime>(ate => ate >= to.AddMinutes(duracaoMinutos)), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SolicitacaoAgendamento>)[confirmada]);

        var result = await _handler.HandleAsync(Query(treinador.Id, pacote.Id, from, to));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty("a confirmada sobrepõe a cauda do único slot, mesmo começando depois de ToUtc");
    }

    [Fact]
    public void AvailabilitySlotResponse_PropriedadesPublicas_SaoExatamenteAsCincoDoSchema()
    {
        typeof(AvailabilitySlotResponse).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            ["SlotId", "ServiceId", "StartsAt", "EndsAt", "CapacityRemaining"],
            "campo extra (ex.: Motivo do bloqueio) quebraria a conformidade com o contrato por desenho");
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
