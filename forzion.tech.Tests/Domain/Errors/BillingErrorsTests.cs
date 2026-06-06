using FluentAssertions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Tests.Domain.Errors;

public class AssinaturaTreinadorErrorsTests
{
    [Theory]
    [InlineData(nameof(AssinaturaTreinadorErrors.TreinadorIdInvalido), "assinatura_treinador.treinador_id_invalido")]
    [InlineData(nameof(AssinaturaTreinadorErrors.PlanoIdInvalido), "assinatura_treinador.plano_id_invalido")]
    [InlineData(nameof(AssinaturaTreinadorErrors.ValorInvalido), "assinatura_treinador.valor_invalido")]
    [InlineData(nameof(AssinaturaTreinadorErrors.CanceladaNaoAtivavel), "assinatura_treinador.cancelada_nao_ativavel")]
    [InlineData(nameof(AssinaturaTreinadorErrors.ApenasAtivasInadimplentes), "assinatura_treinador.apenas_ativas_inadimplentes")]
    [InlineData(nameof(AssinaturaTreinadorErrors.JaCancelada), "assinatura_treinador.ja_cancelada")]
    [InlineData(nameof(AssinaturaTreinadorErrors.ProximaCobrancaNaoFutura), "assinatura_treinador.proxima_cobranca_nao_futura")]
    [InlineData(nameof(AssinaturaTreinadorErrors.InadimplenteDeveUsarRegularizacao), "assinatura_treinador.inadimplente_deve_usar_regularizacao")]
    [InlineData(nameof(AssinaturaTreinadorErrors.TrocaPlanoEstadoInvalido), "assinatura_treinador.troca_plano_estado_invalido")]
    [InlineData(nameof(AssinaturaTreinadorErrors.PlanoAgendadoIdInvalido), "assinatura_treinador.plano_agendado_id_invalido")]
    public void TodosOsErros_TemCodigoEsperado(string propriedade, string codigoEsperado)
    {
        var error = propriedade switch
        {
            nameof(AssinaturaTreinadorErrors.TreinadorIdInvalido) => AssinaturaTreinadorErrors.TreinadorIdInvalido,
            nameof(AssinaturaTreinadorErrors.PlanoIdInvalido) => AssinaturaTreinadorErrors.PlanoIdInvalido,
            nameof(AssinaturaTreinadorErrors.ValorInvalido) => AssinaturaTreinadorErrors.ValorInvalido,
            nameof(AssinaturaTreinadorErrors.CanceladaNaoAtivavel) => AssinaturaTreinadorErrors.CanceladaNaoAtivavel,
            nameof(AssinaturaTreinadorErrors.ApenasAtivasInadimplentes) => AssinaturaTreinadorErrors.ApenasAtivasInadimplentes,
            nameof(AssinaturaTreinadorErrors.JaCancelada) => AssinaturaTreinadorErrors.JaCancelada,
            nameof(AssinaturaTreinadorErrors.ProximaCobrancaNaoFutura) => AssinaturaTreinadorErrors.ProximaCobrancaNaoFutura,
            nameof(AssinaturaTreinadorErrors.InadimplenteDeveUsarRegularizacao) => AssinaturaTreinadorErrors.InadimplenteDeveUsarRegularizacao,
            nameof(AssinaturaTreinadorErrors.TrocaPlanoEstadoInvalido) => AssinaturaTreinadorErrors.TrocaPlanoEstadoInvalido,
            nameof(AssinaturaTreinadorErrors.PlanoAgendadoIdInvalido) => AssinaturaTreinadorErrors.PlanoAgendadoIdInvalido,
            _ => throw new ArgumentOutOfRangeException(nameof(propriedade))
        };

        error.Code.Should().Be(codigoEsperado);
        error.Message.Should().NotBeNullOrWhiteSpace();
    }
}

public class PagamentoTreinadorErrorsTests
{
    [Theory]
    [InlineData(nameof(PagamentoTreinadorErrors.TreinadorIdInvalido), "pagamento_treinador.treinador_id_invalido")]
    [InlineData(nameof(PagamentoTreinadorErrors.AssinaturaIdInvalido), "pagamento_treinador.assinatura_id_invalido")]
    [InlineData(nameof(PagamentoTreinadorErrors.ValorInvalido), "pagamento_treinador.valor_invalido")]
    [InlineData(nameof(PagamentoTreinadorErrors.PaymentIntentIdInvalido), "pagamento_treinador.payment_intent_id_invalido")]
    [InlineData(nameof(PagamentoTreinadorErrors.QrCodeInvalido), "pagamento_treinador.qr_code_invalido")]
    [InlineData(nameof(PagamentoTreinadorErrors.ClientSecretInvalido), "pagamento_treinador.client_secret_invalido")]
    [InlineData(nameof(PagamentoTreinadorErrors.ApenasPendentesPagos), "pagamento_treinador.apenas_pendentes_pagos")]
    [InlineData(nameof(PagamentoTreinadorErrors.ApenasPendentesFalhou), "pagamento_treinador.apenas_pendentes_falhou")]
    [InlineData(nameof(PagamentoTreinadorErrors.ApenasPendentesExpirados), "pagamento_treinador.apenas_pendentes_expirados")]
    public void TodosOsErros_TemCodigoEsperado(string propriedade, string codigoEsperado)
    {
        var error = propriedade switch
        {
            nameof(PagamentoTreinadorErrors.TreinadorIdInvalido) => PagamentoTreinadorErrors.TreinadorIdInvalido,
            nameof(PagamentoTreinadorErrors.AssinaturaIdInvalido) => PagamentoTreinadorErrors.AssinaturaIdInvalido,
            nameof(PagamentoTreinadorErrors.ValorInvalido) => PagamentoTreinadorErrors.ValorInvalido,
            nameof(PagamentoTreinadorErrors.PaymentIntentIdInvalido) => PagamentoTreinadorErrors.PaymentIntentIdInvalido,
            nameof(PagamentoTreinadorErrors.QrCodeInvalido) => PagamentoTreinadorErrors.QrCodeInvalido,
            nameof(PagamentoTreinadorErrors.ClientSecretInvalido) => PagamentoTreinadorErrors.ClientSecretInvalido,
            nameof(PagamentoTreinadorErrors.ApenasPendentesPagos) => PagamentoTreinadorErrors.ApenasPendentesPagos,
            nameof(PagamentoTreinadorErrors.ApenasPendentesFalhou) => PagamentoTreinadorErrors.ApenasPendentesFalhou,
            nameof(PagamentoTreinadorErrors.ApenasPendentesExpirados) => PagamentoTreinadorErrors.ApenasPendentesExpirados,
            _ => throw new ArgumentOutOfRangeException(nameof(propriedade))
        };

        error.Code.Should().Be(codigoEsperado);
        error.Message.Should().NotBeNullOrWhiteSpace();
    }
}
