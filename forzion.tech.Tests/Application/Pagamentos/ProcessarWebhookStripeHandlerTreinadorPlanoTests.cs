using FluentAssertions;
using forzion.tech.Application.Outbox;
using forzion.tech.Application.UseCases.Pagamentos.ProcessarWebhookStripe;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.Builders;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public partial class ProcessarWebhookStripeHandlerTests
{
    [Fact]
    public async Task ProcessarEstornoTreinadorAsync_StatusFalhou_JaConsistenteSemComitarSemLancar()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t_estorno_falhou", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        pagamento.MarcarFalhou(DateTime.UtcNow);

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_t_estorno_falhou", It.IsAny<CancellationToken>())).ReturnsAsync((Pagamento?)null);
        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_t_estorno_falhou", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeRefundedTreinadorPayload("pi_t_estorno_falhou", 5000), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Falhou);
        _assinaturaTreinadorRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChargeDisputeCreated_Aluno_ContaNula_EnfileiraComEmailDoAluno()
    {
        var alunoId = Guid.NewGuid();
        var inicio = DateTime.UtcNow.AddDays(-15);
        var assinaturaId = Guid.NewGuid();
        var pagamento = Pagamento.Criar(assinaturaId, 149.90m, inicio).Value;
        pagamento.DefinirDadosPix("pi_ev_fallback", "qr", "url", inicio.AddHours(1), inicio);
        pagamento.MarcarPago(inicio);
        pagamento.ClearDomainEvents();

        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), alunoId, 149.90m, inicio).Value;
        assinatura.Ativar(inicio);
        assinatura.ClearDomainEvents();

        var aluno = Aluno.Criar(Guid.NewGuid(), "Joana", inicio, "joana@aluno.com").Value;

        _pagamentoRepo.Setup(r => r.ObterPorPaymentIntentIdAsync("pi_ev_fallback", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _contaRepo.Setup(r => r.ObterPorIdAsync(aluno.ContaId, It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(ChargeDisputeCreatedPayload("pi_ev_fallback"), ValidSig));

        _enfileirador.Verify(e => e.Enfileirar(
            "fx:evidencia_disputa",
            It.Is<EvidenciaDisputaPayload>(p => p.Email == "joana@aluno.com" && p.PagamentoId == pagamento.Id),
            $"fx:evidencia_disputa:aluno:{pagamento.Id}"), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_PlanoTreinadorContratacao_AssinaturaNaoEncontrada_LancaSemComitar()
    {
        var treinadorId = Guid.NewGuid();
        var assinaturaId = Guid.NewGuid();
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinaturaId, 100m, FinalidadePagamentoTreinador.Contratacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_contrat_sem_assin", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_contrat_sem_assin", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaTreinadorRepo.Setup(r => r.ObterPorIdAsync(assinaturaId, It.IsAny<CancellationToken>())).ReturnsAsync((AssinaturaTreinador?)null);

        var act = async () => await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.succeeded", "pi_contrat_sem_assin"), ValidSig));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_PlanoTreinadorContratacao_AtivarFalha_LancaSemComitar()
    {
        var treinadorId = Guid.NewGuid();
        var assinatura = AssinaturaTreinador.Criar(treinadorId, Guid.NewGuid(), 100m, DateTime.UtcNow).Value;
        assinatura.Cancelar(DateTime.UtcNow);
        var pagamento = PagamentoTreinador.Criar(treinadorId, assinatura.Id, 100m, FinalidadePagamentoTreinador.Contratacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_contrat_ativar_falha", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_contrat_ativar_falha", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaTreinadorRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var act = async () => await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.succeeded", "pi_contrat_ativar_falha"), ValidSig));

        await act.Should().ThrowAsync<InvalidOperationException>();
        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Cancelada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentSucceeded_PlanoTreinadorCadastro_ContaNaoEncontrada_LancaSemComitar()
    {
        var contaId = Guid.NewGuid();
        var planoId = Guid.NewGuid();
        var treinador = Treinador.Criar(contaId, "Carlos", DateTime.UtcNow, null, planoId, ModoPagamentoAluno.Plataforma, aguardandoPagamento: true).Value;
        var assinatura = AssinaturaTreinador.Criar(treinador.Id, planoId, 50m, DateTime.UtcNow).Value;
        var pagamento = PagamentoTreinador.Criar(treinador.Id, assinatura.Id, 50m, FinalidadePagamentoTreinador.Cadastro, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_cad_sem_conta", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_cad_sem_conta", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaTreinadorRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var act = async () => await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.succeeded", "pi_cad_sem_conta"), ValidSig));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentCanceled_PlanoTreinador_Pendente_MarcaExpiradoEComita()
    {
        var treinadorId = Guid.NewGuid();
        var pagamento = PagamentoTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t_cancel", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_t_cancel", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.canceled", "pi_t_cancel"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Expirado);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaymentIntentCanceled_PlanoTreinador_JaFalhou_JaConsistenteSemComitar()
    {
        var treinadorId = Guid.NewGuid();
        var pagamento = PagamentoTreinador.Criar(treinadorId, Guid.NewGuid(), 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        pagamento.DefinirDadosPix("pi_t_cancel_falhou", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);
        pagamento.MarcarFalhou(DateTime.UtcNow);

        _pagamentoTreinadorRepo.Setup(r => r.ObterPorStripePaymentIntentIdAsync("pi_t_cancel_falhou", It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(PaymentIntentTreinadorPayload("payment_intent.canceled", "pi_t_cancel_falhou"), ValidSig));

        result.IsSuccess.Should().BeTrue();
        pagamento.Status.Should().Be(PagamentoStatus.Falhou);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AccountUpdatedChargesEnabled_ConfirmarOnboardingFalha_NaoComita()
    {
        var contaRecebimento = ContaRecebimento.Criar(Guid.NewGuid(), DateTime.UtcNow).Value;
        _contaRecebimentoRepo.Setup(r => r.ObterPorStripeAccountIdAsync("acct_sem_connect", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);

        var result = await _handler.HandleAsync(
            new ProcessarWebhookStripeCommand(AccountPayload("acct_sem_connect", true), ValidSig));

        result.IsSuccess.Should().BeTrue();
        contaRecebimento.OnboardingCompleto.Should().BeFalse();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
