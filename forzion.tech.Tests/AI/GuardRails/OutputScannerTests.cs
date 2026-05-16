using FluentAssertions;
using forzion.tech.AI.GuardRails;

namespace forzion.tech.Tests.AI.GuardRails;

public class OutputScannerTests
{
    [Theory]
    [InlineData("CPF: 123.456.789-09")]
    [InlineData("cpf 12345678909")]
    public void Scan_Cpf_IsCritical(string output)
    {
        var result = OutputScanner.Scan(output);
        result.HasCritical.Should().BeTrue();
        result.Findings.Should().Contain("cpf");
    }

    [Theory]
    [InlineData("CNPJ: 12.345.678/0001-95")]
    [InlineData("cnpj 12345678000195")]
    public void Scan_Cnpj_IsCritical(string output)
    {
        var result = OutputScanner.Scan(output);
        result.HasCritical.Should().BeTrue();
        result.Findings.Should().Contain("cnpj");
    }

    [Theory]
    [InlineData("Cartão: 4111 1111 1111 1111")]
    [InlineData("4111111111111111")]
    public void Scan_CardNumber_IsCritical(string output)
    {
        var result = OutputScanner.Scan(output);
        result.HasCritical.Should().BeTrue();
        result.Findings.Should().Contain("card");
    }

    [Theory]
    [InlineData("sk-abc123def456ghi789jkl012mno345pqr678")]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.abc")]
    public void Scan_ApiKey_IsCritical(string output)
    {
        var result = OutputScanner.Scan(output);
        result.HasCritical.Should().BeTrue();
        result.Findings.Should().Contain("api_key");
    }

    [Fact]
    public void Scan_ExfiltrationImage_IsCritical()
    {
        var output = "![track](https://tracker.evil.com/pixel.gif)";
        var result = OutputScanner.Scan(output);
        result.HasCritical.Should().BeTrue();
        result.Findings.Should().Contain("exfiltration_img");
    }

    [Fact]
    public void Scan_Email_IsWarningNotCritical()
    {
        var result = OutputScanner.Scan("Contate: usuario@exemplo.com.br");
        result.HasCritical.Should().BeFalse();
        result.Findings.Should().Contain("email_warning");
    }

    [Fact]
    public void Scan_CleanOutput_NoFindings()
    {
        var result = OutputScanner.Scan("Seu próximo treino é amanhã às 7h. Foco: membros superiores.");
        result.HasCritical.Should().BeFalse();
        result.Findings.Should().BeEmpty();
    }
}
