namespace forzion.tech.Application.Interfaces;

public interface IMfaSecretProtector
{
    string Proteger(string textoPuro);

    string Revelar(string textoProtegido);
}
