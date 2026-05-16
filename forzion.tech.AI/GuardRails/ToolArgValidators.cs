using System.Text.Json;
using System.Text.RegularExpressions;

namespace forzion.tech.AI.GuardRails;

public sealed record ValidationResult(bool Ok, string? Error = null)
{
    public static ValidationResult Success() => new(true);
    public static ValidationResult Fail(string reason) => new(false, reason);
}

public static class ToolArgValidators
{
    private static readonly Regex GuidPattern = new(
        @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.Compiled);

    public static ValidationResult ValidateGuidArg(JsonElement args, string propName, out Guid parsed)
    {
        parsed = Guid.Empty;

        if (!args.TryGetProperty(propName, out var el))
            return ValidationResult.Fail($"{propName} ausente");

        var raw = el.GetString() ?? "";

        if (!GuidPattern.IsMatch(raw))
            return ValidationResult.Fail($"{propName} em formato inválido");

        if (!Guid.TryParse(raw, out parsed))
            return ValidationResult.Fail($"{propName} não é um Guid válido");

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateIntArg(JsonElement args, string propName, int min, int max, out int parsed)
    {
        parsed = min;

        if (!args.TryGetProperty(propName, out var el))
            return ValidationResult.Fail($"{propName} ausente");

        if (!el.TryGetInt32(out parsed))
            return ValidationResult.Fail($"{propName} não é inteiro");

        if (parsed < min || parsed > max)
            return ValidationResult.Fail($"{propName} fora do intervalo [{min}, {max}]");

        return ValidationResult.Success();
    }

    public static ValidationResult CheckStringArgsForInjection(JsonElement args)
    {
        foreach (var prop in args.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String) continue;
            var val = prop.Value.GetString()!;
            var (detected, _) = PromptInjectionPatterns.Check(val);
            if (detected)
                return ValidationResult.Fail($"Conteúdo suspeito no argumento {prop.Name}");
        }
        return ValidationResult.Success();
    }
}
