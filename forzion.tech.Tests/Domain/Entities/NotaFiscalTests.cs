using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;

namespace forzion.tech.Tests.Domain.Entities;

public class NotaFiscalTests
{
    private static readonly DateTime Agora = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static NotaFiscal Assinatura(Guid? pagamentoId = null)
        => NotaFiscal.CriarAssinatura(Guid.NewGuid(), pagamentoId ?? Guid.NewGuid(), 99.90m, Agora).Value;

    [Fact]
    public void CriarAssinatura_DadosValidos_NascePendente()
    {
        var treinadorId = Guid.NewGuid();
        var pagamentoId = Guid.NewGuid();

        var result = NotaFiscal.CriarAssinatura(treinadorId, pagamentoId, 99.90m, Agora);

        result.IsSuccess.Should().BeTrue();
        var nf = result.Value;
        nf.Status.Should().Be(NotaFiscalStatus.Pendente);
        nf.Tipo.Should().Be(TipoNotaFiscal.AssinaturaSaaS);
        nf.PagamentoTreinadorId.Should().Be(pagamentoId);
        nf.CompetenciaInicio.Should().BeNull();
        nf.NumeroDps.Should().Be($"AS-{pagamentoId}");
    }

    [Theory]
    [InlineData(false, true, 10)]
    [InlineData(true, false, 10)]
    [InlineData(true, true, 0)]
    [InlineData(true, true, -5)]
    public void CriarAssinatura_DadosInvalidos_Falha(bool treinadorOk, bool pagamentoOk, decimal valor)
    {
        var result = NotaFiscal.CriarAssinatura(
            treinadorOk ? Guid.NewGuid() : Guid.Empty,
            pagamentoOk ? Guid.NewGuid() : Guid.Empty,
            valor, Agora);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CriarComissao_DadosValidos_NasceComCompetencia()
    {
        var treinadorId = Guid.NewGuid();
        var inicio = new DateOnly(2026, 5, 1);
        var fim = new DateOnly(2026, 5, 31);

        var nf = NotaFiscal.CriarComissao(treinadorId, inicio, fim, 12.34m, Agora).Value;

        nf.Tipo.Should().Be(TipoNotaFiscal.ComissaoMarketplace);
        nf.CompetenciaInicio.Should().Be(inicio);
        nf.CompetenciaFim.Should().Be(fim);
        nf.PagamentoTreinadorId.Should().BeNull();
        nf.NumeroDps.Should().Be($"CM-{treinadorId}-202605");
    }

    [Fact]
    public void CriarComissao_CompetenciaFimAntesDeInicio_Falha()
    {
        var result = NotaFiscal.CriarComissao(
            Guid.NewGuid(), new DateOnly(2026, 5, 31), new DateOnly(2026, 5, 1), 10m, Agora);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void NumeroDpsEstavel_MesmaNota_Deterministico()
    {
        var pagamentoId = Guid.NewGuid();
        var a = NotaFiscal.CriarAssinatura(Guid.NewGuid(), pagamentoId, 10m, Agora).Value;
        var b = NotaFiscal.CriarAssinatura(Guid.NewGuid(), pagamentoId, 10m, Agora).Value;

        a.NumeroDpsEstavel().Should().Be(b.NumeroDpsEstavel());
    }

    [Fact]
    public void MarcarEmitida_DePendente_VaiParaEmitidaEEmiteEvento()
    {
        var nf = Assinatura();

        var result = nf.MarcarEmitida("CHAVE123", "1", Agora, "https://danfse/1", Agora);

        result.IsSuccess.Should().BeTrue();
        nf.Status.Should().Be(NotaFiscalStatus.Emitida);
        nf.ChaveAcesso.Should().Be("CHAVE123");
        nf.DanfseRef.Should().Be("https://danfse/1");
        nf.DomainEvents.OfType<NotaFiscalEmitidaEvent>().Should().ContainSingle()
            .Which.ChaveAcesso.Should().Be("CHAVE123");
    }

    [Fact]
    public void MarcarEmitida_DeErro_PermiteRetry()
    {
        var nf = Assinatura();
        nf.MarcarErro("E1", "rejeitado", Agora);

        nf.MarcarEmitida("CHAVE", "1", Agora, null, Agora).IsSuccess.Should().BeTrue();
        nf.Status.Should().Be(NotaFiscalStatus.Emitida);
        nf.CodigoErro.Should().BeNull();
    }

    [Fact]
    public void MarcarEmitida_SemChave_Falha()
    {
        Assinatura().MarcarEmitida(" ", "1", Agora, null, Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarEmitida_DeBloqueada_Falha()
    {
        var nf = Assinatura();
        nf.MarcarBloqueadaDadosFiscais(Agora);

        nf.MarcarEmitida("CHAVE", "1", Agora, null, Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarBloqueadaDadosFiscais_DePendente_Ok()
    {
        var nf = Assinatura();

        nf.MarcarBloqueadaDadosFiscais(Agora).IsSuccess.Should().BeTrue();
        nf.Status.Should().Be(NotaFiscalStatus.BloqueadaDadosFiscais);
    }

    [Fact]
    public void Cancelamento_FluxoCompleto_EmitidaSolicitadaCancelada()
    {
        var nf = Assinatura();
        nf.MarcarEmitida("CHAVE", "1", Agora, null, Agora);

        nf.SolicitarCancelamento(Agora).IsSuccess.Should().BeTrue();
        nf.Status.Should().Be(NotaFiscalStatus.CancelamentoSolicitado);

        nf.MarcarCancelada(Agora).IsSuccess.Should().BeTrue();
        nf.Status.Should().Be(NotaFiscalStatus.Cancelada);
    }

    [Fact]
    public void SolicitarCancelamento_DePendente_Falha()
    {
        Assinatura().SolicitarCancelamento(Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarCancelamentoExpirado_DeSolicitado_Ok()
    {
        var nf = Assinatura();
        nf.MarcarEmitida("CHAVE", "1", Agora, null, Agora);
        nf.SolicitarCancelamento(Agora);

        nf.MarcarCancelamentoExpirado(Agora).IsSuccess.Should().BeTrue();
        nf.Status.Should().Be(NotaFiscalStatus.CancelamentoExpirado);
    }

    [Fact]
    public void MarcarCancelada_SemSolicitacao_Falha()
    {
        var nf = Assinatura();
        nf.MarcarEmitida("CHAVE", "1", Agora, null, Agora);

        nf.MarcarCancelada(Agora).IsFailure.Should().BeTrue();
    }
}
