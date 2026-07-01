using FluentAssertions;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public partial class ProcessarWebhookStripeHandlerTests
{
    [Fact]
    public async Task ProcessarEventoAsync_PaymentIntentSucceeded_ConnectDrift_ContaSemConnectAccount_Lanca()
    {
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_drift", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        var conta = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_drift", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var evento = new StripeWebhookEvento("payment_intent.succeeded", "pi_drift", "acct_evt", false);
        var act = async () => await _handler.ProcessarEventoAsync(evento);

        await act.Should().ThrowAsync<InvalidOperationException>();
        pagamento.Status.Should().Be(PagamentoStatus.Pendente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessarEventoAsync_PaymentIntentSucceeded_ConnectMismatch_JaConsistente()
    {
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_mismatch", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        var conta = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        conta.ConfigurarStripeConnect("acct_correct", TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_mismatch", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var evento = new StripeWebhookEvento("payment_intent.succeeded", "pi_mismatch", "acct_attacker", false);
        var outcome = await _handler.ProcessarEventoAsync(evento);

        outcome.Should().Be(ProcessarEventoResultado.JaConsistente);
        pagamento.Status.Should().Be(PagamentoStatus.Pendente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessarEventoAsync_PaymentIntentSucceeded_ConnectConsistente_Aplicado()
    {
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_match", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), treinadorId, Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        var conta = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        conta.ConfigurarStripeConnect("acct_match", TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_match", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var evento = new StripeWebhookEvento("payment_intent.succeeded", "pi_match", "acct_match", false);
        var outcome = await _handler.ProcessarEventoAsync(evento);

        outcome.Should().Be(ProcessarEventoResultado.Aplicado);
        pagamento.Status.Should().Be(PagamentoStatus.Pago);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarEventoAsync_PaymentIntentSucceeded_AssinaturaCancelada_ReembolsaAntesDeEstornar_Aplicado()
    {
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_cancel_order", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        assinatura.Cancelar(TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_cancel_order", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        PagamentoStatus? statusAoReembolsar = null;
        _stripeService.Setup(s => s.CriarReembolsoAsync(It.IsAny<Guid>(), "pi_cancel_order", true, It.IsAny<CancellationToken>()))
            .Callback(() => statusAoReembolsar = pagamento.Status)
            .Returns(Task.CompletedTask);

        var evento = new StripeWebhookEvento("payment_intent.succeeded", "pi_cancel_order", null, false);
        var outcome = await _handler.ProcessarEventoAsync(evento);

        outcome.Should().Be(ProcessarEventoResultado.Aplicado);
        statusAoReembolsar.Should().Be(PagamentoStatus.Pago, "reembolso precede MarcarEstornado");
        pagamento.Status.Should().Be(PagamentoStatus.Estornado);
        _stripeService.Verify(s => s.CriarReembolsoAsync(It.IsAny<Guid>(), "pi_cancel_order", true, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarEventoAsync_PaymentIntentSucceeded_AssinaturaCancelada_ReembolsoFalha_NaoEstornaNemComita()
    {
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 150m, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_cancel_fail", "qr", "url", DateTime.UtcNow.AddHours(1), TestData.Agora);
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        assinatura.Cancelar(TestData.Agora);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_cancel_fail", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _stripeService.Setup(s => s.CriarReembolsoAsync(It.IsAny<Guid>(), "pi_cancel_fail", true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("stripe down"));

        var evento = new StripeWebhookEvento("payment_intent.succeeded", "pi_cancel_fail", null, false);
        var act = async () => await _handler.ProcessarEventoAsync(evento);

        await act.Should().ThrowAsync<InvalidOperationException>();
        pagamento.Status.Should().Be(PagamentoStatus.Pago);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
