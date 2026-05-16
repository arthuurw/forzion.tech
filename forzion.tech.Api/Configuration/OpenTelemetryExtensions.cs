using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace forzion.tech.Api.Configuration;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddForzionOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("forzion.tech.api"))
            .WithTracing(t =>
            {
                t.AddSource("Microsoft.Agents.AI")
                 .AddSource("Microsoft.Extensions.AI")
                 .AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            })
            .WithMetrics(m =>
            {
                m.AddMeter("Microsoft.Agents.AI")
                 .AddMeter("Microsoft.Extensions.AI")
                 .AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    m.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });

        return services;
    }
}
