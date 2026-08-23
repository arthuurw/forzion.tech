using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.UseCases.Agents.Disponibilidade;

namespace forzion.tech.Tests.Api.Agents;

// Literais do schema `AvailabilitySlot` em `.specs/contracts/forzion-internal-api.v1.yaml` (arquivo
// gitignored, indisponível no CI — mesmo motivo por que StagedLeadContractTests (fatia 2) transcreve
// à mão os literais do contrato em vez de recalculá-los a partir do arquivo em runtime).
public class DisponibilidadeContratoTests
{
    private static readonly string[] CamposObrigatoriosDoSchema = ["slotId", "serviceId", "startsAt", "endsAt"];
    private static readonly string[] TodosOsCamposDoSchema = ["slotId", "serviceId", "startsAt", "endsAt", "capacityRemaining"];

    private static AvailabilitySlotResponse SlotValido() => new(
        "a1b2c3",
        "11111111-1111-1111-1111-111111111111",
        new DateTimeOffset(2026, 8, 24, 11, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero),
        3);

    [Fact]
    public void AvailabilitySlotResponse_PropriedadesPublicas_CorrespondemExatamenteAosCamposDoSchema()
    {
        var camposEmCamelCase = typeof(AvailabilitySlotResponse).GetProperties()
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name));

        camposEmCamelCase.Should().BeEquivalentTo(TodosOsCamposDoSchema,
            "um campo novo ou removido em AvailabilitySlotResponse precisa entrar CONSCIENTEMENTE no contrato — campo extra quebra a conformidade por desenho");
    }

    [Fact]
    public void AvailabilitySlotResponse_Serializado_ContemExatamenteOsCamposDoSchemaComOsTiposCorretos()
    {
        var slot = SlotValido();

        // Minimal API serializa com camelCase por padrão (JsonOptions do host) — a mesma
        // policy é aplicada aqui para o teste refletir o que sai de fato pela borda.
        var json = JsonSerializer.Serialize(slot, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var documento = JsonDocument.Parse(json);

        documento.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(TodosOsCamposDoSchema);

        foreach (var campo in CamposObrigatoriosDoSchema)
            documento.RootElement.TryGetProperty(campo, out _).Should().BeTrue($"'{campo}' é required no schema AvailabilitySlot");

        documento.RootElement.GetProperty("slotId").GetString().Should().Be(slot.SlotId);
        documento.RootElement.GetProperty("serviceId").GetString().Should().Be(slot.ServiceId);
        documento.RootElement.GetProperty("capacityRemaining").GetInt32().Should().Be(slot.CapacityRemaining);
    }

    [Theory]
    [InlineData("startsAt")]
    [InlineData("endsAt")]
    public void AvailabilitySlotResponse_Serializado_CamposDeInstanteSaoIso8601ComOffset(string campo)
    {
        var slot = SlotValido();

        var json = JsonSerializer.Serialize(slot, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var documento = JsonDocument.Parse(json);

        var valor = documento.RootElement.GetProperty(campo).GetString()!;

        DateTimeOffset.TryParse(valor, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            .Should().BeTrue($"'{campo}' deve ser um instante ISO 8601 válido");
        valor.Should().MatchRegex(@"(Z|[+-]\d{2}:\d{2})$", $"'{campo}' deve conter offset explícito, não wall-clock ambíguo");
    }
}
