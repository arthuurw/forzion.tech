using FluentAssertions;
using forzion.tech.Application.UseCases.Agents.Agendamentos;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Tests.Application.Agents.Agendamentos;

public class IdempotenciaAgendamentoTests
{
    private static readonly Guid ServiceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string SlotId = "slot-abc";
    private const string Nome = "Maria";
    private const TipoContatoLead TipoContato = TipoContatoLead.Email;
    private const string ContatoNormalizado = "maria@example.com";
    private const string Finalidade = "aula experimental";

    [Fact]
    public void Calcular_EntradaFixa_ProduzOHexEsperado()
    {
        var hash = IdempotenciaAgendamento.Calcular(ServiceId, SlotId, Nome, TipoContato, ContatoNormalizado, Finalidade);

        hash.Should().Be("306ec1546c06c8a637521f1e08c30a2a05c1a37a9f685394604a02f928ef007e");
    }

    [Fact]
    public void Calcular_MesmosArgumentos_ProduzMesmoHash()
    {
        var hash1 = IdempotenciaAgendamento.Calcular(ServiceId, SlotId, Nome, TipoContato, ContatoNormalizado, Finalidade);
        var hash2 = IdempotenciaAgendamento.Calcular(ServiceId, SlotId, Nome, TipoContato, ContatoNormalizado, Finalidade);

        hash1.Should().Be(hash2);
    }

    public static IEnumerable<object[]> CamposAlterados()
    {
        yield return [Guid.Parse("22222222-2222-2222-2222-222222222222"), SlotId, Nome, TipoContato, ContatoNormalizado, Finalidade];
        yield return [ServiceId, "outro-slot", Nome, TipoContato, ContatoNormalizado, Finalidade];
        yield return [ServiceId, SlotId, "Outro Nome", TipoContato, ContatoNormalizado, Finalidade];
        yield return [ServiceId, SlotId, Nome, TipoContatoLead.WhatsApp, ContatoNormalizado, Finalidade];
        yield return [ServiceId, SlotId, Nome, TipoContato, "outro@example.com", Finalidade];
        yield return [ServiceId, SlotId, Nome, TipoContato, ContatoNormalizado, "outra finalidade"];
    }

    [Theory]
    [MemberData(nameof(CamposAlterados))]
    public void Calcular_QualquerUmDosSeisCamposAlterado_ProduzHashDiferente(
        Guid serviceId, string slotId, string nome, TipoContatoLead tipoContato, string contatoNormalizado, string finalidade)
    {
        var referencia = IdempotenciaAgendamento.Calcular(ServiceId, SlotId, Nome, TipoContato, ContatoNormalizado, Finalidade);
        var alterado = IdempotenciaAgendamento.Calcular(serviceId, slotId, nome, tipoContato, contatoNormalizado, finalidade);

        alterado.Should().NotBe(referencia);
    }
}
