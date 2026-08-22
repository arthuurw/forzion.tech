using FluentAssertions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.ValueObjects;

public class ConsentimentoLeadTests
{
    private static readonly DateTime ConcedidoEm = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RegistradoEm = new(2026, 8, 1, 10, 0, 5, DateTimeKind.Utc);

    [Fact]
    public void Criar_ComFinalidadeValida_GuardaOsDoisInstantes()
    {
        var r = ConsentimentoLead.Criar("Contato comercial", ConcedidoEm, RegistradoEm);

        r.IsSuccess.Should().BeTrue();
        r.Value.Finalidade.Should().Be("Contato comercial");
        r.Value.ConcedidoEm.Should().Be(ConcedidoEm);
        r.Value.RegistradoEm.Should().Be(RegistradoEm);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_FinalidadeAusente_Falha(string finalidade)
    {
        var r = ConsentimentoLead.Criar(finalidade, ConcedidoEm, RegistradoEm);

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_FinalidadeAcimaDe500_Falha()
    {
        var finalidade = new string('a', 501);

        var r = ConsentimentoLead.Criar(finalidade, ConcedidoEm, RegistradoEm);

        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Contain("500");
    }

    [Fact]
    public void Criar_ConcedidoEmDiferenteDeRegistradoEm_MantemAmbosSeparados()
    {
        var concedidoNoFuturo = RegistradoEm.AddDays(30);

        var r = ConsentimentoLead.Criar("Contato comercial", concedidoNoFuturo, RegistradoEm);

        r.IsSuccess.Should().BeTrue();
        r.Value.ConcedidoEm.Should().Be(concedidoNoFuturo);
        r.Value.RegistradoEm.Should().Be(RegistradoEm);
    }
}
