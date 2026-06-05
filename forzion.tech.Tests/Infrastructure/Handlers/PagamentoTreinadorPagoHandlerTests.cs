using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Handlers;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Handlers;

public class PagamentoTreinadorPagoHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly PagamentoTreinadorPagoHandler _handler;

    public PagamentoTreinadorPagoHandlerTests()
    {
        _handler = new PagamentoTreinadorPagoHandler(
            _assinaturaRepo.Object, _treinadorRepo.Object, _contaRepo.Object,
            _unitOfWork.Object, TimeProvider.System, Mock.Of<ILogger<PagamentoTreinadorPagoHandler>>());
    }

    private (Conta conta, Treinador treinador, AssinaturaTreinador assinatura) Cenario()
    {
        var conta = Conta.Criar(Email.Criar("t@x.com").Value, "hash", TipoConta.Treinador, Agora, emitirRegistro: false).Value;
        var planoId = Guid.NewGuid();
        var treinador = Treinador.Criar(conta.Id, "Carlos", Agora, null, planoId, ModoPagamentoAluno.Plataforma, aguardandoPagamento: true).Value;
        var assinatura = AssinaturaTreinador.Criar(treinador.Id, planoId, 50m, Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        return (conta, treinador, assinatura);
    }

    private static PagamentoTreinadorPagoEvent Evento(Guid treinadorId, Guid assinaturaId, FinalidadePagamentoTreinador finalidade)
        => new(Guid.NewGuid(), treinadorId, assinaturaId, finalidade, null, Agora);

    [Fact]
    public async Task HandleAsync_Cadastro_AtivaAssinatura_ConfirmaPlano_EmiteVerificacao()
    {
        var (conta, treinador, assinatura) = Cenario();

        await _handler.HandleAsync(Evento(treinador.Id, assinatura.Id, FinalidadePagamentoTreinador.Cadastro));

        assinatura.Status.Should().Be(AssinaturaTreinadorStatus.Ativa);
        treinador.Status.Should().Be(TreinadorStatus.AguardandoAprovacao);
        conta.DomainEvents.OfType<ContaRegistradaEvent>().Should().ContainSingle("verificação só agora, após o pagamento");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FinalidadeNaoCadastro_NoOp()
    {
        var (_, treinador, assinatura) = Cenario();

        await _handler.HandleAsync(Evento(treinador.Id, assinatura.Id, FinalidadePagamentoTreinador.Renovacao));

        treinador.Status.Should().Be(TreinadorStatus.AguardandoPagamento);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
