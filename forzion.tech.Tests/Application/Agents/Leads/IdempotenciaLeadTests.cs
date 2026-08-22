using FluentAssertions;
using forzion.tech.Application.UseCases.Agents.Leads;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Tests.Application.Agents.Leads;

public class IdempotenciaLeadTests
{
    [Fact]
    public void Calcular_MesmosArgumentos_ProduzMesmoHash()
    {
        var hash1 = IdempotenciaLead.Calcular("Fulano", TipoContatoLead.Email, "fulano@lead.com", "quero treinar", "Contato comercial");
        var hash2 = IdempotenciaLead.Calcular("Fulano", TipoContatoLead.Email, "fulano@lead.com", "quero treinar", "Contato comercial");

        hash1.Should().Be(hash2);
    }

    [Theory]
    [InlineData("Ciclano", TipoContatoLead.Email, "fulano@lead.com", "quero treinar", "Contato comercial")]
    [InlineData("Fulano", TipoContatoLead.WhatsApp, "fulano@lead.com", "quero treinar", "Contato comercial")]
    [InlineData("Fulano", TipoContatoLead.Email, "outro@lead.com", "quero treinar", "Contato comercial")]
    [InlineData("Fulano", TipoContatoLead.Email, "fulano@lead.com", "outro interesse", "Contato comercial")]
    [InlineData("Fulano", TipoContatoLead.Email, "fulano@lead.com", "quero treinar", "outra finalidade")]
    public void Calcular_CampoDeNegocioDiferente_ProduzHashDiferente(string nome, TipoContatoLead tipo, string valor, string? interesse, string finalidade)
    {
        var referencia = IdempotenciaLead.Calcular("Fulano", TipoContatoLead.Email, "fulano@lead.com", "quero treinar", "Contato comercial");
        var alterado = IdempotenciaLead.Calcular(nome, tipo, valor, interesse, finalidade);

        alterado.Should().NotBe(referencia);
    }

    [Fact]
    public void Calcular_InteresseAusenteEmAmbos_ProduzMesmoHash()
    {
        var hash1 = IdempotenciaLead.Calcular("Fulano", TipoContatoLead.Email, "fulano@lead.com", null, "Contato comercial");
        var hash2 = IdempotenciaLead.Calcular("Fulano", TipoContatoLead.Email, "fulano@lead.com", null, "Contato comercial");

        hash1.Should().Be(hash2);
    }
}
