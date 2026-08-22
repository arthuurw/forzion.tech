using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.UseCases.Agents.Leads;

namespace forzion.tech.Tests.Api.Agents;

// Literais do schema `StagedLead` em `.specs/contracts/forzion-internal-api.v1.yaml` (arquivo
// gitignored, indisponível no CI — mesmo motivo por que CanonicalPayloadTests transcreve à mão
// os literais do contrato em vez de recalculá-los a partir do arquivo em runtime).
public class StagedLeadContractTests
{
    private static readonly string[] CamposDoSchema = ["leadId", "source", "status"];
    private const string SourceDoSchema = "agent";
    private const string StatusDoSchema = "pending";

    [Fact]
    public void StagedLead_PropriedadesPublicas_CorrespondemExatamenteAosCamposDoSchema()
    {
        var camposEmCamelCase = typeof(StagedLead).GetProperties()
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name));

        camposEmCamelCase.Should().BeEquivalentTo(CamposDoSchema,
            "um campo novo ou removido em StagedLead precisa entrar CONSCIENTEMENTE no contrato — este teste é o sensor de drift sem depender do gateway");
    }

    [Fact]
    public void StagedLead_Serializado_ContemExatamenteOsCamposDoSchemaComOsEnumsCorretos()
    {
        var staged = new StagedLead("11111111-1111-1111-1111-111111111111", SourceDoSchema, StatusDoSchema);

        // Minimal API serializa com camelCase por padrão (JsonOptions do host) — a mesma
        // policy é aplicada aqui para o teste refletir o que sai de fato pela borda.
        var json = JsonSerializer.Serialize(staged, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var documento = JsonDocument.Parse(json);

        documento.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(CamposDoSchema);
        documento.RootElement.GetProperty("leadId").GetString().Should().Be(staged.LeadId);
        documento.RootElement.GetProperty("source").GetString().Should().Be(SourceDoSchema);
        documento.RootElement.GetProperty("status").GetString().Should().Be(StatusDoSchema);
    }
}
