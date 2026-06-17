using FluentAssertions;
using forzion.tech.Infrastructure.Services;
using OtpNet;

namespace forzion.tech.Tests.Infrastructure.Services;

public class OtpNetTotpServiceTests
{
    private readonly OtpNetTotpService _service = new();

    [Fact]
    public void GerarSecret_RetornaBase32Valido()
    {
        var secret = _service.GerarSecret();

        secret.Should().NotBeNullOrWhiteSpace();
        var act = () => Base32Encoding.ToBytes(secret);
        act.Should().NotThrow();
    }

    [Fact]
    public void GerarUri_RetornaOtpauthTotp()
    {
        var secret = _service.GerarSecret();

        var uri = _service.GerarUri(secret, "user@test.com", "forzion.tech");

        uri.Should().StartWith("otpauth://totp/");
        uri.Should().Contain("issuer=forzion.tech");
    }

    [Fact]
    public void Verificar_CodigoCorreto_Valido()
    {
        var secret = _service.GerarSecret();
        var codigo = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

        var r = _service.Verificar(secret, codigo, ultimoTimeStep: null);

        r.Valido.Should().BeTrue();
        r.TimeStep.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Verificar_CodigoIncorreto_Invalido()
    {
        var secret = _service.GerarSecret();

        _service.Verificar(secret, "000001", ultimoTimeStep: null).Valido.Should().BeFalse();
    }

    [Fact]
    public void Verificar_Replay_Invalido()
    {
        var secret = _service.GerarSecret();
        var codigo = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

        var primeiro = _service.Verificar(secret, codigo, ultimoTimeStep: null);
        primeiro.Valido.Should().BeTrue();

        var replay = _service.Verificar(secret, codigo, ultimoTimeStep: primeiro.TimeStep);
        replay.Valido.Should().BeFalse();
    }
}
