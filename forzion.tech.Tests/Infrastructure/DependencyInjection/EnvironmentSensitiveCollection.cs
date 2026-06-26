namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

[CollectionDefinition(Name, DisableParallelization = true)]
public class EnvironmentSensitiveCollection
{
    public const string Name = "EnvironmentSensitive";
}
