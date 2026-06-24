using System.Text.Json;

namespace forzion.tech.Infrastructure.Outbox;

internal static class OutboxErroClassifier
{
    public static bool EhPermanente(Exception ex) => ex switch
    {
        JsonException => true,
        ArgumentException => true,
        _ => false,
    };
}
