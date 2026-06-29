using FluentAssertions;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Domain.Entities;

/// <summary>
/// Lógica de negócio temporal verificada com tempo controlado via <see cref="FakeTimeProvider"/>
/// (harness fase 1 — F1.6). Usa <see cref="FakeTimeProvider.Advance"/>, nunca tempo real.
/// </summary>
public class LogicaTemporalTests
{
    private static readonly DateTimeOffset Instante = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static AssinaturaAluno CriarAssinatura(DateTime agora) =>
        AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 199m, agora).Value;

    [Fact]
    public void AssinaturaAluno_Criar_DefineDataProximaCobranca_NoInstanteControlado()
    {
        var time = new FakeTimeProvider(Instante);

        var assinatura = CriarAssinatura(time.GetUtcNow().UtcDateTime);

        assinatura.DataInicio.Should().Be(Instante.UtcDateTime);
        assinatura.DataProximaCobranca.Should().Be(Instante.UtcDateTime);
        assinatura.CreatedAt.Should().Be(Instante.UtcDateTime);
    }

    [Fact]
    public void AgendarProximaCobranca_DataNoFuturoRelativoAoTempoControlado_Atualiza()
    {
        var time = new FakeTimeProvider(Instante);
        var assinatura = CriarAssinatura(time.GetUtcNow().UtcDateTime);

        var agora = time.GetUtcNow().UtcDateTime;
        var proxima = agora.AddMonths(1);

        assinatura.AgendarProximaCobranca(proxima, agora);

        assinatura.DataProximaCobranca.Should().Be(proxima);
        assinatura.UpdatedAt.Should().Be(agora);
    }

    [Fact]
    public void AgendarProximaCobranca_DataQueEraFuturaTornaSePassadaAposAvancarOTempo_Rejeita()
    {
        var time = new FakeTimeProvider(Instante);
        var assinatura = CriarAssinatura(time.GetUtcNow().UtcDateTime);

        var dataAgendada = time.GetUtcNow().UtcDateTime.AddDays(7);

        // O tempo avança 30 dias: a data agendada agora está no passado.
        time.Advance(TimeSpan.FromDays(30));
        var agoraDepois = time.GetUtcNow().UtcDateTime;

        var r = assinatura.AgendarProximaCobranca(dataAgendada, agoraDepois);

        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A data da próxima cobrança deve ser futura.");
    }

    [Fact]
    public void Pagamento_Pix_ExpiracaoDerivadaDoTempoControlado_FicaNoPassadoAposAvancar()
    {
        var time = new FakeTimeProvider(Instante);
        var agora = time.GetUtcNow().UtcDateTime;

        var pagamento = Pagamento.Criar(Guid.NewGuid(), 99m, agora).Value;
        var expiracao = agora.AddHours(1);
        pagamento.DefinirDadosPix("pi_pix", "qr", "url", expiracao, agora);

        pagamento.PixExpiracao.Should().Be(Instante.UtcDateTime.AddHours(1));

        // Avança 2 horas: o Pix já expirou em relação ao novo "agora".
        time.Advance(TimeSpan.FromHours(2));
        pagamento.PixExpiracao.Should().BeBefore(time.GetUtcNow().UtcDateTime);
    }
}
