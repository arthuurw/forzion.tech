using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Nfse;

public class EmitirNfseAssinaturaHandlerTests
{
    private static readonly DateTimeOffset Instante = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Agora = Instante.UtcDateTime;

    private readonly Mock<IPagamentoTreinadorRepository> _pagamentoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<INotaFiscalRepository> _notaRepo = new();
    private readonly Mock<IOutboxEnfileirador> _outbox = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(Instante);
    private readonly EmitirNfseAssinaturaHandler _handler;

    public EmitirNfseAssinaturaHandlerTests()
    {
        _handler = new EmitirNfseAssinaturaHandler(
            _pagamentoRepo.Object,
            _treinadorRepo.Object,
            _notaRepo.Object,
            _outbox.Object,
            _unitOfWork.Object,
            _time,
            Mock.Of<ILogger<EmitirNfseAssinaturaHandler>>());
    }

    [Fact]
    public async Task ComDadosFiscais_CriaNotaPendenteEEnfileira()
    {
        var treinador = CriarTreinador(comDadosFiscais: true);
        var pagamento = CriarPagamento(treinador.Id, 99.90m);
        NotaFiscal? notaSalva = null;
        _notaRepo.Setup(r => r.AdicionarAsync(It.IsAny<NotaFiscal>(), It.IsAny<CancellationToken>()))
            .Callback<NotaFiscal, CancellationToken>((n, _) => notaSalva = n)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(Evento(pagamento.Id, treinador.Id));

        notaSalva.Should().NotBeNull();
        notaSalva!.Status.Should().Be(NotaFiscalStatus.Pendente);
        notaSalva.Valor.Should().Be(99.90m);
        _outbox.Verify(o => o.Enfileirar(
            "fx:emitir_nfse",
            It.Is<EmitirNfsePayload>(p => p.NotaFiscalId == notaSalva.Id),
            $"fx:emitir_nfse:assinatura:{pagamento.Id}"), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SemDadosFiscais_MarcaBloqueadaENaoEnfileira()
    {
        var treinador = CriarTreinador(comDadosFiscais: false);
        var pagamento = CriarPagamento(treinador.Id, 50m);
        NotaFiscal? notaSalva = null;
        _notaRepo.Setup(r => r.AdicionarAsync(It.IsAny<NotaFiscal>(), It.IsAny<CancellationToken>()))
            .Callback<NotaFiscal, CancellationToken>((n, _) => notaSalva = n)
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(Evento(pagamento.Id, treinador.Id));

        notaSalva.Should().NotBeNull();
        notaSalva!.Status.Should().Be(NotaFiscalStatus.BloqueadaDadosFiscais);
        notaSalva.DomainEvents.Should().ContainSingle(e => e is NotaFiscalBloqueadaDadosFiscaisEvent);
        _outbox.Verify(o => o.Enfileirar(It.IsAny<string>(), It.IsAny<EmitirNfsePayload>(), It.IsAny<string>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotaJaExiste_NaoDuplica()
    {
        var treinador = CriarTreinador(comDadosFiscais: true);
        var pagamento = CriarPagamento(treinador.Id, 99.90m);
        var existente = NotaFiscal.CriarAssinatura(treinador.Id, pagamento.Id, 99.90m, Agora).Value;
        _notaRepo.Setup(r => r.ObterPorPagamentoTreinadorAsync(pagamento.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existente);

        await _handler.HandleAsync(Evento(pagamento.Id, treinador.Id));

        _notaRepo.Verify(r => r.AdicionarAsync(It.IsAny<NotaFiscal>(), It.IsAny<CancellationToken>()), Times.Never);
        _outbox.Verify(o => o.Enfileirar(It.IsAny<string>(), It.IsAny<EmitirNfsePayload>(), It.IsAny<string>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValorZero_NaoEmite()
    {
        var treinador = CriarTreinador(comDadosFiscais: true);
        var pagamento = CriarPagamento(treinador.Id, 1m);
        typeof(PagamentoTreinador).GetProperty(nameof(PagamentoTreinador.Valor))!.SetValue(pagamento, 0m);
        _pagamentoRepo.Setup(r => r.ObterPorIdAsync(pagamento.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);

        await _handler.HandleAsync(Evento(pagamento.Id, treinador.Id));

        _notaRepo.Verify(r => r.AdicionarAsync(It.IsAny<NotaFiscal>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PagamentoInexistente_NaoEmite()
    {
        var treinador = CriarTreinador(comDadosFiscais: true);
        var pagamentoId = Guid.NewGuid();
        _pagamentoRepo.Setup(r => r.ObterPorIdAsync(pagamentoId, It.IsAny<CancellationToken>())).ReturnsAsync((PagamentoTreinador?)null);

        await _handler.HandleAsync(Evento(pagamentoId, treinador.Id));

        _notaRepo.Verify(r => r.AdicionarAsync(It.IsAny<NotaFiscal>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private Treinador CriarTreinador(bool comDadosFiscais)
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Treinador X", Agora).Value;
        if (comDadosFiscais)
        {
            var endereco = EnderecoFiscal.Criar("Rua A", "100", "Centro", "3550308", "SP", "01001000").Value;
            var dados = DadosFiscais.Criar(TipoDocumentoFiscal.Cnpj, "11222333000181", "Treinador X LTDA", endereco).Value;
            treinador.DefinirDadosFiscais(dados, Agora);
        }

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        return treinador;
    }

    private PagamentoTreinador CriarPagamento(Guid treinadorId, decimal valor)
    {
        var pagamento = PagamentoTreinador.Criar(treinadorId, Guid.NewGuid(), valor, FinalidadePagamentoTreinador.Renovacao, Agora).Value;
        _pagamentoRepo.Setup(r => r.ObterPorIdAsync(pagamento.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        return pagamento;
    }

    private static PagamentoTreinadorPagoEvent Evento(Guid pagamentoId, Guid treinadorId) =>
        new(pagamentoId, treinadorId, Guid.NewGuid(), FinalidadePagamentoTreinador.Renovacao, null, Agora);
}
