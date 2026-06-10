using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Tests.Domain.Entities;

public class OutboxEfeitoTests
{
    private static readonly DateTime Agora = new(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);

    private static OutboxEfeito Novo() =>
        OutboxEfeito.Criar("fx:teste", "{}", "fx:teste:1", Agora).Value;

    [Fact]
    public void Criar_Valido_NascePendenteProcessavelAgora()
    {
        var efeito = Novo();

        efeito.Status.Should().Be(OutboxStatus.Pendente);
        efeito.Tentativas.Should().Be(0);
        efeito.ProximaTentativa.Should().Be(Agora);
        efeito.ProcessadoEm.Should().BeNull();
        efeito.UltimoErro.Should().BeNull();
    }

    [Theory]
    [InlineData("", "{}", "k")]
    [InlineData("fx:t", "", "k")]
    [InlineData("fx:t", "{}", "")]
    public void Criar_CampoObrigatorioVazio_Falha(string tipo, string payload, string chave)
    {
        var result = OutboxEfeito.Criar(tipo, payload, chave, Agora);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarConcluido_AposProcessando_SetaProcessadoEm()
    {
        var efeito = Novo();
        efeito.MarcarProcessando();

        efeito.MarcarConcluido(Agora);

        efeito.Status.Should().Be(OutboxStatus.Concluido);
        efeito.ProcessadoEm.Should().Be(Agora);
    }

    [Fact]
    public void RegistrarFalha_IncrementaTentativaEVoltaPendente()
    {
        var efeito = Novo();
        efeito.MarcarProcessando();
        var proxima = Agora.AddMinutes(2);

        efeito.RegistrarFalha("timeout", proxima);

        efeito.Status.Should().Be(OutboxStatus.Pendente);
        efeito.Tentativas.Should().Be(1);
        efeito.UltimoErro.Should().Be("timeout");
        efeito.ProximaTentativa.Should().Be(proxima);
    }

    [Fact]
    public void MarcarFalhouDefinitivo_EstadoTerminal()
    {
        var efeito = Novo();
        efeito.MarcarProcessando();

        efeito.MarcarFalhouDefinitivo("erro fatal", Agora);

        efeito.Status.Should().Be(OutboxStatus.Falhou);
        efeito.Tentativas.Should().Be(1);
        efeito.ProcessadoEm.Should().Be(Agora);
    }

    [Fact]
    public void MarcarConcluido_SemProcessar_Lanca()
    {
        var efeito = Novo();

        var act = () => efeito.MarcarConcluido(Agora);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarcarProcessando_DuasVezes_Lanca()
    {
        var efeito = Novo();
        efeito.MarcarProcessando();

        var act = efeito.MarcarProcessando;

        act.Should().Throw<InvalidOperationException>();
    }
}
