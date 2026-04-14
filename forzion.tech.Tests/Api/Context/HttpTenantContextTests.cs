using FluentAssertions;
using forzion.tech.Api.Context;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace forzion.tech.Tests.Api.Context;

public class HttpTenantContextTests
{
    [Fact]
    public void Constructor_AccessorNulo_LancaArgumentNullException()
    {
        var act = () => new HttpTenantContext(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TenantId_ClaimTenantIdValido_RetornaGuid()
    {
        var tenantId = Guid.NewGuid();
        var context = CriarContextComClaim("tenant_id", tenantId.ToString());
        var accessor = CriarAccessor(context);

        var sut = new HttpTenantContext(accessor);

        sut.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void TenantId_SemClaimTenantId_RetornaNull()
    {
        var context = CriarContextComClaim("outro_claim", "valor");
        var accessor = CriarAccessor(context);

        var sut = new HttpTenantContext(accessor);

        sut.TenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_ClaimTenantIdInvalido_RetornaNull()
    {
        var context = CriarContextComClaim("tenant_id", "nao-e-um-guid");
        var accessor = CriarAccessor(context);

        var sut = new HttpTenantContext(accessor);

        sut.TenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_HttpContextNulo_RetornaNull()
    {
        var accessor = Mock.Of<IHttpContextAccessor>(a => a.HttpContext == null);

        var sut = new HttpTenantContext(accessor);

        sut.TenantId.Should().BeNull();
    }

    private static HttpContext CriarContextComClaim(string tipo, string valor)
    {
        var claims = new[] { new Claim(tipo, valor) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var context = new DefaultHttpContext { User = principal };
        return context;
    }

    private static IHttpContextAccessor CriarAccessor(HttpContext context)
        => Mock.Of<IHttpContextAccessor>(a => a.HttpContext == context);
}
