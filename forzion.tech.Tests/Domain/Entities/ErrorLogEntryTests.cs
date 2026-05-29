using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class ErrorLogEntryTests
{
    [Fact]
    public void Criar_DadosValidos_RetornaEntry()
    {
        var ocorrido = new DateTime(2026, 5, 26, 10, 0, 0, DateTimeKind.Utc);

        var entry = ErrorLogEntry.Criar(ocorrido, "Error", "PaymentService", "Falha ao processar pagamento", TestData.Agora).Value;

        entry.Id.Should().NotBeEmpty();
        entry.OcorridoEm.Should().Be(ocorrido);
        entry.Nivel.Should().Be("Error");
        entry.Origem.Should().Be("PaymentService");
        entry.Mensagem.Should().Be("Falha ao processar pagamento");
        entry.CreatedAt.Should().BeCloseTo(TestData.Agora, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Criar_NivelEOrigemComEspacos_Remove()
    {
        var entry = ErrorLogEntry.Criar(TestData.Agora, "  Critical  ", "  Worker  ", "boom", TestData.Agora).Value;
        entry.Nivel.Should().Be("Critical");
        entry.Origem.Should().Be("Worker");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NivelVazio_LancaDomainException(string nivel)
    {
        var r = ErrorLogEntry.Criar(TestData.Agora, nivel, "Worker", "boom", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nível é obrigatório.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_OrigemVazia_LancaDomainException(string origem)
    {
        var r = ErrorLogEntry.Criar(TestData.Agora, "Error", origem, "boom", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A origem é obrigatória.");
    }

    [Fact]
    public void Criar_MensagemLonga_Trunca()
    {
        var longa = new string('x', ErrorLogEntry.MensagemMaxLength + 500);

        var entry = ErrorLogEntry.Criar(TestData.Agora, "Error", "Worker", longa, TestData.Agora).Value;

        entry.Mensagem.Length.Should().Be(ErrorLogEntry.MensagemMaxLength);
    }

    [Fact]
    public void Criar_MensagemNula_ViraVazia()
    {
        var entry = ErrorLogEntry.Criar(TestData.Agora, "Error", "Worker", null!, TestData.Agora).Value;
        entry.Mensagem.Should().BeEmpty();
    }
}
