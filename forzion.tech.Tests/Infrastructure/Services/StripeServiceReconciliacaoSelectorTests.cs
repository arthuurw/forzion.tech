using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Infrastructure.Services;

namespace forzion.tech.Tests.Infrastructure.Services;

public class StripeServiceReconciliacaoSelectorTests
{
    private static StripeEventSummary Evento(string id, DateTime created) =>
        new(EventId: id, Type: "payment_intent.succeeded", PayloadRaw: "{}", Created: created);

    [Fact]
    public void SelecionarMaisAntigos_AcimaDoCap_RetemMaisAntigosAscETruncado()
    {
        var e3 = Evento("evt_3", new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc));
        var e2 = Evento("evt_2", new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc));
        var e1 = Evento("evt_1", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var descNewestFirst = new[] { e3, e2, e1 };

        var (asc, truncado) = StripeService.SelecionarMaisAntigos(descNewestFirst, cap: 2);

        truncado.Should().BeTrue();
        asc.Select(e => e.EventId).Should().Equal("evt_1", "evt_2");
    }

    [Fact]
    public void SelecionarMaisAntigos_AbaixoDoCap_RetemTodosAscSemTruncado()
    {
        var e2 = Evento("evt_2", new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc));
        var e1 = Evento("evt_1", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var descNewestFirst = new[] { e2, e1 };

        var (asc, truncado) = StripeService.SelecionarMaisAntigos(descNewestFirst, cap: 5);

        truncado.Should().BeFalse();
        asc.Select(e => e.EventId).Should().Equal("evt_1", "evt_2");
    }

    [Fact]
    public void SelecionarMaisAntigos_ExatamenteNoCap_SemTruncado()
    {
        var e3 = Evento("evt_3", new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc));
        var e2 = Evento("evt_2", new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc));
        var e1 = Evento("evt_1", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var descNewestFirst = new[] { e3, e2, e1 };

        var (asc, truncado) = StripeService.SelecionarMaisAntigos(descNewestFirst, cap: 3);

        truncado.Should().BeFalse();
        asc.Select(e => e.EventId).Should().Equal("evt_1", "evt_2", "evt_3");
    }
}
