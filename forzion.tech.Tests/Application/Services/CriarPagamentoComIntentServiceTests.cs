using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Services;

public class CriarPagamentoComIntentServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDbContextTransactionProvider> _transactionProvider = new();
    private readonly Mock<IDatabaseErrorInspector> _errorInspector = new();
    private readonly CriarPagamentoComIntentService _service;

    private sealed class NoopTransaction : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public CriarPagamentoComIntentServiceTests()
    {
        _transactionProvider.Setup(p => p.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NoopTransaction());

        _service = new CriarPagamentoComIntentService(
            _unitOfWork.Object, _transactionProvider.Object, _errorInspector.Object, TimeProvider.System,
            Mock.Of<ILogger<CriarPagamentoComIntentService>>());
    }

    private static AssinaturaTreinador CriarAssinaturaAtiva()
    {
        var a = AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, DateTime.UtcNow).Value;
        a.Ativar(DateTime.UtcNow);
        return a;
    }

    private static CriarPagamentoComIntentParams<PagamentoTreinador> BuildParams(
        AssinaturaTreinador assinatura,
        PagamentoTreinador? pendente = null,
        bool stripeThrows = false,
        Func<PagamentoTreinador, PagamentoTreinador?>? verificarIdempotencia = null,
        Mock<IPagamentoTreinadorRepository>? pagamentoRepo = null,
        Action? onCriarIntent = null)
    {
        var repo = pagamentoRepo ?? new Mock<IPagamentoTreinadorRepository>();
        repo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendente);

        return new CriarPagamentoComIntentParams<PagamentoTreinador>(
            CriarIntent: _ =>
            {
                onCriarIntent?.Invoke();
                if (stripeThrows)
                    throw new InvalidOperationException("Stripe indisponível");
                return Task.CompletedTask;
            },
            ObterPendente: ct => repo.Object.ObterPendentePorAssinaturaAsync(assinatura.Id, ct),
            VerificarIdempotencia: verificarIdempotencia ?? (p => p.StripePaymentIntentId is not null ? p : null),
            CriarPagamento: () => PagamentoTreinador.Criar(
                assinatura.TreinadorId, assinatura.Id, assinatura.Valor,
                FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow),
            AplicarIntent: pag => pag.DefinirDadosPix("pi_test", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow),
            AdicionarAsync: (pag, ct) => repo.Object.AdicionarAsync(pag, ct)
        )
        { MarcarFalhou = (pag, agora) => pag.MarcarFalhou(agora) };
    }

    [Fact]
    public async Task ExecutarAsync_SemPendente_CriaPagamentoComIntentEPersiste()
    {
        var assinatura = CriarAssinaturaAtiva();
        var pagamentoRepo = new Mock<IPagamentoTreinadorRepository>();
        pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        PagamentoTreinador? adicionado = null;
        pagamentoRepo.Setup(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()))
            .Callback<PagamentoTreinador, CancellationToken>((p, _) => adicionado = p);

        var p = BuildParams(assinatura, pagamentoRepo: pagamentoRepo);

        var result = await _service.ExecutarAsync(p);

        result.IsSuccess.Should().BeTrue();
        adicionado.Should().NotBeNull();
        adicionado!.StripePaymentIntentId.Should().NotBeNullOrEmpty("persiste só após Stripe retornar intent (G-PAY-1)");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecutarAsync_PendenteZumbi_MarcaFalhouECriaNovoIntent()
    {
        var assinatura = CriarAssinaturaAtiva();
        var zumbi = PagamentoTreinador.Criar(assinatura.TreinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;

        var pagamentoRepo = new Mock<IPagamentoTreinadorRepository>();
        pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(zumbi);

        var p = BuildParams(assinatura, pendente: zumbi, pagamentoRepo: pagamentoRepo);

        var result = await _service.ExecutarAsync(p);

        result.IsSuccess.Should().BeTrue();
        zumbi.Status.Should().Be(PagamentoStatus.Falhou, "zumbi deve ser marcado como falhou antes de criar novo");
        result.Value.Id.Should().NotBe(zumbi.Id, "novo pagamento deve ser criado");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecutarAsync_PendenteIdempotente_RetornaExistenteSemNovasChamadas()
    {
        var assinatura = CriarAssinaturaAtiva();
        var existente = PagamentoTreinador.Criar(assinatura.TreinadorId, assinatura.Id, 50m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        existente.DefinirDadosPix("pi_existente", "qr", "url", DateTime.UtcNow.AddHours(1), DateTime.UtcNow);

        var pagamentoRepo = new Mock<IPagamentoTreinadorRepository>();
        pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existente);

        var p = BuildParams(assinatura, pendente: existente, pagamentoRepo: pagamentoRepo);

        var result = await _service.ExecutarAsync(p);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existente.Id, "idempotência: retorna o pagamento existente");
        pagamentoRepo.Verify(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecutarAsync_StripeLanca_NaoPersisteNenhumPagamento()
    {
        var assinatura = CriarAssinaturaAtiva();
        var pagamentoRepo = new Mock<IPagamentoTreinadorRepository>();
        pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var p = BuildParams(assinatura, stripeThrows: true, pagamentoRepo: pagamentoRepo);

        var act = async () => await _service.ExecutarAsync(p);
        await act.Should().ThrowAsync<InvalidOperationException>();

        pagamentoRepo.Verify(r => r.AdicionarAsync(It.IsAny<PagamentoTreinador>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecutarAsync_CriaIntentAntesDeAbrirTransacao()
    {
        var assinatura = CriarAssinaturaAtiva();
        var pagamentoRepo = new Mock<IPagamentoTreinadorRepository>();
        pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var ordem = new List<string>();
        _transactionProvider.Setup(t => t.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                ordem.Add("tx");
                return Task.FromResult<ITransaction>(new NoopTransaction());
            });

        var p = BuildParams(assinatura, pagamentoRepo: pagamentoRepo, onCriarIntent: () => ordem.Add("intent"));

        await _service.ExecutarAsync(p);

        ordem.IndexOf("intent").Should().BeLessThan(ordem.IndexOf("tx"),
            "PERF-01: chamada Stripe ocorre fora da transação Serializable");
    }

    [Fact]
    public async Task ExecutarAsync_ConflitoSerializacao_RetentaEConclui()
    {
        var assinatura = CriarAssinaturaAtiva();
        var pagamentoRepo = new Mock<IPagamentoTreinadorRepository>();
        pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var conflito = new InvalidOperationException("serialization_failure");
        _errorInspector.Setup(i => i.EhConflitoDeSerializacao(conflito)).Returns(true);

        var commits = 0;
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                commits++;
                return commits == 1 ? throw conflito : Task.CompletedTask;
            });

        var criarIntentCalls = 0;
        var p = BuildParams(assinatura, pagamentoRepo: pagamentoRepo, onCriarIntent: () => criarIntentCalls++);

        var result = await _service.ExecutarAsync(p);

        result.IsSuccess.Should().BeTrue();
        commits.Should().Be(2, "primeira tentativa falha com serialization_failure, segunda conclui");
        criarIntentCalls.Should().Be(1, "intent criado uma vez antes do loop de retry, não recriado por tentativa");
    }

    [Fact]
    public async Task ExecutarAsync_UsaTransacaoSerialized_SempPreBeginTransaction()
    {
        var assinatura = CriarAssinaturaAtiva();
        var pagamentoRepo = new Mock<IPagamentoTreinadorRepository>();
        pagamentoRepo.Setup(r => r.ObterPendentePorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagamentoTreinador?)null);

        var p = BuildParams(assinatura, pagamentoRepo: pagamentoRepo);
        await _service.ExecutarAsync(p);

        _transactionProvider.Verify(t => t.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, It.IsAny<CancellationToken>()), Times.Once,
            "G-PAY-1 exige transação Serializable para proteger contra concorrência");
    }
}
