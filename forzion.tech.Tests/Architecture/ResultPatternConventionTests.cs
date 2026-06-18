using System.Text.RegularExpressions;
using FluentAssertions;

namespace forzion.tech.Tests.Architecture;

public class ResultPatternConventionTests
{
    private static readonly string SolutionRoot = LocalizarRaiz();

    private static string LocalizarRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "forzion.tech.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException($"forzion.tech.slnx não encontrado a partir de {AppContext.BaseDirectory}");
    }

    private static IEnumerable<string> ArquivosCs(params string[] segmentos)
    {
        var raiz = Path.Combine(new[] { SolutionRoot }.Concat(segmentos).ToArray());
        return Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }

    [Fact]
    public void Application_NaoLancaDomainExceptionCru()
    {
        var ofensores = ArquivosCs("forzion.tech.Application", "UseCases")
            .Where(p => Regex.IsMatch(File.ReadAllText(p), @"throw\s+new\s+DomainException\s*\("))
            .Select(p => Path.GetFileName(p))
            .ToList();

        ofensores.Should().BeEmpty(
            "invariante quebrada usa EstadoInconsistenteException (500), nunca DomainException cru (422)");
    }

    [Fact]
    public void Producao_NaoInstanciaErrorDiretamente()
    {
        var ofensores = ArquivosCs("forzion.tech.Application", "UseCases")
            .Concat(ArquivosCs("forzion.tech.Domain", "Shared", "Errors"))
            .Concat(ArquivosCs("forzion.tech.Domain", "Entities"))
            .Where(p => Regex.IsMatch(File.ReadAllText(p), @"new\s+Error\s*\("))
            .Select(p => Path.GetFileName(p))
            .ToList();

        ofensores.Should().BeEmpty(
            "Error sempre via factory (Validation/Conflict/NotFound/Business) com Type explícito");
    }

    [Fact]
    public void Catalogo_NaoUsaConstrutorImplicito()
    {
        var ofensores = ArquivosCs("forzion.tech.Domain", "Shared", "Errors")
            .Where(p => Regex.IsMatch(File.ReadAllText(p), @"=>\s*new\s*\("))
            .Select(p => Path.GetFileName(p))
            .ToList();

        ofensores.Should().BeEmpty(
            "entradas de catálogo usam factory com Type explícito, não new(...) que silenciosamente vira Business");
    }
}
