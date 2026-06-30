using FluentAssertions;
using forzion.tech.Application.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace forzion.tech.Tests.Infrastructure.DependencyInjection;

public class DeliveryLogSettingsTests
{
    [Fact]
    public void Production_SemChave_FalhaFechado()
    {
        using var sp = InfraHarness.BuildProvider("Production");

        var act = () => _ = sp.GetRequiredService<IOptions<DeliveryLogSettings>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Production_ComChave_Sobe()
    {
        using var sp = InfraHarness.BuildProvider("Production",
            new Dictionary<string, string?> { ["DeliveryLog:RecipientHashKey"] = "prod-key" });

        sp.GetRequiredService<IOptions<DeliveryLogSettings>>().Value
            .RecipientHashKey.Should().Be("prod-key");
    }

    [Fact]
    public void NaoProducao_SemChave_UsaDefaultDev()
    {
        using var sp = InfraHarness.BuildProvider("Development");

        sp.GetRequiredService<IOptions<DeliveryLogSettings>>().Value
            .RecipientHashKey.Should().Be(DeliveryLogSettings.DevDefaultKey);
    }
}
