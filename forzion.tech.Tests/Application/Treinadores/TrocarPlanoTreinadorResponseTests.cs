using FluentAssertions;
using forzion.tech.Application.UseCases.Treinadores.IniciarPagamentoPlano;
using forzion.tech.Application.UseCases.Treinadores.TrocarPlanoTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Tests.Application.Treinadores;

public class TrocarPlanoTreinadorResponseTests
{
    private static readonly DateTime Agora = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PagamentoTreinador PagamentoPix()
    {
        var p = PagamentoTreinador.Criar(
            Guid.NewGuid(), Guid.NewGuid(), 50m,
            FinalidadePagamentoTreinador.TrocaPlano, Agora, MetodoPagamento.Pix, Guid.NewGuid()).Value;
        p.DefinirDadosPix("pi_pix", "qrcode_val", "https://img", Agora.AddHours(1), Agora);
        return p;
    }

    private static PagamentoTreinador PagamentoCartao()
    {
        var p = PagamentoTreinador.Criar(
            Guid.NewGuid(), Guid.NewGuid(), 80m,
            FinalidadePagamentoTreinador.TrocaPlano, Agora, MetodoPagamento.Cartao, Guid.NewGuid()).Value;
        p.DefinirDadosCartao("pi_cartao", "secret_cartao", Agora);
        return p;
    }

    [Fact]
    public void Upgrade_MapeiaPropriedadesDePagamento()
    {
        var pagamento = PagamentoPix();
        var response = TrocarPlanoTreinadorResponse.Upgrade(pagamento);

        response.Tipo.Should().Be(TipoTrocaPlano.Upgrade);
        response.PagamentoId.Should().Be(pagamento.Id);
        response.ValorPagamento.Should().Be(pagamento.Valor);
        response.MetodoPagamento.Should().Be(MetodoPagamento.Pix);
        response.PixQrCode.Should().Be("qrcode_val");
        response.PixQrCodeUrl.Should().Be("https://img");
        response.PixExpiracao.Should().Be(Agora.AddHours(1));
        response.ClientSecret.Should().BeNull();
        response.DataEfetivacao.Should().BeNull();
    }

    [Fact]
    public void Downgrade_MapeiaDataEfetivacao()
    {
        var dataEfetivacao = Agora.AddDays(15);
        var response = TrocarPlanoTreinadorResponse.Downgrade(dataEfetivacao);

        response.Tipo.Should().Be(TipoTrocaPlano.Downgrade);
        response.PagamentoId.Should().BeNull();
        response.ValorPagamento.Should().BeNull();
        response.MetodoPagamento.Should().BeNull();
        response.DataEfetivacao.Should().Be(dataEfetivacao);
    }

    [Fact]
    public void UpgradeImediato_MapeiaDataEfetivacaoComoAgora()
    {
        var response = TrocarPlanoTreinadorResponse.UpgradeImediato(Agora);

        response.Tipo.Should().Be(TipoTrocaPlano.UpgradeImediato);
        response.PagamentoId.Should().BeNull();
        response.ValorPagamento.Should().BeNull();
        response.DataEfetivacao.Should().Be(Agora);
    }

    [Fact]
    public void Regularizacao_MapeiaPropriedadesDePagamento()
    {
        var pagamento = PagamentoPix();
        var response = TrocarPlanoTreinadorResponse.Regularizacao(pagamento);

        response.Tipo.Should().Be(TipoTrocaPlano.InadimplenteRegularizacao);
        response.PagamentoId.Should().Be(pagamento.Id);
        response.ValorPagamento.Should().Be(pagamento.Valor);
        response.DataEfetivacao.Should().BeNull();
    }

    [Fact]
    public void Upgrade_ComCartao_MapeiaClientSecret()
    {
        var pagamento = PagamentoCartao();
        var response = TrocarPlanoTreinadorResponse.Upgrade(pagamento);

        response.ClientSecret.Should().Be("secret_cartao");
        response.PixQrCode.Should().BeNull();
    }
}

public class IniciarPagamentoPlanoResponseTests
{
    private static readonly DateTime Agora = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void De_PagamentoPix_MapeiaTodasAsPropriedades()
    {
        var p = PagamentoTreinador.Criar(
            Guid.NewGuid(), Guid.NewGuid(), 99m,
            FinalidadePagamentoTreinador.Cadastro, Agora, MetodoPagamento.Pix).Value;
        p.DefinirDadosPix("pi_123", "qr_val", "https://qr", Agora.AddHours(2), Agora);

        var response = IniciarPagamentoPlanoResponse.De(p);

        response.PagamentoId.Should().Be(p.Id);
        response.Valor.Should().Be(99m);
        response.Status.Should().Be(PagamentoStatus.Pendente);
        response.MetodoPagamento.Should().Be(MetodoPagamento.Pix);
        response.StripePaymentIntentId.Should().Be("pi_123");
        response.PixQrCode.Should().Be("qr_val");
        response.PixQrCodeUrl.Should().Be("https://qr");
        response.PixExpiracao.Should().Be(Agora.AddHours(2));
        response.ClientSecret.Should().BeNull();
        response.CreatedAt.Should().Be(p.CreatedAt);
    }

    [Fact]
    public void De_PagamentoCartao_MapeiaClientSecret()
    {
        var p = PagamentoTreinador.Criar(
            Guid.NewGuid(), Guid.NewGuid(), 99m,
            FinalidadePagamentoTreinador.Cadastro, Agora, MetodoPagamento.Cartao).Value;
        p.DefinirDadosCartao("pi_cartao", "secret_abc", Agora);

        var response = IniciarPagamentoPlanoResponse.De(p);

        response.ClientSecret.Should().Be("secret_abc");
        response.StripePaymentIntentId.Should().Be("pi_cartao");
        response.PixQrCode.Should().BeNull();
    }
}
