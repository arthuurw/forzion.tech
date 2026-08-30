using System.Text.Json.Serialization;

namespace forzion.tech.Application.UseCases.Agents.BusinessInfo;

public sealed record AddressResponse(
    string? Street,
    string? Number,
    string? Complement,
    string? Neighborhood,
    string? City,
    string? State,
    string? PostalCode);

public sealed record OpeningHoursResponse(int DayOfWeek, string OpensAt, string ClosesAt);

public sealed record BusinessInfoResponse(
    string Name,
    string Timezone,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AddressResponse? Address,
    IReadOnlyList<string> Modalities,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<OpeningHoursResponse>? OpeningHours,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Policies);
