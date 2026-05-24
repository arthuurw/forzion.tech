using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class PagamentoTests
{
    private static readonly Guid AssinaturaAlunoId = Guid.NewGuid();
    private const decimal Valor = 150m;

    private static Pagamento CriarValido() => Pagamento.Criar(AssinaturaAlunoId, Valor);

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaPagamentoPendente()
    {
        var p = CriarValido();

        p.Id.Should().NotBeEmpty();
        p.AssinaturaAlunoId.Should().Be(AssinaturaAlunoId);
        p.Valor.Should().Be(Valor);
        p.Status.Should().Be(PagamentoStatus.Pendente);
        p.StripePaymentIntentId.Should().BeNull();
        p.PixQrCode.Should().BeNull();
        p.DataPagamento.Should().BeNull();
    }

    [Fact]
    public void Criar_AssinaturaAlunoIdVazio_LancaDomainException()
    {
        var act = () => Pagamento.Criar(Guid.Empty, Valor);
        act.Should().Throw<DomainException>().WithMessage("O identificador da assinatura é inválido.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_ValorInvalido_LancaDomainException(decimal valor)
    {
        var act = () => Pagamento.Criar(AssinaturaAlunoId, valor);
        act.Should().Throw<DomainException>().WithMessage("O valor do pagamento deve ser maior que zero.");
    }

    // --- DefinirDadosPix ---

    [Fact]
    public void DefinirDadosPix_DadosValidos_Salva()
    {
        var p = CriarValido();
        var expiracao = DateTime.UtcNow.AddHours(1);

        p.DefinirDadosPix("pi_123", "qrcode_data", "https://img.url", expiracao);

        p.StripePaymentIntentId.Should().Be("pi_123");
        p.PixQrCode.Should().Be("qrcode_data");
        p.PixQrCodeUrl.Should().Be("https://img.url");
        p.PixExpiracao.Should().Be(expiracao);
        p.UpdatedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DefinirDadosPix_PaymentIntentIdVazio_LancaDomainException(string paymentIntentId)
    {
        var p = CriarValido();
        var act = () => p.DefinirDadosPix(paymentIntentId, "qr", "url", DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("O identificador do PaymentIntent é inválido.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DefinirDadosPix_QrCodeVazio_LancaDomainException(string qrCode)
    {
        var p = CriarValido();
        var act = () => p.DefinirDadosPix("pi_123", qrCode, "url", DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("O QR code Pix é inválido.");
    }

    // --- MarcarPago ---

    [Fact]
    public void MarcarPago_StatusPendente_MudaParaPago()
    {
        var p = CriarValido();
        p.MarcarPago();
        p.Status.Should().Be(PagamentoStatus.Pago);
        p.DataPagamento.Should().NotBeNull();
        p.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarcarPago_StatusNaoPendente_LancaDomainException()
    {
        var p = CriarValido();
        p.MarcarPago();
        var act = () => p.MarcarPago();
        act.Should().Throw<DomainException>().WithMessage("Apenas pagamentos pendentes podem ser marcados como pagos.");
    }

    // --- MarcarFalhou ---

    [Fact]
    public void MarcarFalhou_StatusPendente_MudaParaFalhou()
    {
        var p = CriarValido();
        p.MarcarFalhou();
        p.Status.Should().Be(PagamentoStatus.Falhou);
        p.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarcarFalhou_StatusNaoPendente_LancaDomainException()
    {
        var p = CriarValido();
        p.MarcarFalhou();
        var act = () => p.MarcarFalhou();
        act.Should().Throw<DomainException>().WithMessage("Apenas pagamentos pendentes podem ser marcados como falhou.");
    }

    // --- MarcarExpirado ---

    [Fact]
    public void MarcarExpirado_StatusPendente_MudaParaExpirado()
    {
        var p = CriarValido();
        p.MarcarExpirado();
        p.Status.Should().Be(PagamentoStatus.Expirado);
        p.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarcarExpirado_StatusNaoPendente_LancaDomainException()
    {
        var p = CriarValido();
        p.MarcarExpirado();
        var act = () => p.MarcarExpirado();
        act.Should().Throw<DomainException>().WithMessage("Apenas pagamentos pendentes podem ser marcados como expirados.");
    }
}
