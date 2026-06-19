using System.Reflection;
using FluentAssertions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Tests.Domain.Shared.Errors;

public class MensagensErroSemTermoInternoTests
{
    private static readonly Type[] ClassesDeErro = typeof(Error).Assembly.GetTypes()
        .Where(t => t.IsAbstract && t.IsSealed
            && t.Namespace == "forzion.tech.Domain.Shared.Errors"
            && t.Name.EndsWith("Errors", StringComparison.Ordinal))
        .ToArray();

    private static readonly string[] TermosInternos = ClassesDeErro
        .Select(t => t.Name[..^"Errors".Length])
        .Where(n => n.Skip(1).Any(char.IsUpper))
        .Append("RegistrarPagamentoRegularizado")
        .Distinct()
        .ToArray();

    public static IEnumerable<object[]> TodasAsMensagens()
    {
        foreach (var classe in ClassesDeErro)
        {
            var membros = classe.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(Error) && p.GetIndexParameters().Length == 0);
            foreach (var membro in membros)
            {
                var erro = (Error)membro.GetValue(null)!;
                yield return [$"{classe.Name}.{membro.Name}", erro.Message];
            }
        }
    }

    [Theory]
    [MemberData(nameof(TodasAsMensagens))]
    public void MensagemDeErro_NaoContemTermoInterno(string origem, string mensagem)
    {
        _ = origem;
        mensagem.Should().NotContainAny(TermosInternos);
    }
}
