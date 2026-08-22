using FluentAssertions;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Domain.Entities;

public class HorarioFuncionamentoTests
{
    [Fact]
    public void Criar_DadosValidos_RetornaHorario()
    {
        var r = HorarioFuncionamento.Criar(1, new TimeOnly(8, 0), new TimeOnly(12, 0));

        r.IsSuccess.Should().BeTrue();
        r.Value.Id.Should().NotBeEmpty();
        r.Value.DiaSemana.Should().Be(1);
        r.Value.AbreAs.Should().Be(new TimeOnly(8, 0));
        r.Value.FechaAs.Should().Be(new TimeOnly(12, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Criar_DiaSemanaForaDe0A6_Falha(int diaSemana)
    {
        var r = HorarioFuncionamento.Criar(diaSemana, new TimeOnly(8, 0), new TimeOnly(12, 0));
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_AbreAsIgualFechaAs_Falha()
    {
        var r = HorarioFuncionamento.Criar(0, new TimeOnly(8, 0), new TimeOnly(8, 0));
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_AbreAsMaiorQueFechaAs_Falha()
    {
        var r = HorarioFuncionamento.Criar(0, new TimeOnly(13, 0), new TimeOnly(8, 0));
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_MesmoDiaSemanaEmMultiplasInstancias_AmbasValidas()
    {
        var manha = HorarioFuncionamento.Criar(1, new TimeOnly(8, 0), new TimeOnly(12, 0));
        var tarde = HorarioFuncionamento.Criar(1, new TimeOnly(14, 0), new TimeOnly(18, 0));

        manha.IsSuccess.Should().BeTrue();
        tarde.IsSuccess.Should().BeTrue();
        manha.Value.Id.Should().NotBe(tarde.Value.Id);
        manha.Value.DiaSemana.Should().Be(tarde.Value.DiaSemana);
    }
}
