using FluentAssertions;
using forzion.tech.Application.UseCases.Admin.HealthReport;

namespace forzion.tech.Tests.Application.Admin.HealthReport;

public class AtualizarHealthReportConfigCommandValidatorTests
{
    private readonly AtualizarHealthReportConfigCommandValidator _validator = new();

    private static AtualizarHealthReportConfigCommand Command(bool ativo, params string[] destinatarios) =>
        new(ativo, new TimeOnly(7, 0), destinatarios, true, true, true, true);

    [Fact]
    public void Ativo_SemDestinatarios_Invalido()
    {
        var result = _validator.Validate(Command(true));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Ativo_ComDestinatario_Valido()
    {
        var result = _validator.Validate(Command(true, "a@b.com"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Inativo_SemDestinatarios_Valido()
    {
        var result = _validator.Validate(Command(false));
        result.IsValid.Should().BeTrue();
    }
}
