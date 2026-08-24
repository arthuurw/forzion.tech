using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Agents.Disponibilidade;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Services;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Agents;

// R2: abatimento é sempre por SOBREPOSIÇÃO de intervalo, nunca por igualdade de slotId — um mock
// de ListarConfirmadasNoIntervaloAsync que devolve confirmadas com slotId "de outra derivação"
// (duração diferente) é o único jeito de matar uma implementação que comparasse por slotId.
public class ConsultarDisponibilidadeCapacidadeTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IBloqueioAgendaRepository> _bloqueioRepo = new();
    private readonly Mock<ISolicitacaoAgendamentoRepository> _solicitacaoRepo = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 23, 0, 0, 0, TimeSpan.Zero));
    private readonly ConsultarDisponibilidadeAgenteHandler _handler;

    private static readonly DateTime From = new(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InicioSlot0800 = new(2026, 8, 24, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FimSlot0800 = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    public ConsultarDisponibilidadeCapacidadeTests()
    {
        _handler = new ConsultarDisponibilidadeAgenteHandler(_treinadorRepo.Object, _pacoteRepo.Object, _bloqueioRepo.Object, _solicitacaoRepo.Object, _timeProvider);
        _bloqueioRepo.Setup(r => r.ListarVigentesAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<BloqueioAgenda>)[]);
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

    private (Treinador Treinador, Pacote Pacote) SetupTenant(int capacidadeMaxima, int duracaoMinutos = 60)
    {
        var treinador = CriarTreinadorPublicado();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        var pacote = new PacoteBuilder().ComTreinadorId(treinador.Id).Build();
        pacote.AtualizarCatalogoPublico("Categoria", duracaoMinutos, false, DateTime.UtcNow, capacidadeMaxima: capacidadeMaxima);
        pacote.TornarPublico(DateTime.UtcNow);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        return (treinador, pacote);
    }

    private void SetupConfirmadas(params SolicitacaoAgendamento[] confirmadas) =>
        _solicitacaoRepo.Setup(r => r.ListarConfirmadasNoIntervaloAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<SolicitacaoAgendamento>)confirmadas);

    private static SolicitacaoAgendamento CriarConfirmada(Guid treinadorId, Guid pacoteId, DateTime inicioUtc, DateTime fimUtc)
    {
        var slotId = SlotId.Calcular(treinadorId, pacoteId, inicioUtc);
        var solicitacao = SolicitacaoAgendamento.Criar(
            treinadorId, pacoteId, Guid.NewGuid(), slotId, inicioUtc, fimUtc, $"chave-{Guid.NewGuid():N}", "hash", DateTime.UtcNow).Value;
        solicitacao.Confirmar(Guid.NewGuid(), DateTime.UtcNow);
        return solicitacao;
    }

    [Fact]
    public async Task Capacidade2ComUmaConfirmada_SlotPresenteComCapacityRemainingUm()
    {
        var (treinador, pacote) = SetupTenant(capacidadeMaxima: 2);
        SetupConfirmadas(CriarConfirmada(treinador.Id, pacote.Id, InicioSlot0800, FimSlot0800));

        var result = await _handler.HandleAsync(new ConsultarDisponibilidadeQuery(treinador.Id, pacote.Id, From, To));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(s => s.StartsAt == new DateTimeOffset(InicioSlot0800, TimeSpan.Zero));
        result.Value.Single().CapacityRemaining.Should().Be(1);
    }

    [Fact]
    public async Task Capacidade1ComUmaConfirmada_SlotAusenteDaResposta()
    {
        var (treinador, pacote) = SetupTenant(capacidadeMaxima: 1);
        SetupConfirmadas(CriarConfirmada(treinador.Id, pacote.Id, InicioSlot0800, FimSlot0800));

        var result = await _handler.HandleAsync(new ConsultarDisponibilidadeQuery(treinador.Id, pacote.Id, From, To));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmadaCancelada_SlotVoltaComAVagaDevolvida()
    {
        var (treinador, pacote) = SetupTenant(capacidadeMaxima: 1);
        SetupConfirmadas(CriarConfirmada(treinador.Id, pacote.Id, InicioSlot0800, FimSlot0800));
        var comConfirmada = await _handler.HandleAsync(new ConsultarDisponibilidadeQuery(treinador.Id, pacote.Id, From, To));
        comConfirmada.Value.Should().BeEmpty("capacidade 1 com uma confirmada esgota o slot");

        // Cancelamento devolve a vaga: o repositório passa a não listar mais essa solicitação
        // entre as confirmadas do intervalo (ListarConfirmadasNoIntervaloAsync já filtra por status).
        SetupConfirmadas();

        var apoisCancelamento = await _handler.HandleAsync(new ConsultarDisponibilidadeQuery(treinador.Id, pacote.Id, From, To));

        apoisCancelamento.IsSuccess.Should().BeTrue();
        apoisCancelamento.Value.Should().ContainSingle(s => s.StartsAt == new DateTimeOffset(InicioSlot0800, TimeSpan.Zero));
        apoisCancelamento.Value.Single().CapacityRemaining.Should().Be(1);
    }

    [Fact]
    public async Task ConfirmadaComDuracaoAntigaDesalinhadaDosSlotsAtuais_ContinuaAbatendoOSlotQueElaSobrepoe()
    {
        // Pacote hoje tem duração de 60min (slot 11:00-12:00 UTC). A confirmada foi gravada quando
        // o pacote tinha 90min, começando 30min depois do slot atual — [11:30, 13:00) intersecta
        // [11:00, 12:00) mas seu slotId (hash de 11:30) NUNCA bate com o slotId do slot atual
        // (hash de 11:00). Uma implementação por igualdade de slotId não abateria nada aqui.
        var (treinador, pacote) = SetupTenant(capacidadeMaxima: 1, duracaoMinutos: 60);
        var inicioAntigo = InicioSlot0800.AddMinutes(30);
        var fimAntigo = inicioAntigo.AddMinutes(90);
        var confirmadaComDuracaoAntiga = CriarConfirmada(treinador.Id, pacote.Id, inicioAntigo, fimAntigo);
        confirmadaComDuracaoAntiga.SlotId.Should().NotBe(SlotId.Calcular(treinador.Id, pacote.Id, InicioSlot0800),
            "pré-condição do teste: o slotId da confirmada antiga não deve coincidir com o do slot atual");
        SetupConfirmadas(confirmadaComDuracaoAntiga);

        var result = await _handler.HandleAsync(new ConsultarDisponibilidadeQuery(treinador.Id, pacote.Id, From, To));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty("a confirmada antiga sobrepõe o slot atual e deve abatê-lo mesmo com slotId diferente");
    }
}
