using FluentAssertions;
using forzion.tech.Application.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

public class InternalSettingsTests
{
    [Fact]
    public void Production_SemChave_FalhaFechado()
    {
        using var sp = InfraHarness.BuildProvider("Production");

        var act = () => _ = sp.GetRequiredService<IOptions<InternalSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Production_ComChave_Sobe()
    {
        using var sp = InfraHarness.BuildProvider("Production",
            new Dictionary<string, string?> { ["Internal:ApiKey"] = "prod-internal-key" });

        sp.GetRequiredService<IOptions<InternalSettings>>().Value
            .ApiKey.Should().Be("prod-internal-key");
    }

    [Fact]
    public void NaoProducao_SemChave_Sobe()
    {
        using var sp = InfraHarness.BuildProvider("Development");

        sp.GetRequiredService<IOptions<InternalSettings>>().Value
            .ApiKey.Should().BeEmpty();
    }
}
