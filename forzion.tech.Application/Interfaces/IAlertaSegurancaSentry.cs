namespace forzion.tech.Application.Interfaces;

public interface IAlertaSegurancaSentry
{
    void Registrar(string sinal, string mensagem, IReadOnlyDictionary<string, string> tags);
}
