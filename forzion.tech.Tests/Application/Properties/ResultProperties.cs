using CsCheck;
using FluentAssertions;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Tests.Application.Properties;

/// <summary>
/// Properties algebricas observaveis de <see cref="Result"/>/<see cref="Result{T}"/>.
///
/// Observacao: o tipo de producao NAO expoe Map/Bind (combinadores monadicos). As leis
/// abaixo cobrem as propriedades observaveis disponiveis (sucesso/falha, dualidade
/// IsSuccess/IsFailure, round-trip de Value, acesso a Value em falha lancando). Map/bind
/// foram pedidos no harness mas inexistem na API atual — documentado no relatorio, sem
/// alterar codigo de producao.
/// </summary>
public class ResultProperties
{
    [Fact]
    public void Success_QualquerValor_PreservaValorEMarcaSucesso()
    {
        Gen.Int.Sample(valor =>
        {
            var result = Result.Success(valor);

            result.IsSuccess.Should().BeTrue();
            result.IsFailure.Should().BeFalse();
            result.Error.Should().BeNull();
            result.Value.Should().Be(valor);
        });
    }

    [Fact]
    public void Failure_QualquerErro_PropagaErroEMarcaFalha()
    {
        Gen.String[Gen.Char.AlphaNumeric, 1, 20].Sample(mensagem =>
        {
            var erro = Error.Business(mensagem);
            var result = Result.Failure<int>(erro);

            result.IsSuccess.Should().BeFalse();
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be(erro);
        });
    }

    [Fact]
    public void IsSuccess_E_IsFailure_SaoSempreOpostos()
    {
        var genResult =
            from sucesso in Gen.Bool
            from valor in Gen.Int
            from msg in Gen.String[Gen.Char.AlphaNumeric, 1, 10]
            select sucesso
                ? Result.Success(valor)
                : Result.Failure<int>(Error.Business(msg));

        genResult.Sample(result =>
            result.IsSuccess.Should().Be(!result.IsFailure));
    }

    [Fact]
    public void Value_EmFalha_LancaInvalidOperationException()
    {
        Gen.String[Gen.Char.AlphaNumeric, 1, 20].Sample(mensagem =>
        {
            var result = Result.Failure<string>(Error.Business(mensagem));

            var act = () => result.Value;
            act.Should().Throw<InvalidOperationException>();
        });
    }

    [Fact]
    public void Success_RoundTrip_ValueIgualAoOriginal()
    {
        Gen.String.Sample(valor =>
        {
            var result = Result.Success(valor);
            result.Value.Should().Be(valor);
        });
    }

    [Fact]
    public void Error_Business_CodigoFixoEmensagemPreservada()
    {
        Gen.String[Gen.Char.AlphaNumeric, 0, 50].Sample(mensagem =>
        {
            var erro = Error.Business(mensagem);
            erro.Code.Should().Be("business_error");
            erro.Message.Should().Be(mensagem);
        });
    }
}
