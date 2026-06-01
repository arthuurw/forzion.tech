using System.Text.Json;
using System.Text.Json.Serialization;

namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public static class HealthReportPayload
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serializar(HealthReport report) => JsonSerializer.Serialize(report, Options);
}
