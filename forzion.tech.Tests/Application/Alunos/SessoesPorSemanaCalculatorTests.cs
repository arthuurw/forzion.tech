using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.Dashboard;

namespace forzion.tech.Tests.Application.Alunos;

public class SessoesPorSemanaCalculatorTests
{
    private static readonly DateTime Agora = new(2026, 6, 25, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Bucketizar_MapeiaCadaDiaParaSuaSemanaEZeroPreencheAsVazias()
    {
        var dias = new List<SessaoDiaCount>
        {
            new(new DateTime(2026, 6, 25), 3),
            new(new DateTime(2026, 6, 19), 2),
            new(new DateTime(2026, 6, 18), 1),
            new(new DateTime(2026, 6, 12), 4),
            new(new DateTime(2026, 6, 4), 2),
            new(new DateTime(2026, 4, 30), 9),
            new(new DateTime(2026, 4, 29), 9),
        };

        var buckets = SessoesPorSemanaCalculator.Bucketizar(Agora, 8, dias);

        buckets.Should().HaveCount(8);

        buckets[7].SemanaInicio.Should().Be(new DateTime(2026, 6, 19));
        buckets[7].SemanaFim.Should().Be(new DateTime(2026, 6, 25));
        buckets[7].Total.Should().Be(5);

        buckets[6].SemanaInicio.Should().Be(new DateTime(2026, 6, 12));
        buckets[6].SemanaFim.Should().Be(new DateTime(2026, 6, 18));
        buckets[6].Total.Should().Be(5);

        buckets[4].Total.Should().Be(2);

        buckets[0].SemanaInicio.Should().Be(new DateTime(2026, 5, 1));
        buckets[0].Total.Should().Be(0);

        buckets.Sum(b => b.Total).Should().Be(12);
    }

    [Fact]
    public void Bucketizar_SemSessoes_RetornaOitoSemanasZeradas()
    {
        var buckets = SessoesPorSemanaCalculator.Bucketizar(Agora, 8, []);

        buckets.Should().HaveCount(8);
        buckets.Should().OnlyContain(b => b.Total == 0);
    }
}
