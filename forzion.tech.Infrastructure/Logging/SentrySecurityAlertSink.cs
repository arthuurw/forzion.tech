using forzion.tech.Application.Interfaces;
using Sentry;

namespace forzion.tech.Infrastructure.Logging;

// Captura explícita (fora do pipeline de ILogger) porque MinimumEventLevel do sink de erro fica
// em Error — um LogWarning normal nunca vira evento Sentry. SetBeforeSend (ScrubPii) roda pro
// hub inteiro, então esta captura herda o mesmo scrubbing de PII sem duplicar lógica.
public sealed class SentrySecurityAlertSink : IAlertaSegurancaSentry
{
    public void Registrar(string sinal, string mensagem, IReadOnlyDictionary<string, string> tags) =>
        SentrySdk.CaptureMessage(mensagem, scope =>
        {
            scope.Level = SentryLevel.Warning;
            scope.SetTag("security_signal", sinal);
            foreach (var (chave, valor) in tags)
                scope.SetTag(chave, valor);
        });
}
