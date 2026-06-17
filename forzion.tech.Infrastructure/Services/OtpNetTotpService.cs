using forzion.tech.Application.Interfaces;
using OtpNet;

namespace forzion.tech.Infrastructure.Services;

public sealed class OtpNetTotpService : ITotpService
{
    public string GerarSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public string GerarUri(string secretBase32, string contaLabel, string issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretBase32);
        ArgumentException.ThrowIfNullOrWhiteSpace(contaLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);

        return new OtpUri(OtpType.Totp, secretBase32, contaLabel, issuer: issuer).ToString();
    }

    public TotpVerificacao Verificar(string secretBase32, string codigo, long? ultimoTimeStep)
    {
        if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(codigo))
            return new TotpVerificacao(false, 0);

        byte[] secret;
        try
        {
            secret = Base32Encoding.ToBytes(secretBase32);
        }
        catch (ArgumentException)
        {
            return new TotpVerificacao(false, 0);
        }

        var totp = new Totp(secret);
        var valido = totp.VerifyTotp(codigo, out var timeStep, VerificationWindow.RfcSpecifiedNetworkDelay);

        if (!valido)
            return new TotpVerificacao(false, 0);

        if (ultimoTimeStep.HasValue && timeStep <= ultimoTimeStep.Value)
            return new TotpVerificacao(false, timeStep);

        return new TotpVerificacao(true, timeStep);
    }
}
