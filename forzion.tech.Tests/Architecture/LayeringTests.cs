using System.Reflection;
using forzion.tech.Api.Middleware;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Entities;
using forzion.tech.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace forzion.tech.Tests.Architecture;

/// <summary>
/// Trava a direcao de dependencia da clean architecture com testes executaveis.
/// Domain nao referencia nenhuma camada externa; Application so depende de Domain;
/// Infrastructure/Api nao sao referenciadas por dentro do Domain; EF Core fica fora do Domain.
/// </summary>
public class LayeringTests
{
    private const string DomainNamespace = "forzion.tech.Domain";
    private const string ApplicationNamespace = "forzion.tech.Application";
    private const string InfrastructureNamespace = "forzion.tech.Infrastructure";
    private const string ApiNamespace = "forzion.tech.Api";

    private static readonly Assembly DomainAssembly = typeof(Aluno).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Result).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(GlobalExceptionHandler).Assembly;

    private static string FormatarFalhas(TestResult resultado) =>
        resultado.FailingTypeNames is null
            ? string.Empty
            : string.Join(", ", resultado.FailingTypeNames);

    [Fact]
    public void Domain_NaoDeveDependerDeApplication()
    {
        var resultado = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        Assert.True(resultado.IsSuccessful, $"Domain depende de Application: {FormatarFalhas(resultado)}");
    }

    [Fact]
    public void Domain_NaoDeveDependerDeInfrastructure()
    {
        var resultado = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(resultado.IsSuccessful, $"Domain depende de Infrastructure: {FormatarFalhas(resultado)}");
    }

    [Fact]
    public void Domain_NaoDeveDependerDeApi()
    {
        var resultado = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(resultado.IsSuccessful, $"Domain depende de Api: {FormatarFalhas(resultado)}");
    }

    [Fact]
    public void Domain_NaoDeveDependerDeEntityFrameworkCore()
    {
        var resultado = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Abstractions")
            .GetResult();

        Assert.True(resultado.IsSuccessful, $"Domain depende de EF Core: {FormatarFalhas(resultado)}");
    }

    [Fact]
    public void Application_NaoDeveDependerDeInfrastructure()
    {
        var resultado = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(resultado.IsSuccessful, $"Application depende de Infrastructure: {FormatarFalhas(resultado)}");
    }

    [Fact]
    public void Application_NaoDeveDependerDeApi()
    {
        var resultado = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(resultado.IsSuccessful, $"Application depende de Api: {FormatarFalhas(resultado)}");
    }

    [Fact]
    public void Application_NaoDeveDependerDeEntityFrameworkCore()
    {
        var resultado = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Abstractions")
            .GetResult();

        Assert.True(resultado.IsSuccessful, $"Application depende de EF Core: {FormatarFalhas(resultado)}");
    }

    [Fact]
    public void Infrastructure_NaoDeveDependerDeApi()
    {
        var resultado = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(resultado.IsSuccessful, $"Infrastructure depende de Api: {FormatarFalhas(resultado)}");
    }
}
