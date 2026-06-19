using FluentAssertions;
using forzion.tech.Application.Services;

namespace forzion.tech.Tests.Application.Services;

public class IdempotencyKeyTests
{
    private static readonly Guid Assinatura = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Plano = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Cobranca_SemDiscriminador_FormatoEsperado() =>
        IdempotencyKey.Cobranca("aluno", Assinatura, new DateTime(2026, 6, 19, 14, 30, 45, DateTimeKind.Utc))
            .Should().Be($"cobr:aluno:{Assinatura}:202606191430");

    [Fact]
    public void Cobranca_ComDiscriminador_IncluiSegmento() =>
        IdempotencyKey.Cobranca("troca-upgrade", Assinatura, new DateTime(2026, 6, 19, 14, 30, 0, DateTimeKind.Utc), Plano)
            .Should().Be($"cobr:troca-upgrade:{Assinatura}:{Plano}:202606191430");

    [Fact]
    public void Cobranca_MesmoMinuto_KeyEstavel()
    {
        var a = IdempotencyKey.Cobranca("aluno", Assinatura, new DateTime(2026, 6, 19, 14, 30, 5, DateTimeKind.Utc));
        var b = IdempotencyKey.Cobranca("aluno", Assinatura, new DateTime(2026, 6, 19, 14, 30, 59, DateTimeKind.Utc));
        a.Should().Be(b);
    }

    [Fact]
    public void Cobranca_MinutosDiferentes_KeyDistinta()
    {
        var a = IdempotencyKey.Cobranca("aluno", Assinatura, new DateTime(2026, 6, 19, 14, 30, 0, DateTimeKind.Utc));
        var b = IdempotencyKey.Cobranca("aluno", Assinatura, new DateTime(2026, 6, 19, 14, 31, 0, DateTimeKind.Utc));
        a.Should().NotBe(b);
    }

    [Fact]
    public void Cobranca_TiposDiferentes_KeyDistinta()
    {
        var instante = new DateTime(2026, 6, 19, 14, 30, 0, DateTimeKind.Utc);
        IdempotencyKey.Cobranca("aluno", Assinatura, instante)
            .Should().NotBe(IdempotencyKey.Cobranca("treinador", Assinatura, instante));
    }

    [Fact]
    public void Cobranca_UpgradeVsRegular_MesmoAssinaturaPlanoMinuto_NaoColidem()
    {
        var instante = new DateTime(2026, 6, 19, 14, 30, 0, DateTimeKind.Utc);
        IdempotencyKey.Cobranca("troca-upgrade", Assinatura, instante, Plano)
            .Should().NotBe(IdempotencyKey.Cobranca("troca-regular", Assinatura, instante, Plano));
    }
}
