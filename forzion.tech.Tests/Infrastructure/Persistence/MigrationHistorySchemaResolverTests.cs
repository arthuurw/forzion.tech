using FluentAssertions;
using forzion.tech.Infrastructure.Persistence;

namespace forzion.tech.Tests.Infrastructure.Persistence;

public class MigrationHistorySchemaResolverTests
{
    [Fact]
    public void Resolve_SearchPathUnico_RetornaOSchema() =>
        MigrationHistorySchemaResolver.Resolve("Host=h;Database=d;Username=u;Password=p;Search Path=homolog")
            .Should().Be("homolog");

    [Fact]
    public void Resolve_SearchPathMultiplo_RetornaOPrimeiro() =>
        MigrationHistorySchemaResolver.Resolve("Host=h;Database=d;Username=u;Password=p;Search Path=homolog,public")
            .Should().Be("homolog");

    [Fact]
    public void Resolve_SemSearchPath_RetornaNull() =>
        MigrationHistorySchemaResolver.Resolve("Host=h;Database=d;Username=u;Password=p")
            .Should().BeNull();

    [Fact]
    public void Resolve_SearchPathVazio_RetornaNull() =>
        MigrationHistorySchemaResolver.Resolve("Host=h;Database=d;Username=u;Password=p;Search Path=")
            .Should().BeNull();
}
