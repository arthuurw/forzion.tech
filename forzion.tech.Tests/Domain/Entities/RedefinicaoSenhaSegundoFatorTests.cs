using FluentAssertions;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Domain.Entities;

public class RedefinicaoSenhaSegundoFatorTests
{
    private static readonly Guid ContaId = Guid.NewGuid();

    private static FakeTimeProvider Relogio() =>
        new(new DateTimeOffset(2026, 6, 17, 12, 0, 0, TimeSpan.Zero));

    private static RedefinicaoSenhaSegundoFator Novo(FakeTimeProvider relogio) =>
        RedefinicaoSenhaSegundoFator.Criar(ContaId, relogio.GetUtcNow().UtcDateTime).Value;

    [Fact]
    public void Criar_ContaIdVazio_RetornaFailure()
    {
        var r = RedefinicaoSenhaSegundoFator.Criar(Guid.Empty, DateTime.UtcNow);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegistrarFalha_Incrementa()
    {
        var relogio = Relogio();
        var guard = Novo(relogio);

        guard.RegistrarFalha(relogio.GetUtcNow().UtcDateTime);

        guard.Tentativas.Should().Be(1);
    }

    [Fact]
    public void GarantirNaoBloqueado_AbaixoDoCap_Sucesso()
    {
        var relogio = Relogio();
        var guard = Novo(relogio);
        var agora = relogio.GetUtcNow().UtcDateTime;

        guard.RegistrarFalha(agora);

        guard.GarantirNaoBloqueado(agora).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RegistrarFalha_AtingeCap_Bloqueia()
    {
        var relogio = Relogio();
        var guard = Novo(relogio);
        var agora = relogio.GetUtcNow().UtcDateTime;

        for (var i = 0; i < RedefinicaoSenhaSegundoFator.MaximoTentativas; i++)
            guard.RegistrarFalha(agora);

        guard.Tentativas.Should().Be(RedefinicaoSenhaSegundoFator.MaximoTentativas);
        guard.Bloqueado(agora).Should().BeTrue();
        guard.GarantirNaoBloqueado(agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Bloqueio_ExpiraComJanela_LiberaEReiniciaContador()
    {
        var relogio = Relogio();
        var guard = Novo(relogio);
        var inicio = relogio.GetUtcNow().UtcDateTime;

        for (var i = 0; i < RedefinicaoSenhaSegundoFator.MaximoTentativas; i++)
            guard.RegistrarFalha(inicio);
        guard.Bloqueado(inicio).Should().BeTrue();

        relogio.Advance(RedefinicaoSenhaSegundoFator.Janela);
        var depois = relogio.GetUtcNow().UtcDateTime;

        guard.Bloqueado(depois).Should().BeFalse();
        guard.GarantirNaoBloqueado(depois).IsSuccess.Should().BeTrue();

        guard.RegistrarFalha(depois);
        guard.Tentativas.Should().Be(1);
    }

    [Fact]
    public void Bloqueio_EhPorConta_NaoVazaParaOutraConta()
    {
        var agora = DateTime.UtcNow;
        var contaA = RedefinicaoSenhaSegundoFator.Criar(Guid.NewGuid(), agora).Value;
        var contaB = RedefinicaoSenhaSegundoFator.Criar(Guid.NewGuid(), agora).Value;

        for (var i = 0; i < RedefinicaoSenhaSegundoFator.MaximoTentativas; i++)
            contaA.RegistrarFalha(agora);

        contaA.Bloqueado(agora).Should().BeTrue();
        contaB.Bloqueado(agora).Should().BeFalse();
        contaB.GarantirNaoBloqueado(agora).IsSuccess.Should().BeTrue();
    }
}
