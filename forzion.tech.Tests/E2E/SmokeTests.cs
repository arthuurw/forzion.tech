using System.Net;
using FluentAssertions;

namespace forzion.tech.Tests.E2E;

[Collection(E2ECollection.Name)]
[Trait("Category", "Integration")]
public class SmokeTests(RealPipelineFixture fixture)
{
    [Fact]
    public async Task Get_Health_Retorna200()
    {
        var response = await fixture.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_AuthPlanos_Retorna200ComPlanosSemeados()
    {
        var response = await fixture.CreateClient().GetAsync("/auth/planos");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Free");
    }
}
