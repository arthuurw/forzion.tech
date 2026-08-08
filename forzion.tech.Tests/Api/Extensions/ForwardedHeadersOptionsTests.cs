using FluentAssertions;
using forzion.tech.Api.Configuration;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Api.Extensions;

public class ForwardedHeadersOptionsTests
{
    private static IHostEnvironment Env(string name) =>
        Mock.Of<IHostEnvironment>(e => e.EnvironmentName == name);

    private static IConfiguration Config(string? knownNetworks)
    {
        var dict = new Dictionary<string, string?>();
        if (knownNetworks is not null)
            dict["ForwardedHeaders:KnownNetworks"] = knownNetworks;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void BuildForwardedHeadersOptions_ComCidrsConfigurados_AdicionaCadaRedeEmKnownIPNetworks()
    {
        var options = Config("172.18.0.0/16, 10.20.0.0/24").BuildForwardedHeadersOptions(Env("Production"));

        options.KnownIPNetworks.Should().Contain(System.Net.IPNetwork.Parse("172.18.0.0/16"));
        options.KnownIPNetworks.Should().Contain(System.Net.IPNetwork.Parse("10.20.0.0/24"));
    }

    [Fact]
    public void BuildForwardedHeadersOptions_ComCidrsConfigurados_MantemForwardLimitUm()
    {
        var options = Config("172.18.0.0/16").BuildForwardedHeadersOptions(Env("Production"));

        options.ForwardLimit.Should().Be(1);
        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
    }

    [Theory]
    [InlineData("Production", null)]
    [InlineData("Production", "")]
    [InlineData("Production", " , ")]
    [InlineData("Homolog", null)]
    [InlineData("Homolog", "")]
    [InlineData("Homolog", " , ")]
    public void BuildForwardedHeadersOptions_SemRedeConfigurada_LancaEmProducaoEHomolog(
        string environmentName, string? knownNetworks)
    {
        var act = () => Config(knownNetworks).BuildForwardedHeadersOptions(Env(environmentName));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ForwardedHeaders__KnownNetworks*");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public void BuildForwardedHeadersOptions_SemRedeConfigurada_NaoLancaForaDeProducaoEHomolog(string environmentName)
    {
        var options = Config(null).BuildForwardedHeadersOptions(Env(environmentName));

        options.ForwardLimit.Should().Be(1);
        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
    }

    [Fact]
    public void BuildForwardedHeadersOptions_CidrMalformado_LancaComAEntradaOfensoraNaMensagem()
    {
        var act = () => Config("172.18.0.0/16,nao-e-cidr").BuildForwardedHeadersOptions(Env("Production"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*nao-e-cidr*");
    }
}
