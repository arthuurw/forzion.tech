using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
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
        AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 199m, agora);

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

        // Data agendada 7 dias à frente do instante inicial.
        var dataAgendada = time.GetUtcNow().UtcDateTime.AddDays(7);

        // O tempo avança 30 dias: a data agendada agora está no passado.
        time.Advance(TimeSpan.FromDays(30));
        var agoraDepois = time.GetUtcNow().UtcDateTime;

        var act = () => assinatura.AgendarProximaCobranca(dataAgendada, agoraDepois);

        act.Should().Throw<DomainException>()
            .WithMessage("A data da próxima cobrança deve ser futura.");
    }

    [Fact]
    public void Pagamento_Pix_ExpiracaoDerivadaDoTempoControlado_FicaNoPassadoAposAvancar()
    {
        var time = new FakeTimeProvider(Instante);
        var agora = time.GetUtcNow().UtcDateTime;

        var pagamento = Pagamento.Criar(Guid.NewGuid(), 99m, agora);
        // Expiração do Pix: 1 hora a partir do instante controlado.
        var expiracao = agora.AddHours(1);
        pagamento.DefinirDadosPix("pi_pix", "qr", "url", expiracao);

        pagamento.PixExpiracao.Should().Be(Instante.UtcDateTime.AddHours(1));

        // Avança 2 horas: o Pix já expirou em relação ao novo "agora".
        time.Advance(TimeSpan.FromHours(2));
        pagamento.PixExpiracao.Should().BeBefore(time.GetUtcNow().UtcDateTime);
    }
}
