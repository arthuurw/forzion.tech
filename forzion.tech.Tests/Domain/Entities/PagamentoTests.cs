using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class PagamentoTests
{
    private static readonly Guid AssinaturaAlunoId = Guid.NewGuid();
    private const decimal Valor = 150m;

    private static Pagamento CriarValido() => Pagamento.Criar(AssinaturaAlunoId, Valor, TestData.Agora).Value;

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
    public void Criar_DispatchaPagamentoCriadoEvent()
    {
        var p = Pagamento.Criar(AssinaturaAlunoId, Valor, TestData.Agora, MetodoPagamento.Cartao).Value;

        p.DomainEvents.Should().ContainSingle();
        var evento = p.DomainEvents.OfType<PagamentoCriadoEvent>().Single();
        evento.PagamentoId.Should().Be(p.Id);
        evento.AssinaturaAlunoId.Should().Be(AssinaturaAlunoId);
        evento.Valor.Should().Be(Valor);
        evento.MetodoPagamento.Should().Be(MetodoPagamento.Cartao);
        evento.OcorridoEm.Should().Be(TestData.Agora);
    }

    [Fact]
    public void Criar_AssinaturaAlunoIdVazio_LancaDomainException()
    {
        var r = Pagamento.Criar(Guid.Empty, Valor, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador da assinatura é inválido.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_ValorInvalido_LancaDomainException(decimal valor)
    {
        var r = Pagamento.Criar(AssinaturaAlunoId, valor, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O valor do pagamento deve ser maior que zero.");
    }

    // --- DefinirDadosPix ---

    [Fact]
    public void DefinirDadosPix_DadosValidos_Salva()
    {
        var p = CriarValido();
        var expiracao = TestData.Agora.AddHours(1);

        p.DefinirDadosPix("pi_123", "qrcode_data", "https://img.url", expiracao, TestData.Agora);

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
        var r = p.DefinirDadosPix(paymentIntentId, "qr", "url", TestData.Agora, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do PaymentIntent é inválido.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DefinirDadosPix_QrCodeVazio_LancaDomainException(string qrCode)
    {
        var p = CriarValido();
        var r = p.DefinirDadosPix("pi_123", qrCode, "url", TestData.Agora, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O QR code Pix é inválido.");
    }

    // --- MarcarPago ---

    [Fact]
    public void MarcarPago_StatusPendente_MudaParaPago()
    {
        var p = CriarValido();
        p.MarcarPago(TestData.Agora);
        p.Status.Should().Be(PagamentoStatus.Pago);
        p.DataPagamento.Should().NotBeNull();
        p.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarcarPago_StatusNaoPendente_LancaDomainException()
    {
        var p = CriarValido();
        p.MarcarPago(TestData.Agora);
        var r = p.MarcarPago(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas pagamentos pendentes podem ser marcados como pagos.");
    }

    [Fact]
    public void MarcarPago_LimpaClientSecret_PreservaPaymentIntentId()
    {
        var p = CriarValido();
        p.DefinirDadosCartao("pi_123", "cs_secret", TestData.Agora);

        p.MarcarPago(TestData.Agora);

        p.ClientSecret.Should().BeNull();
        p.StripePaymentIntentId.Should().Be("pi_123");
    }

    [Fact]
    public void MarcarFalhou_LimpaQrCodePix()
    {
        var p = CriarValido();
        p.DefinirDadosPix("pi_123", "qr", "https://img.url", TestData.Agora.AddHours(1), TestData.Agora);

        p.MarcarFalhou(TestData.Agora);

        p.PixQrCode.Should().BeNull();
        p.PixQrCodeUrl.Should().BeNull();
    }

    [Fact]
    public void MarcarExpirado_LimpaQrCodePix()
    {
        var p = CriarValido();
        p.DefinirDadosPix("pi_123", "qr", "https://img.url", TestData.Agora.AddHours(1), TestData.Agora);

        p.MarcarExpirado(TestData.Agora);

        p.PixQrCode.Should().BeNull();
        p.PixQrCodeUrl.Should().BeNull();
    }

    // --- MarcarFalhou ---

    [Fact]
    public void MarcarFalhou_StatusPendente_MudaParaFalhou()
    {
        var p = CriarValido();
        p.MarcarFalhou(TestData.Agora);
        p.Status.Should().Be(PagamentoStatus.Falhou);
        p.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarcarFalhou_StatusNaoPendente_LancaDomainException()
    {
        var p = CriarValido();
        p.MarcarFalhou(TestData.Agora);
        var r = p.MarcarFalhou(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas pagamentos pendentes podem ser marcados como falhou.");
    }

    // --- MarcarExpirado ---

    [Fact]
    public void MarcarExpirado_StatusPendente_MudaParaExpirado()
    {
        var p = CriarValido();
        p.MarcarExpirado(TestData.Agora);
        p.Status.Should().Be(PagamentoStatus.Expirado);
        p.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarcarExpirado_StatusNaoPendente_LancaDomainException()
    {
        var p = CriarValido();
        p.MarcarExpirado(TestData.Agora);
        var r = p.MarcarExpirado(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas pagamentos pendentes podem ser marcados como expirados.");
    }

    // --- MarcarEstornado ---

    [Fact]
    public void MarcarEstornado_StatusPago_MudaParaEstornado()
    {
        var p = CriarValido();
        p.MarcarPago(TestData.Agora);
        p.ClearDomainEvents();

        p.MarcarEstornado(TestData.Agora);

        p.Status.Should().Be(PagamentoStatus.Estornado);
        p.UpdatedAt.Should().NotBeNull();
        // Auditoria: data de pagamento original é preservada como registro histórico.
        p.DataPagamento.Should().NotBeNull();
    }

    [Fact]
    public void MarcarEstornado_DispatchaPagamentoEstornadoEvent()
    {
        var p = CriarValido();
        p.MarcarPago(TestData.Agora);
        p.ClearDomainEvents();

        p.MarcarEstornado(TestData.Agora);

        p.DomainEvents.Should().ContainSingle();
        var evento = p.DomainEvents.OfType<PagamentoEstornadoEvent>().Single();
        evento.PagamentoId.Should().Be(p.Id);
        evento.AssinaturaAlunoId.Should().Be(AssinaturaAlunoId);
        evento.Valor.Should().Be(Valor);
    }

    [Theory]
    [InlineData(PagamentoStatus.Pendente)]
    [InlineData(PagamentoStatus.Falhou)]
    [InlineData(PagamentoStatus.Expirado)]
    public void MarcarEstornado_StatusNaoPago_LancaDomainException(PagamentoStatus statusInicial)
    {
        var p = CriarValido();
        switch (statusInicial)
        {
            case PagamentoStatus.Pendente:
                break;
            case PagamentoStatus.Falhou:
                p.MarcarFalhou(TestData.Agora);
                break;
            case PagamentoStatus.Expirado:
                p.MarcarExpirado(TestData.Agora);
                break;
        }

        var r = p.MarcarEstornado(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas pagamentos pagos podem ser estornados.");
    }

    [Fact]
    public void MarcarEstornado_JaEstornado_LancaDomainException()
    {
        var p = CriarValido();
        p.MarcarPago(TestData.Agora);
        p.MarcarEstornado(TestData.Agora);

        var r = p.MarcarEstornado(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas pagamentos pagos podem ser estornados.");
    }

    // --- MarcarEmDisputa (chargeback) ---

    [Fact]
    public void MarcarEmDisputa_StatusPago_TransicionaParaEmDisputa()
    {
        var p = CriarValido();
        p.MarcarPago(TestData.Agora);
        p.ClearDomainEvents();

        p.MarcarEmDisputa("fraudulent", TestData.Agora);

        p.Status.Should().Be(PagamentoStatus.EmDisputa);
        p.UpdatedAt.Should().NotBeNull();
        // DataPagamento é registro histórico do recebimento — não pode ser apagada.
        p.DataPagamento.Should().NotBeNull();
    }

    [Fact]
    public void MarcarEmDisputa_DispatchaPagamentoEmDisputaEventComMotivo()
    {
        var p = CriarValido();
        p.MarcarPago(TestData.Agora);
        p.ClearDomainEvents();

        p.MarcarEmDisputa("fraudulent", TestData.Agora);

        p.DomainEvents.Should().ContainSingle();
        var evento = p.DomainEvents.OfType<PagamentoEmDisputaEvent>().Single();
        evento.PagamentoId.Should().Be(p.Id);
        evento.AssinaturaAlunoId.Should().Be(AssinaturaAlunoId);
        evento.Valor.Should().Be(Valor);
        evento.MotivoDisputa.Should().Be("fraudulent");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarcarEmDisputa_MotivoVazio_NormalizaParaUnknown(string? motivo)
    {
        var p = CriarValido();
        p.MarcarPago(TestData.Agora);
        p.ClearDomainEvents();

        p.MarcarEmDisputa(motivo!, TestData.Agora);

        p.DomainEvents.OfType<PagamentoEmDisputaEvent>().Single()
            .MotivoDisputa.Should().Be("unknown");
    }

    [Theory]
    [InlineData(PagamentoStatus.Pendente)]
    [InlineData(PagamentoStatus.Falhou)]
    [InlineData(PagamentoStatus.Expirado)]
    public void MarcarEmDisputa_StatusNaoPago_LancaDomainException(PagamentoStatus statusInicial)
    {
        var p = CriarValido();
        switch (statusInicial)
        {
            case PagamentoStatus.Pendente:
                break;
            case PagamentoStatus.Falhou:
                p.MarcarFalhou(TestData.Agora);
                break;
            case PagamentoStatus.Expirado:
                p.MarcarExpirado(TestData.Agora);
                break;
        }

        var r = p.MarcarEmDisputa("fraudulent", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas pagamentos pagos podem ser marcados em disputa.");
    }

    [Fact]
    public void MarcarEmDisputa_JaEmDisputa_LancaDomainException()
    {
        // Idempotência ao nível do handler — domain enforce uma única transição.
        var p = CriarValido();
        p.MarcarPago(TestData.Agora);
        p.MarcarEmDisputa("fraudulent", TestData.Agora);

        var r = p.MarcarEmDisputa("duplicate", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas pagamentos pagos podem ser marcados em disputa.");
    }

    // --- EstaVencido ---

    [Fact]
    public void EstaVencido_PixAposExpiracao_True()
    {
        var p = Pagamento.Criar(AssinaturaAlunoId, Valor, TestData.Agora, MetodoPagamento.Pix).Value;
        p.DefinirDadosPix("pi_123", "qr", "url", TestData.Agora.AddMinutes(15), TestData.Agora);

        p.EstaVencido(TestData.Agora.AddMinutes(15)).Should().BeTrue();
    }

    [Fact]
    public void EstaVencido_PixAntesExpiracao_False()
    {
        var p = Pagamento.Criar(AssinaturaAlunoId, Valor, TestData.Agora, MetodoPagamento.Pix).Value;
        p.DefinirDadosPix("pi_123", "qr", "url", TestData.Agora.AddMinutes(15), TestData.Agora);

        p.EstaVencido(TestData.Agora.AddMinutes(14)).Should().BeFalse();
    }

    [Fact]
    public void EstaVencido_CartaoAposJanela_True()
    {
        var p = Pagamento.Criar(AssinaturaAlunoId, Valor, TestData.Agora, MetodoPagamento.Cartao).Value;

        p.EstaVencido(TestData.Agora.AddHours(Pagamento.JanelaValidadeCartaoHoras)).Should().BeTrue();
    }

    [Fact]
    public void EstaVencido_CartaoDentroDaJanela_False()
    {
        var p = Pagamento.Criar(AssinaturaAlunoId, Valor, TestData.Agora, MetodoPagamento.Cartao).Value;

        p.EstaVencido(TestData.Agora.AddHours(Pagamento.JanelaValidadeCartaoHoras - 1)).Should().BeFalse();
    }
}
