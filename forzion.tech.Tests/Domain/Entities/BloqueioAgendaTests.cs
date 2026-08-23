using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class BloqueioAgendaTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();

    // --- CriarPontual ---

    [Fact]
    public void CriarPontual_DadosValidos_RetornaBloqueio()
    {
        var inicio = TestData.Agora;
        var fim = TestData.Agora.AddHours(2);

        var b = BloqueioAgenda.CriarPontual(TreinadorId, inicio, fim, "Férias", TestData.Agora).Value;

        b.Id.Should().NotBeEmpty();
        b.TreinadorId.Should().Be(TreinadorId);
        b.Tipo.Should().Be(TipoBloqueio.Pontual);
        b.InicioUtc.Should().Be(inicio);
        b.FimUtc.Should().Be(fim);
        b.Motivo.Should().Be("Férias");
        b.DiaSemana.Should().BeNull();
        b.HoraInicio.Should().BeNull();
        b.HoraFim.Should().BeNull();
    }

    [Fact]
    public void CriarPontual_SemMotivo_MotivoNulo()
    {
        var b = BloqueioAgenda.CriarPontual(TreinadorId, TestData.Agora, TestData.Agora.AddHours(1), null, TestData.Agora).Value;
        b.Motivo.Should().BeNull();
    }

    [Fact]
    public void CriarPontual_TreinadorIdVazio_Falha()
    {
        var r = BloqueioAgenda.CriarPontual(Guid.Empty, TestData.Agora, TestData.Agora.AddHours(1), null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("bloqueio_agenda.treinador_id_invalido");
    }

    [Fact]
    public void CriarPontual_InicioIgualFim_Falha()
    {
        var r = BloqueioAgenda.CriarPontual(TreinadorId, TestData.Agora, TestData.Agora, null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("bloqueio_agenda.intervalo_pontual_invalido");
    }

    [Fact]
    public void CriarPontual_InicioDepoisDoFim_Falha()
    {
        var r = BloqueioAgenda.CriarPontual(TreinadorId, TestData.Agora.AddHours(2), TestData.Agora, null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("bloqueio_agenda.intervalo_pontual_invalido");
    }

    [Fact]
    public void CriarPontual_MotivoMuitoLongo_Falha()
    {
        var r = BloqueioAgenda.CriarPontual(TreinadorId, TestData.Agora, TestData.Agora.AddHours(1), new string('a', 201), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("bloqueio_agenda.motivo_muito_longo");
    }

    // --- CriarRecorrente ---

    [Fact]
    public void CriarRecorrente_DadosValidos_RetornaBloqueio()
    {
        var b = BloqueioAgenda.CriarRecorrente(TreinadorId, 1, new TimeOnly(9, 0), new TimeOnly(10, 0), "Almoço", TestData.Agora).Value;

        b.Tipo.Should().Be(TipoBloqueio.RecorrenteSemanal);
        b.DiaSemana.Should().Be(1);
        b.HoraInicio.Should().Be(new TimeOnly(9, 0));
        b.HoraFim.Should().Be(new TimeOnly(10, 0));
        b.Motivo.Should().Be("Almoço");
        b.InicioUtc.Should().BeNull();
        b.FimUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void CriarRecorrente_DiaSemanaInvalido_Falha(int diaSemana)
    {
        var r = BloqueioAgenda.CriarRecorrente(TreinadorId, diaSemana, new TimeOnly(9, 0), new TimeOnly(10, 0), null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("bloqueio_agenda.dia_semana_invalido");
    }

    [Fact]
    public void CriarRecorrente_HoraInicioIgualHoraFim_Falha()
    {
        var r = BloqueioAgenda.CriarRecorrente(TreinadorId, 1, new TimeOnly(9, 0), new TimeOnly(9, 0), null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("bloqueio_agenda.intervalo_recorrente_invalido");
    }

    [Fact]
    public void CriarRecorrente_HoraInicioDepoisDaHoraFim_Falha()
    {
        var r = BloqueioAgenda.CriarRecorrente(TreinadorId, 1, new TimeOnly(10, 0), new TimeOnly(9, 0), null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("bloqueio_agenda.intervalo_recorrente_invalido");
    }

    [Fact]
    public void CriarRecorrente_MotivoMuitoLongo_Falha()
    {
        var r = BloqueioAgenda.CriarRecorrente(TreinadorId, 1, new TimeOnly(9, 0), new TimeOnly(10, 0), new string('a', 201), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("bloqueio_agenda.motivo_muito_longo");
    }

    [Fact]
    public void CriarRecorrente_TreinadorIdVazio_Falha()
    {
        var r = BloqueioAgenda.CriarRecorrente(Guid.Empty, 1, new TimeOnly(9, 0), new TimeOnly(10, 0), null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("bloqueio_agenda.treinador_id_invalido");
    }
}
