using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.UseCases.Agents.Agendamentos;

namespace forzion.tech.Tests.Api.Agents;

// Literais do schema `StagedBookingRequest` em `.specs/contracts/forzion-internal-api.v1.yaml`
// (arquivo gitignored, indisponível no CI — mesmo motivo por que StagedLeadContractTests, da
// fatia 3, transcreve à mão os literais do contrato em vez de recalculá-los a partir do arquivo).
//
// Os quatro códigos que colapsariam por descuido (tenant_not_found, service_not_found,
// slot_not_found, slot_unavailable) e o valor de wire exato de idempotency_conflict já são
// verificados ponta-a-ponta via HTTP em BookingRequestEndpointTests (T15) — este arquivo cobre
// especificamente o que aquele não cobre: a FORMA do schema StagedBookingRequest.
public class BookingRequestContratoTests
{
    private static readonly string[] CamposDoSchema = ["bookingRequestId", "status"];
    private const string StatusDoSchema = "pending-agent";

    [Fact]
    public void StagedBookingRequest_PropriedadesPublicas_CorrespondemExatamenteAosCamposDoSchema()
    {
        var camposEmCamelCase = typeof(StagedBookingRequest).GetProperties()
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name));

        camposEmCamelCase.Should().BeEquivalentTo(CamposDoSchema,
            "um campo novo ou removido em StagedBookingRequest precisa entrar CONSCIENTEMENTE no contrato — campo extra quebra a conformidade por desenho");
    }

    [Fact]
    public void StagedBookingRequest_Serializado_ContemExatamenteOsCamposDoSchemaComStatusPendingAgent()
    {
        var staged = new StagedBookingRequest("11111111-1111-1111-1111-111111111111", StatusDoSchema);

        // Minimal API serializa com camelCase por padrão (JsonOptions do host) — a mesma
        // policy é aplicada aqui para o teste refletir o que sai de fato pela borda.
        var json = JsonSerializer.Serialize(staged, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var documento = JsonDocument.Parse(json);

        documento.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(CamposDoSchema);
        documento.RootElement.GetProperty("bookingRequestId").GetString().Should().Be(staged.BookingRequestId);
        documento.RootElement.GetProperty("status").GetString().Should().Be(StatusDoSchema);
    }
}
