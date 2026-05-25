using FluentAssertions;
using forzion.tech.Api.Context;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace forzion.tech.Tests.Api.Context;

public class HttpUserContextTests
{
    private static IHttpContextAccessor CriarAccessor(params Claim[] claims)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(ctx);
        return accessor.Object;
    }

    private static IHttpContextAccessor CriarAccessorSemContexto()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        return accessor.Object;
    }

    // --- ContaId ---

    [Fact]
    public void ContaId_ClaimValida_RetornaGuid()
    {
        var id = Guid.NewGuid();
        var ctx = new HttpUserContext(CriarAccessor(new Claim("conta_id", id.ToString())));
        ctx.ContaId.Should().Be(id);
    }

    [Fact]
    public void ContaId_ClaimAusente_LancaAcessoNegadoException()
    {
        var ctx = new HttpUserContext(CriarAccessor());
        var act = () => ctx.ContaId;
        act.Should().Throw<AcessoNegadoException>();
    }

    // --- TipoConta ---

    [Fact]
    public void TipoConta_ClaimValida_RetornaEnum()
    {
        var ctx = new HttpUserContext(CriarAccessor(new Claim("tipo_conta", "Treinador")));
        ctx.TipoConta.Should().Be(TipoConta.Treinador);
    }

    [Fact]
    public void TipoConta_ClaimInvalida_LancaAcessoNegadoException()
    {
        var ctx = new HttpUserContext(CriarAccessor(new Claim("tipo_conta", "invalido")));
        var act = () => ctx.TipoConta;
        act.Should().Throw<AcessoNegadoException>();
    }

    [Fact]
    public void TipoConta_ClaimAusente_LancaAcessoNegadoException()
    {
        var ctx = new HttpUserContext(CriarAccessor());
        var act = () => ctx.TipoConta;
        act.Should().Throw<AcessoNegadoException>();
    }

    // --- PerfilId ---

    [Fact]
    public void PerfilId_ClaimValida_RetornaGuid()
    {
        var id = Guid.NewGuid();
        var ctx = new HttpUserContext(CriarAccessor(new Claim("perfil_id", id.ToString())));
        ctx.PerfilId.Should().Be(id);
    }

    [Fact]
    public void PerfilId_ClaimAusente_LancaAcessoNegadoException()
    {
        var ctx = new HttpUserContext(CriarAccessor());
        var act = () => ctx.PerfilId;
        act.Should().Throw<AcessoNegadoException>();
    }

    // --- Jti ---

    [Fact]
    public void Jti_ClaimAusente_LancaAcessoNegadoException()
    {
        var ctx = new HttpUserContext(CriarAccessor());
        var act = () => ctx.Jti;
        act.Should().Throw<AcessoNegadoException>();
    }

    // --- TokenExpiraEm ---

    [Fact]
    public void TokenExpiraEm_ClaimUnixValida_RetornaDataUtc()
    {
        var unix = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var ctx = new HttpUserContext(CriarAccessor(new Claim("exp", unix.ToString())));
        ctx.TokenExpiraEm.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void TokenExpiraEm_ClaimInvalida_RetornaDateTimeMinValue()
    {
        var ctx = new HttpUserContext(CriarAccessor(new Claim("exp", "nao-e-numero")));
        ctx.TokenExpiraEm.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void TokenExpiraEm_ClaimAusente_RetornaDateTimeMinValue()
    {
        var ctx = new HttpUserContext(CriarAccessor());
        ctx.TokenExpiraEm.Should().Be(DateTime.MinValue);
    }

    // --- Claim() com HttpContext nulo ---

    [Fact]
    public void ContaId_HttpContextNulo_LancaAcessoNegadoException()
    {
        var ctx = new HttpUserContext(CriarAccessorSemContexto());
        var act = () => ctx.ContaId;
        act.Should().Throw<AcessoNegadoException>();
    }
}
