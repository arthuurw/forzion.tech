using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;

namespace forzion.tech.Tests.Domain.Entities;

public class AssinaturaTreinadorTests
{
    private static readonly DateTime Agora = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static AssinaturaTreinador Nova(decimal valor = 50m)
        => AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), valor, Agora).Value;

    private static AssinaturaTreinador Ativa(decimal valor = 50m)
    {
        var a = Nova(valor);
        a.Ativar(Agora);
        a.ClearDomainEvents();
        return a;
    }

    [Fact]
    public void Criar_DadosValidos_RetornaPendenteComEvento()
    {
        var treinadorId = Guid.NewGuid();
        var planoId = Guid.NewGuid();

        var result = AssinaturaTreinador.Criar(treinadorId, planoId, 50m, Agora);

        result.IsSuccess.Should().BeTrue();
        var a = result.Value;
        a.Status.Should().Be(AssinaturaTreinadorStatus.Pendente);
        a.TreinadorId.Should().Be(treinadorId);
        a.PlanoPlataformaId.Should().Be(planoId);
        a.Valor.Should().Be(50m);
        a.DataInicio.Should().Be(Agora);
        a.DataProximaCobranca.Should().Be(Agora);
        a.DomainEvents.OfType<AssinaturaTreinadorCriadaEvent>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(false, true, 50)]
    [InlineData(true, false, 50)]
    [InlineData(true, true, 0)]
    [InlineData(true, true, -10)]
    public void Criar_DadosInvalidos_Falha(bool treinadorOk, bool planoOk, decimal valor)
    {
        var result = AssinaturaTreinador.Criar(
            treinadorOk ? Guid.NewGuid() : Guid.Empty,
            planoOk ? Guid.NewGuid() : Guid.Empty,
            valor, Agora);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Ativar_Pendente_VaiParaAtiva()
    {
        var a = Nova();
        a.Ativar(Agora).IsSuccess.Should().BeTrue();
        a.Status.Should().Be(AssinaturaTreinadorStatus.Ativa);
    }

    [Fact]
    public void Ativar_Cancelada_Falha()
    {
        var a = Nova();
        a.Cancelar(Agora);
        a.Ativar(Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Ativar_Inadimplente_FalhaDeveUsarRegularizacao()
    {
        var a = Ativa();
        a.RegistrarPagamentoFalho(Agora);
        a.RegistrarPagamentoFalho(Agora);
        a.RegistrarPagamentoFalho(Agora);
        a.Status.Should().Be(AssinaturaTreinadorStatus.Inadimplente);

        a.Ativar(Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarInadimplente_Ativa_Transiciona()
    {
        var a = Ativa();
        a.MarcarInadimplente(Agora).IsSuccess.Should().BeTrue();
        a.Status.Should().Be(AssinaturaTreinadorStatus.Inadimplente);
    }

    [Fact]
    public void MarcarInadimplente_NaoAtiva_Falha()
    {
        var a = Nova();
        a.MarcarInadimplente(Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Cancelar_DisparaEvento_ESegundaVezFalha()
    {
        var a = Ativa();
        a.Cancelar(Agora).IsSuccess.Should().BeTrue();
        a.Status.Should().Be(AssinaturaTreinadorStatus.Cancelada);
        a.DataCancelamento.Should().Be(Agora);
        a.DomainEvents.OfType<AssinaturaTreinadorCanceladaEvent>().Should().ContainSingle();
        a.Cancelar(Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AgendarProximaCobranca_Futura_Ok_PassadoFalha()
    {
        var a = Ativa();
        a.AgendarProximaCobranca(Agora.AddDays(30), Agora).IsSuccess.Should().BeTrue();
        a.DataProximaCobranca.Should().Be(Agora.AddDays(30));
        a.AgendarProximaCobranca(Agora, Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RegistrarPagamentoFalho_TresVezes_MarcaInadimplenteComEvento()
    {
        var a = Ativa();
        a.RegistrarPagamentoFalho(Agora);
        a.Status.Should().Be(AssinaturaTreinadorStatus.Ativa);
        a.RegistrarPagamentoFalho(Agora);
        a.Status.Should().Be(AssinaturaTreinadorStatus.Ativa);
        a.RegistrarPagamentoFalho(Agora);

        a.Status.Should().Be(AssinaturaTreinadorStatus.Inadimplente);
        a.TentativasFalhasConsecutivas.Should().Be(3);
        a.DomainEvents.OfType<AssinaturaTreinadorMarcadaInadimplenteEvent>().Should().ContainSingle();
    }

    [Fact]
    public void RegistrarPagamentoFalho_Cancelada_NoOp()
    {
        var a = Ativa();
        a.Cancelar(Agora);
        a.RegistrarPagamentoFalho(Agora);
        a.TentativasFalhasConsecutivas.Should().Be(0);
    }

    [Fact]
    public void RegistrarPagamentoRegularizado_Inadimplente_VoltaAtivaComEvento()
    {
        var a = Ativa();
        a.RegistrarPagamentoFalho(Agora);
        a.RegistrarPagamentoFalho(Agora);
        a.RegistrarPagamentoFalho(Agora);
        a.ClearDomainEvents();

        a.RegistrarPagamentoRegularizado(Agora);

        a.Status.Should().Be(AssinaturaTreinadorStatus.Ativa);
        a.TentativasFalhasConsecutivas.Should().Be(0);
        a.DomainEvents.OfType<AssinaturaTreinadorReativadaEvent>().Should().ContainSingle();
    }

    [Fact]
    public void RegistrarPagamentoRegularizado_Ativa_Idempotente_SemEvento()
    {
        var a = Ativa();
        a.RegistrarPagamentoRegularizado(Agora);
        a.Status.Should().Be(AssinaturaTreinadorStatus.Ativa);
        a.DomainEvents.OfType<AssinaturaTreinadorReativadaEvent>().Should().BeEmpty();
    }

    [Fact]
    public void TrocarPlanoImediato_Ativa_AplicaNovoPlanoComEvento()
    {
        var a = Ativa(50m);
        var novoPlano = Guid.NewGuid();

        a.TrocarPlanoImediato(novoPlano, 100m, Agora).IsSuccess.Should().BeTrue();

        a.PlanoPlataformaId.Should().Be(novoPlano);
        a.Valor.Should().Be(100m);
        a.PlanoPlataformaIdAgendado.Should().BeNull();
        a.DomainEvents.OfType<AssinaturaTreinadorPlanoTrocadoEvent>().Should().ContainSingle();
    }

    [Fact]
    public void TrocarPlanoImediato_Cancelada_Falha()
    {
        var a = Ativa();
        a.Cancelar(Agora);
        a.TrocarPlanoImediato(Guid.NewGuid(), 100m, Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AgendarDowngrade_Ativa_GuardaAgendado()
    {
        var a = Ativa(100m);
        var alvo = Guid.NewGuid();

        a.AgendarDowngrade(alvo, Agora).IsSuccess.Should().BeTrue();

        a.PlanoPlataformaIdAgendado.Should().Be(alvo);
        a.Valor.Should().Be(100m, "downgrade só vale na renovação");
    }

    [Fact]
    public void AgendarDowngrade_NaoAtiva_Falha()
    {
        var a = Nova();
        a.AgendarDowngrade(Guid.NewGuid(), Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AplicarPlanoAgendado_Pago_AplicaNovoPlano()
    {
        var a = Ativa(100m);
        var alvo = Guid.NewGuid();
        a.AgendarDowngrade(alvo, Agora);
        a.ClearDomainEvents();

        a.AplicarPlanoAgendado(50m, Agora).IsSuccess.Should().BeTrue();

        a.PlanoPlataformaId.Should().Be(alvo);
        a.Valor.Should().Be(50m);
        a.PlanoPlataformaIdAgendado.Should().BeNull();
        a.DomainEvents.OfType<AssinaturaTreinadorPlanoTrocadoEvent>().Should().ContainSingle();
    }

    [Fact]
    public void AplicarPlanoAgendado_Free_CancelaAssinatura()
    {
        var a = Ativa(100m);
        a.AgendarDowngrade(Guid.NewGuid(), Agora);
        a.ClearDomainEvents();

        a.AplicarPlanoAgendado(0m, Agora).IsSuccess.Should().BeTrue();

        a.Status.Should().Be(AssinaturaTreinadorStatus.Cancelada);
        a.DomainEvents.OfType<AssinaturaTreinadorCanceladaEvent>().Should().ContainSingle();
    }

    [Fact]
    public void AplicarPlanoAgendado_SemAgendado_NoOp()
    {
        var a = Ativa(100m);
        a.AplicarPlanoAgendado(50m, Agora).IsSuccess.Should().BeTrue();
        a.Valor.Should().Be(100m);
    }
}
