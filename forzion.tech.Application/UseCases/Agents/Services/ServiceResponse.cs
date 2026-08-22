namespace forzion.tech.Application.UseCases.Agents.Services;

public sealed record MoneyResponse(decimal Amount, string Currency);

public sealed record ServiceResponse(
    string Id,
    string Name,
    string? Category,
    string? Description,
    int? DurationMinutes,
    MoneyResponse Price,
    bool TrialAvailable);
