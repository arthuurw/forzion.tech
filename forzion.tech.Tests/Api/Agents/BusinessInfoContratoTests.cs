using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.UseCases.Agents.BusinessInfo;

namespace forzion.tech.Tests.Api.Agents;

// Literais do schema `BusinessInfo` em `.specs/contracts/forzion-internal-api.v1.yaml` v1.3.1
// (arquivo gitignored e ausente do CI por decisão do repo do gateway — repo público; mesmo motivo
// por que DisponibilidadeContratoTests e StagedLeadContractTests transcrevem os literais à mão em
// vez de carregá-los do arquivo em runtime). Sem o schema no CI, este teste é a única guarda local
// contra drift de shape: o que o pegaria depois é a suíte T17 do gateway, já em produção.
public class BusinessInfoContratoTests
{
    private static readonly string[] CamposObrigatoriosDoSchema = ["name", "modalities"];
    private static readonly string[] TodosOsCamposDoSchema = ["name", "timezone", "address", "modalities", "openingHours", "policies"];
    private static readonly string[] CamposOmitidosQuandoVazios = ["address", "openingHours", "policies"];

    private static readonly JsonSerializerOptions OpcoesDaBorda = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static BusinessInfoResponse Completo() => new(
        "Studio Teste",
        "America/Manaus",
        new AddressResponse("Rua das Flores", "123", null, "Centro", "Fortaleza", "CE", "60000000"),
        ["Pilates"],
        [new OpeningHoursResponse(1, "08:00", "18:00")],
        new Dictionary<string, string> { ["cancelamento"] = "24h de antecedencia" });

    private static BusinessInfoResponse Minimo() => new("Studio Teste", "America/Sao_Paulo", null, [], null, null);

    [Fact]
    public void BusinessInfoResponse_PropriedadesPublicas_CorrespondemExatamenteAosCamposDoSchema()
    {
        var camposEmCamelCase = typeof(BusinessInfoResponse).GetProperties()
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name));

        camposEmCamelCase.Should().BeEquivalentTo(TodosOsCamposDoSchema,
            "um campo novo ou removido em BusinessInfoResponse precisa entrar CONSCIENTEMENTE no contrato — campo extra quebra a conformidade por desenho");
    }

    [Fact]
    public void BusinessInfoResponse_SerializadoCompleto_ContemExatamenteOsCamposDoSchema()
    {
        var json = JsonSerializer.Serialize(Completo(), OpcoesDaBorda);
        using var documento = JsonDocument.Parse(json);

        documento.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(TodosOsCamposDoSchema);

        foreach (var campo in CamposObrigatoriosDoSchema)
            documento.RootElement.TryGetProperty(campo, out _).Should().BeTrue($"'{campo}' é required no schema BusinessInfo");

        documento.RootElement.GetProperty("name").GetString().Should().Be("Studio Teste");
        documento.RootElement.GetProperty("timezone").GetString().Should().Be("America/Manaus");
    }

    [Fact]
    public void BusinessInfoResponse_SerializadoSemEnderecoHorariosNemPoliticas_OmiteExatamenteEssesTres()
    {
        var json = JsonSerializer.Serialize(Minimo(), OpcoesDaBorda);
        using var documento = JsonDocument.Parse(json);

        var camposPresentes = documento.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        camposPresentes.Should().NotIntersectWith(CamposOmitidosQuandoVazios,
            "a v1.3.1 é normativa: address, openingHours e policies são OMITIDOS quando nulos, nunca emitidos como null");
        camposPresentes.Should().BeEquivalentTo(["name", "timezone", "modalities"],
            "todo o resto do schema é emitido sempre — timezone inclusive, que não é omissível");
    }
}
