using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;

namespace forzion.tech.Tests.Domain.Entities;

public class PagamentoTreinadorTests
{
    private static readonly DateTime Agora = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static PagamentoTreinador Novo(FinalidadePagamentoTreinador finalidade = FinalidadePagamentoTreinador.Cadastro, Guid? planoAlvo = null)
        => PagamentoTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, finalidade, Agora, MetodoPagamento.Pix, planoAlvo).Value;

    [Fact]
    public void Criar_DadosValidos_RetornaPendente()
    {
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();

        var result = PagamentoTreinador.Criar(treinadorId, assinaturaId, 50m, FinalidadePagamentoTreinador.Cadastro, Agora);

        result.IsSuccess.Should().BeTrue();
        var p = result.Value;
        p.Status.Should().Be(PagamentoStatus.Pendente);
        p.TreinadorId.Should().Be(treinadorId);
        p.AssinaturaTreinadorId.Should().Be(assinaturaId);
        p.Finalidade.Should().Be(FinalidadePagamentoTreinador.Cadastro);
        p.Valor.Should().Be(50m);
    }

    [Theory]
    [InlineData(false, true, 50)]
    [InlineData(true, false, 50)]
    [InlineData(true, true, 0)]
    public void Criar_DadosInvalidos_Falha(bool treinadorOk, bool assinaturaOk, decimal valor)
    {
        var result = PagamentoTreinador.Criar(
            treinadorOk ? Guid.NewGuid() : Guid.Empty,
            assinaturaOk ? Guid.NewGuid() : Guid.Empty,
            valor, FinalidadePagamentoTreinador.Renovacao, Agora);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DefinirDadosPix_Valido_SetaCampos()
    {
        var p = Novo();
        p.DefinirDadosPix("pi_1", "qr", "url", Agora.AddMinutes(30), Agora).IsSuccess.Should().BeTrue();
        p.StripePaymentIntentId.Should().Be("pi_1");
        p.PixQrCode.Should().Be("qr");
    }

    [Fact]
    public void DefinirDadosCartao_Valido_SetaCampos()
    {
        var p = Novo();
        p.DefinirDadosCartao("pi_1", "cs_1", Agora).IsSuccess.Should().BeTrue();
        p.StripePaymentIntentId.Should().Be("pi_1");
        p.ClientSecret.Should().Be("cs_1");
    }

    [Fact]
    public void DefinirDadosPix_PaymentIntentVazio_Falha()
    {
        var p = Novo();
        p.DefinirDadosPix("", "qr", "url", Agora, Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarPago_Pendente_DisparaEventoComFinalidadeEPlanoAlvo()
    {
        var planoAlvo = Guid.NewGuid();
        var p = Novo(FinalidadePagamentoTreinador.TrocaPlano, planoAlvo);

        p.MarcarPago(Agora).IsSuccess.Should().BeTrue();

        p.Status.Should().Be(PagamentoStatus.Pago);
        p.DataPagamento.Should().Be(Agora);
        var evt = p.DomainEvents.OfType<PagamentoTreinadorPagoEvent>().Should().ContainSingle().Subject;
        evt.Finalidade.Should().Be(FinalidadePagamentoTreinador.TrocaPlano);
        evt.PlanoAlvoId.Should().Be(planoAlvo);
        evt.TreinadorId.Should().Be(p.TreinadorId);
    }

    [Fact]
    public void MarcarPago_NaoPendente_Falha()
    {
        var p = Novo();
        p.MarcarPago(Agora);
        p.MarcarPago(Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarPago_LimpaClientSecret_PreservaPaymentIntentId()
    {
        var p = Novo();
        p.DefinirDadosCartao("pi_1", "cs_1", Agora);

        p.MarcarPago(Agora);

        p.ClientSecret.Should().BeNull();
        p.StripePaymentIntentId.Should().Be("pi_1");
    }

    [Fact]
    public void MarcarFalhou_LimpaQrCodePix()
    {
        var p = Novo();
        p.DefinirDadosPix("pi_1", "qr", "url", Agora.AddMinutes(30), Agora);

        p.MarcarFalhou(Agora);

        p.PixQrCode.Should().BeNull();
        p.PixQrCodeUrl.Should().BeNull();
    }

    [Fact]
    public void MarcarFalhou_Pendente_Ok_DepoisFalha()
    {
        var p = Novo();
        p.MarcarFalhou(Agora).IsSuccess.Should().BeTrue();
        p.Status.Should().Be(PagamentoStatus.Falhou);
        p.MarcarFalhou(Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarExpirado_Pendente_Ok()
    {
        var p = Novo();
        p.MarcarExpirado(Agora).IsSuccess.Should().BeTrue();
        p.Status.Should().Be(PagamentoStatus.Expirado);
    }

    [Fact]
    public void MarcarEstornado_DePago_Ok()
    {
        var p = Novo();
        p.MarcarPago(Agora);
        p.MarcarEstornado(Agora).IsSuccess.Should().BeTrue();
        p.Status.Should().Be(PagamentoStatus.Estornado);
    }

    [Fact]
    public void MarcarEstornado_DePendente_Falha()
    {
        var p = Novo();
        p.MarcarEstornado(Agora).IsFailure.Should().BeTrue();
        p.Status.Should().Be(PagamentoStatus.Pendente);
    }

    [Fact]
    public void MarcarEstornado_JaEstornado_Falha()
    {
        var p = Novo();
        p.MarcarPago(Agora);
        p.MarcarEstornado(Agora);
        p.MarcarEstornado(Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarcarEmDisputa_DePago_Ok()
    {
        var p = Novo();
        p.MarcarPago(Agora);
        p.MarcarEmDisputa(Agora).IsSuccess.Should().BeTrue();
        p.Status.Should().Be(PagamentoStatus.EmDisputa);
    }

    [Fact]
    public void MarcarEmDisputa_DePendente_Falha()
    {
        var p = Novo();
        p.MarcarEmDisputa(Agora).IsFailure.Should().BeTrue();
        p.Status.Should().Be(PagamentoStatus.Pendente);
    }

    [Fact]
    public void MarcarEmDisputa_JaEmDisputa_Falha()
    {
        var p = Novo();
        p.MarcarPago(Agora);
        p.MarcarEmDisputa(Agora);
        p.MarcarEmDisputa(Agora).IsFailure.Should().BeTrue();
    }
}
