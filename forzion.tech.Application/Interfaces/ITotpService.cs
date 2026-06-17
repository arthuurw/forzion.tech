namespace forzion.tech.Application.Interfaces;

public readonly record struct TotpVerificacao(bool Valido, long TimeStep);

public interface ITotpService
{
    string GerarSecret();

    string GerarUri(string secretBase32, string contaLabel, string issuer);

    TotpVerificacao Verificar(string secretBase32, string codigo, long? ultimoTimeStep);
}
