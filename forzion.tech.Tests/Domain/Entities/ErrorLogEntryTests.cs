using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class ErrorLogEntryTests
{
    [Fact]
    public void Criar_DadosValidos_RetornaEntry()
    {
        var ocorrido = new DateTime(2026, 5, 26, 10, 0, 0, DateTimeKind.Utc);

        var entry = ErrorLogEntry.Criar(ocorrido, "Error", "PaymentService", "Falha ao processar pagamento");

        entry.Id.Should().NotBeEmpty();
        entry.OcorridoEm.Should().Be(ocorrido);
        entry.Nivel.Should().Be("Error");
        entry.Origem.Should().Be("PaymentService");
        entry.Mensagem.Should().Be("Falha ao processar pagamento");
        entry.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Criar_NivelEOrigemComEspacos_Remove()
    {
        var entry = ErrorLogEntry.Criar(DateTime.UtcNow, "  Critical  ", "  Worker  ", "boom");
        entry.Nivel.Should().Be("Critical");
        entry.Origem.Should().Be("Worker");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NivelVazio_LancaDomainException(string nivel)
    {
        var act = () => ErrorLogEntry.Criar(DateTime.UtcNow, nivel, "Worker", "boom");
        act.Should().Throw<DomainException>().WithMessage("O nível é obrigatório.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_OrigemVazia_LancaDomainException(string origem)
    {
        var act = () => ErrorLogEntry.Criar(DateTime.UtcNow, "Error", origem, "boom");
        act.Should().Throw<DomainException>().WithMessage("A origem é obrigatória.");
    }

    [Fact]
    public void Criar_MensagemLonga_Trunca()
    {
        var longa = new string('x', ErrorLogEntry.MensagemMaxLength + 500);

        var entry = ErrorLogEntry.Criar(DateTime.UtcNow, "Error", "Worker", longa);

        entry.Mensagem.Length.Should().Be(ErrorLogEntry.MensagemMaxLength);
    }

    [Fact]
    public void Criar_MensagemNula_ViraVazia()
    {
        var entry = ErrorLogEntry.Criar(DateTime.UtcNow, "Error", "Worker", null!);
        entry.Mensagem.Should().BeEmpty();
    }
}
