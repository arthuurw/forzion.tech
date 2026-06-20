using FluentAssertions;
using forzion.tech.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;
using Moq;

namespace forzion.tech.Tests.Api.Configuration;

public class BearerSecuritySchemeTransformerTests
{
    private static IAuthenticationSchemeProvider ProviderCom(params string[] nomes)
    {
        var schemes = nomes
            .Select(n => new AuthenticationScheme(n, n, typeof(IAuthenticationHandler)))
            .ToList();
        var mock = new Mock<IAuthenticationSchemeProvider>();
        mock.Setup(p => p.GetAllSchemesAsync()).ReturnsAsync(schemes);
        return mock.Object;
    }

    private static OpenApiDocument DocComOperacao()
    {
        return new OpenApiDocument
        {
            Paths = new OpenApiPaths
            {
                ["/teste"] = new OpenApiPathItem
                {
                    Operations = new Dictionary<HttpMethod, OpenApiOperation>
                    {
                        [HttpMethod.Get] = new OpenApiOperation(),
                    },
                },
            },
        };
    }

    [Fact]
    public async Task TransformAsync_SchemeBearerPresente_AdicionaSecuritySchemeEExigeEmCadaOperacao()
    {
        var transformer = new BearerSecuritySchemeTransformer(ProviderCom("Bearer", "Cookies"));
        var doc = DocComOperacao();

        await transformer.TransformAsync(doc, null!, CancellationToken.None);

        doc.Components!.SecuritySchemes.Should().ContainKey("Bearer");
        doc.Components.SecuritySchemes!["Bearer"].Scheme.Should().Be("bearer");
        doc.Components.SecuritySchemes["Bearer"].BearerFormat.Should().Be("JWT");

        var operacao = doc.Paths["/teste"].Operations![HttpMethod.Get];
        operacao.Security.Should().ContainSingle();
    }

    [Fact]
    public async Task TransformAsync_SemSchemeBearer_NaoTocaDocumento()
    {
        var transformer = new BearerSecuritySchemeTransformer(ProviderCom("Cookies"));
        var doc = DocComOperacao();

        await transformer.TransformAsync(doc, null!, CancellationToken.None);

        doc.Components.Should().BeNull();
        doc.Paths["/teste"].Operations![HttpMethod.Get].Security.Should().BeNullOrEmpty();
    }
}
