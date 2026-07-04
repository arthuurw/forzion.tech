using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class NotificacaoTests
{
    private static Notificacao Criar() =>
        Notificacao.Criar(Guid.NewGuid(), TipoNotificacao.NovoTreino, "Novo treino", "Seu treinador liberou um treino", TestData.Agora).Value;

    [Fact]
    public void Criar_DadosValidos_RetornaNotificacaoNaoLida()
    {
        var link = "/treinos/123";
        var dia = new DateOnly(2026, 5, 24);
        var n = Notificacao.Criar(
            Guid.NewGuid(), TipoNotificacao.Reforco, "Parabéns", "Você treinou hoje", TestData.Agora, link, dia).Value;

        n.Tipo.Should().Be(TipoNotificacao.Reforco);
        n.Titulo.Should().Be("Parabéns");
        n.Corpo.Should().Be("Você treinou hoje");
        n.LinkRelativo.Should().Be(link);
        n.DiaReferencia.Should().Be(dia);
        n.Lida.Should().BeFalse();
        n.CreatedAt.Should().Be(TestData.Agora);
    }

    [Fact]
    public void Criar_SemDiaReferencia_DiaNulo()
    {
        var n = Criar();
        n.DiaReferencia.Should().BeNull();
        n.LinkRelativo.Should().BeNull();
    }

    [Fact]
    public void Criar_DestinatarioVazio_Falha()
    {
        var r = Notificacao.Criar(Guid.Empty, TipoNotificacao.NovoTreino, "t", "c", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error.Should().Be(NotificacaoErrors.DestinatarioInvalido);
    }

    [Fact]
    public void Criar_TituloVazio_Falha()
    {
        var r = Notificacao.Criar(Guid.NewGuid(), TipoNotificacao.NovoTreino, "  ", "c", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error.Should().Be(NotificacaoErrors.TituloObrigatorio);
    }

    [Fact]
    public void Criar_TituloComTamanhoMaximo_Sucesso()
    {
        var r = Notificacao.Criar(Guid.NewGuid(), TipoNotificacao.NovoTreino, new string('a', 120), "c", TestData.Agora);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Criar_TituloMuitoLongo_Falha()
    {
        var r = Notificacao.Criar(Guid.NewGuid(), TipoNotificacao.NovoTreino, new string('a', 121), "c", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error.Should().Be(NotificacaoErrors.TituloMuitoLongo);
    }

    [Fact]
    public void Criar_CorpoVazio_Falha()
    {
        var r = Notificacao.Criar(Guid.NewGuid(), TipoNotificacao.NovoTreino, "t", "   ", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error.Should().Be(NotificacaoErrors.CorpoObrigatorio);
    }

    [Fact]
    public void Criar_CorpoComTamanhoMaximo_Sucesso()
    {
        var r = Notificacao.Criar(Guid.NewGuid(), TipoNotificacao.NovoTreino, "t", new string('a', 500), TestData.Agora);
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Criar_CorpoMuitoLongo_Falha()
    {
        var r = Notificacao.Criar(Guid.NewGuid(), TipoNotificacao.NovoTreino, "t", new string('a', 501), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error.Should().Be(NotificacaoErrors.CorpoMuitoLongo);
    }

    [Fact]
    public void MarcarLida_NotificacaoNaoLida_MarcaLidaEAtualiza()
    {
        var n = Criar();
        var depois = TestData.Agora.AddHours(2);

        n.MarcarLida(depois);

        n.Lida.Should().BeTrue();
        n.UpdatedAt.Should().Be(depois);
    }

    [Fact]
    public void MarcarLida_JaLida_NaoRealteraUpdatedAt()
    {
        var n = Criar();
        var primeira = TestData.Agora.AddHours(1);
        n.MarcarLida(primeira);

        n.MarcarLida(TestData.Agora.AddHours(5));

        n.Lida.Should().BeTrue();
        n.UpdatedAt.Should().Be(primeira);
    }
}
