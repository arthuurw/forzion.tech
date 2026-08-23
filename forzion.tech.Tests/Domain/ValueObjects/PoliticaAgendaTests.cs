using FluentAssertions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.ValueObjects;

public class PoliticaAgendaTests
{
    // --- Criar ---

    [Fact]
    public void Criar_ValoresValidos_RetornaPolitica()
    {
        var p = PoliticaAgenda.Criar(4, 90).Value;

        p.AntecedenciaMinimaHoras.Should().Be(4);
        p.HorizonteDias.Should().Be(90);
    }

    [Fact]
    public void Criar_AntecedenciaZero_Aceita()
    {
        var r = PoliticaAgenda.Criar(0, 30);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Criar_AntecedenciaNegativa_Falha()
    {
        var r = PoliticaAgenda.Criar(-1, 30);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("politica_agenda.antecedencia_minima_invalida");
    }

    [Fact]
    public void Criar_HorizonteZero_Falha()
    {
        var r = PoliticaAgenda.Criar(2, 0);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("politica_agenda.horizonte_invalido");
    }

    [Fact]
    public void Criar_HorizonteNegativo_Falha()
    {
        var r = PoliticaAgenda.Criar(2, -10);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("politica_agenda.horizonte_invalido");
    }

    [Fact]
    public void Criar_HorizonteAcimaDoLimite_Falha()
    {
        var r = PoliticaAgenda.Criar(2, 366);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("politica_agenda.horizonte_invalido");
    }

    [Fact]
    public void Criar_HorizonteNoLimite_Aceita()
    {
        var r = PoliticaAgenda.Criar(2, 365);
        r.IsSuccess.Should().BeTrue();
    }

    // --- Padrao ---

    [Fact]
    public void Padrao_DevolveAntecedencia2EHorizonte60()
    {
        var p = PoliticaAgenda.Padrao();

        p.AntecedenciaMinimaHoras.Should().Be(2);
        p.HorizonteDias.Should().Be(60);
    }
}
