using System.Diagnostics.Metrics;

namespace forzion.tech.AI.Observability;

public sealed class ForzionAiMetrics : IDisposable
{
    public const string MeterName = "forzion.tech.ai";

    private readonly Meter _meter;

    // Segue semântica OpenTelemetry gen_ai para compatibilidade com dashboards padrão
    public readonly Counter<long> TokensUsed;
    public readonly Histogram<double> OperationDurationMs;

    // Métricas de negócio específicas
    public readonly Counter<long> InjectionDetected;
    public readonly Counter<long> EmptyReplies;
    public readonly Counter<long> AgentErrors;
    public readonly Counter<long> BudgetExceeded;

    public ForzionAiMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        TokensUsed = _meter.CreateCounter<long>(
            "gen_ai.client.token.usage",
            unit: "{token}",
            description: "Total tokens consumed per agent call (input + output)");

        OperationDurationMs = _meter.CreateHistogram<double>(
            "gen_ai.client.operation.duration",
            unit: "ms",
            description: "End-to-end latency of an agent chat call");

        InjectionDetected = _meter.CreateCounter<long>(
            "forzion.ai.injection.detected",
            unit: "{event}",
            description: "Prompt injection patterns detected in user input");

        EmptyReplies = _meter.CreateCounter<long>(
            "forzion.ai.reply.empty",
            unit: "{event}",
            description: "Agent calls that produced an empty response");

        AgentErrors = _meter.CreateCounter<long>(
            "forzion.ai.agent.error",
            unit: "{event}",
            description: "Agent calls that ended in error or timeout");

        BudgetExceeded = _meter.CreateCounter<long>(
            "forzion.ai.budget.exceeded",
            unit: "{event}",
            description: "Requests rejected because the daily token budget was exhausted");
    }

    public void Dispose() => _meter.Dispose();
}
