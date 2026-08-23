using FluentAssertions;
using forzion.tech.Domain.Services;

namespace forzion.tech.Tests.Domain.Services;

public class ConversaoFusoTests
{
    private static readonly TimeZoneInfo FusoSaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly TimeZoneInfo FusoNewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    [Fact]
    public void ParaUtc_RecebeDateTimeComKindDiferenteDeUnspecified_NaoLancaEConverteCorretamente()
    {
        var wallClockComKindUtc = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);

        var resultado = ConversaoFuso.ParaUtc(wallClockComKindUtc, FusoSaoPaulo);

        resultado.Should().Be(new DateTime(2026, 5, 25, 15, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ParaUtc_HoraInexistentePorDst_RetornaNuloSemLancar()
    {
        var horaInvalida = EncontrarHoraInvalida(FusoNewYork, 2026);

        var resultado = ConversaoFuso.ParaUtc(horaInvalida, FusoNewYork);

        resultado.Should().BeNull();
    }

    [Fact]
    public void ParaUtc_HoraAmbiguaPorDst_UsaBaseUtcOffset()
    {
        var horaAmbigua = EncontrarHoraAmbigua(FusoNewYork, 2026);
        var esperado = DateTime.SpecifyKind(horaAmbigua - FusoNewYork.BaseUtcOffset, DateTimeKind.Utc);

        var resultado = ConversaoFuso.ParaUtc(horaAmbigua, FusoNewYork);

        resultado.Should().Be(esperado);
    }

    private static DateTime EncontrarHoraInvalida(TimeZoneInfo fuso, int ano)
    {
        for (var dia = new DateTime(ano, 1, 1); dia.Year == ano; dia = dia.AddDays(1))
            for (var minuto = 0; minuto < 24 * 60; minuto += 15)
            {
                var candidato = dia.AddMinutes(minuto);
                if (fuso.IsInvalidTime(candidato))
                    return candidato;
            }

        throw new InvalidOperationException("Nenhuma hora inválida encontrada no ano informado para o fuso informado.");
    }

    private static DateTime EncontrarHoraAmbigua(TimeZoneInfo fuso, int ano)
    {
        for (var dia = new DateTime(ano, 1, 1); dia.Year == ano; dia = dia.AddDays(1))
            for (var minuto = 0; minuto < 24 * 60; minuto += 15)
            {
                var candidato = dia.AddMinutes(minuto);
                if (fuso.IsAmbiguousTime(candidato))
                    return candidato;
            }

        throw new InvalidOperationException("Nenhuma hora ambígua encontrada no ano informado para o fuso informado.");
    }
}
