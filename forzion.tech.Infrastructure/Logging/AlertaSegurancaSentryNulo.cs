using forzion.tech.Application.Interfaces;

namespace forzion.tech.Infrastructure.Logging;

public sealed class AlertaSegurancaSentryNulo : IAlertaSegurancaSentry
{
    public void Registrar(string sinal, string mensagem, IReadOnlyDictionary<string, string> tags)
    {
    }
}
