using FluentAssertions;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Tests.Domain.Shared.Errors;

public class MensagensErroSemTermoInternoTests
{
    public static IEnumerable<object[]> MensagensDisplayaveis()
    {
        yield return [AssinaturaAlunoErrors.CanceladaNaoAtivavel.Code, AssinaturaAlunoErrors.CanceladaNaoAtivavel.Message];
        yield return [AssinaturaAlunoErrors.InadimplenteDeveUsarRegularizacao.Code, AssinaturaAlunoErrors.InadimplenteDeveUsarRegularizacao.Message];
        yield return [AssinaturaAlunoErrors.NaoEncontrada.Code, AssinaturaAlunoErrors.NaoEncontrada.Message];
        yield return [AssinaturaAlunoErrors.CanceladaNaoCobravel.Code, AssinaturaAlunoErrors.CanceladaNaoCobravel.Message];
        yield return [AssinaturaTreinadorErrors.CanceladaNaoAtivavel.Code, AssinaturaTreinadorErrors.CanceladaNaoAtivavel.Message];
        yield return [AssinaturaTreinadorErrors.InadimplenteDeveUsarRegularizacao.Code, AssinaturaTreinadorErrors.InadimplenteDeveUsarRegularizacao.Message];
        yield return [AssinaturaTreinadorErrors.NaoEncontrada.Code, AssinaturaTreinadorErrors.NaoEncontrada.Message];
    }

    [Theory]
    [MemberData(nameof(MensagensDisplayaveis))]
    public void MensagemDeErro_NaoContemTermoInterno(string code, string mensagem)
    {
        _ = code;
        mensagem.Should().NotContainAny(
            "AssinaturaAluno", "AssinaturaTreinador", "RegistrarPagamentoRegularizado");
    }
}
