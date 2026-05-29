using System.Reflection;
using System.Runtime.CompilerServices;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using NetArchTest.Rules;

namespace forzion.tech.Tests.Architecture;

/// <summary>
/// Convencoes de dominio e de camada: entidades imutaveis externamente (sem setter publico),
/// construidas apenas via factory estatica (sem construtor publico), e handlers seguindo
/// sufixo/namespace.
/// </summary>
public class ConventionTests
{
    private static readonly Assembly DomainAssembly = typeof(Aluno).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(LimiteTreinadorService).Assembly;

    // LogAprovacao usa a factory "Registrar" (audit log) em vez de "Criar" — excecao explicita,
    // documentada, em vez de afrouxar a regra para todas as entidades.
    private static readonly IReadOnlySet<string> FactoriesAlternativas = new HashSet<string>
    {
        "Registrar",
    };

    private static IEnumerable<Type> EntidadesDeDominio() =>
        DomainAssembly.GetTypes()
            .Where(t => t.IsClass
                && !t.IsAbstract
                && t.Namespace == "forzion.tech.Domain.Entities"
                && !t.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false));

    [Fact]
    public void EntidadesDeDominio_NaoDevemExporSettersPublicos()
    {
        var violacoes = EntidadesDeDominio()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true })
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        Assert.True(violacoes.Count == 0,
            $"Entidades com setter publico (devem ser private set): {string.Join(", ", violacoes)}");
    }

    [Fact]
    public void EntidadesDeDominio_NaoDevemExporConstrutorPublico()
    {
        var violacoes = EntidadesDeDominio()
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0)
            .Select(t => t.Name)
            .ToList();

        Assert.True(violacoes.Count == 0,
            $"Entidades com construtor publico (construcao deve ser via factory): {string.Join(", ", violacoes)}");
    }

    [Fact]
    public void EntidadesDeDominio_DevemTerConstrutorPrivadoSemParametros()
    {
        // O construtor sem parametros (usado pelo ORM e pelas factories) precisa existir e ser nao-publico.
        var violacoes = EntidadesDeDominio()
            .Where(t => t.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                Type.EmptyTypes,
                modifiers: null) is null)
            .Select(t => t.Name)
            .ToList();

        Assert.True(violacoes.Count == 0,
            $"Entidades sem construtor privado sem parametros: {string.Join(", ", violacoes)}");
    }

    [Fact]
    public void EntidadesDeDominio_DevemTerFactoryEstatica()
    {
        // Factory pode ser public (raiz de agregado) ou internal (entidade filha criada apenas
        // pelo agregado dono, dentro do proprio assembly de dominio).
        var violacoes = EntidadesDeDominio()
            .Where(t => !t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Any(m => (m.Name == "Criar" || FactoriesAlternativas.Contains(m.Name))
                    && m.ReturnType == t))
            .Select(t => t.Name)
            .ToList();

        Assert.True(violacoes.Count == 0,
            $"Entidades sem factory estatica (Criar/Registrar): {string.Join(", ", violacoes)}");
    }

    [Fact]
    public void Handlers_DevemResidirEmUseCases()
    {
        var resultado = Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("Handler")
            .And()
            .AreClasses()
            .Should()
            .ResideInNamespaceStartingWith("forzion.tech.Application.UseCases")
            .GetResult();

        var falhas = resultado.FailingTypeNames is null
            ? string.Empty
            : string.Join(", ", resultado.FailingTypeNames);

        Assert.True(resultado.IsSuccessful, $"Handlers fora de UseCases: {falhas}");
    }
}
