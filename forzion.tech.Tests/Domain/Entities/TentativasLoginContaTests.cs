using FluentAssertions;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Domain.Entities;

public class TentativasLoginContaTests
{
    private static readonly Guid ContaId = Guid.NewGuid();

    private static FakeTimeProvider Relogio() =>
        new(new DateTimeOffset(2026, 6, 17, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Criar_IniciaComZeroTentativas()
    {
        var relogio = Relogio();

        var registro = TentativasLoginConta.Criar(ContaId, relogio.GetUtcNow().UtcDateTime);

        registro.Tentativas.Should().Be(0);
        registro.ContaId.Should().Be(ContaId);
    }

    [Fact]
    public void SemFalhas_DelayAtualEhZero()
    {
        var relogio = Relogio();
        var registro = TentativasLoginConta.Criar(ContaId, relogio.GetUtcNow().UtcDateTime);

        registro.DelayAtual().Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void RegistrarFalha_IncrementaEAtualizaRelogio()
    {
        var relogio = Relogio();
        var registro = TentativasLoginConta.Criar(ContaId, relogio.GetUtcNow().UtcDateTime);
        relogio.Advance(TimeSpan.FromMinutes(1));
        var agora = relogio.GetUtcNow().UtcDateTime;

        registro.RegistrarFalha(agora);

        registro.Tentativas.Should().Be(1);
        registro.AtualizadoEm.Should().Be(agora);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void DelayAtual_CrescaExponencialmenteAbaixoDoTeto(int tentativas, int segundosEsperados)
    {
        var relogio = Relogio();
        var registro = TentativasLoginConta.Criar(ContaId, relogio.GetUtcNow().UtcDateTime);
        var agora = relogio.GetUtcNow().UtcDateTime;
        for (var i = 0; i < tentativas; i++)
            registro.RegistrarFalha(agora);

        registro.DelayAtual().Should().Be(TimeSpan.FromSeconds(segundosEsperados));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(50)]
    public void DelayAtual_AcimaDoTeto_NuncaExcedeODelayMaximo(int tentativas)
    {
        var relogio = Relogio();
        var registro = TentativasLoginConta.Criar(ContaId, relogio.GetUtcNow().UtcDateTime);
        var agora = relogio.GetUtcNow().UtcDateTime;
        for (var i = 0; i < tentativas; i++)
            registro.RegistrarFalha(agora);

        registro.DelayAtual().Should().Be(TimeSpan.FromSeconds(TentativasLoginConta.DelayMaximoSegundos));
    }

    [Fact]
    public void Zerar_ReiniciaTentativasEDelay()
    {
        var relogio = Relogio();
        var registro = TentativasLoginConta.Criar(ContaId, relogio.GetUtcNow().UtcDateTime);
        var agora = relogio.GetUtcNow().UtcDateTime;
        for (var i = 0; i < 3; i++)
            registro.RegistrarFalha(agora);

        relogio.Advance(TimeSpan.FromSeconds(5));
        var depois = relogio.GetUtcNow().UtcDateTime;
        registro.Zerar(depois);

        registro.Tentativas.Should().Be(0);
        registro.AtualizadoEm.Should().Be(depois);
        registro.DelayAtual().Should().Be(TimeSpan.Zero);
    }
}
