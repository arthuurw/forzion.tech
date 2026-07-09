using System.Reflection;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Architecture;

/// <summary>
/// Ancora as contagens que as specs citam em prosa (DbSets, interfaces de repositorio) ao
/// repo real via reflection. Numeros em prosa driftam silenciosamente a cada migration/entidade;
/// este teste quebra quando a estrutura muda, forcando atualizar a spec junto do codigo.
/// Fonte de verdade dos numeros: estas constantes. Ao mudar, atualizar specs/specification-backend.md
/// (§2 repos, §5 DbSets) e specs/specification-db.md (§STACK & SCHEMAS).
/// </summary>
public class SpecInventoryTests
{
    private const int DbSetsEsperados = 42;
    private const int RepositoriosEsperados = 41;

    [Fact]
    public void AppDbContext_TemContagemDeDbSetsDocumentada()
    {
        var dbSets = typeof(AppDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Count(p => p.PropertyType.IsGenericType
                && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

        Assert.True(dbSets == DbSetsEsperados,
            $"DbSets reais={dbSets}, esperado={DbSetsEsperados}. Atualizar a constante + "
            + "specs/specification-backend.md §5 e specs/specification-db.md §STACK & SCHEMAS.");
    }

    [Fact]
    public void Application_TemContagemDeRepositoriosDocumentada()
    {
        var repos = typeof(IContaRepository).Assembly
            .GetTypes()
            .Count(t => t.IsInterface
                && t.Namespace == "forzion.tech.Application.Interfaces.Repositories");

        Assert.True(repos == RepositoriosEsperados,
            $"Interfaces de repositorio reais={repos}, esperado={RepositoriosEsperados}. "
            + "Atualizar a constante + a lista em specs/specification-backend.md §2.");
    }
}
