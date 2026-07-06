using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Application.Settings;
using forzion.tech.Application.UseCases.Nfse.GerarNfseComissaoMensal;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Nfse;

public class GerarNfseComissaoMensalHandlerTests
{
    private static readonly DateTimeOffset Instante = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<INotaFiscalRepository> _notaRepo = new();
    private readonly Mock<IOutboxEnfileirador> _enfileirador = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDatabaseErrorInspector> _dbErrorInspector = new();
    private readonly FakeTimeProvider _time = new(Instante);
    private readonly GerarNfseComissaoMensalHandler _handler;

    private static readonly DateOnly Inicio = new(2026, 5, 1);
    private static readonly DateOnly Fim = new(2026, 5, 31);

    public GerarNfseComissaoMensalHandlerTests()
    {
        var scopeFactory = new ServiceCollection()
            .AddSingleton(_notaRepo.Object)
            .AddSingleton(_enfileirador.Object)
            .AddSingleton(_unitOfWork.Object)
            .AddSingleton(_dbErrorInspector.Object)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        _handler = new GerarNfseComissaoMensalHandler(
            _pagamentoRepo.Object,
            _notaRepo.Object,
            scopeFactory,
            Options.Create(new PaymentSettings { TaxaPlataformaPercent = 10m }),
            _time,
            Mock.Of<ILogger<GerarNfseComissaoMensalHandler>>());
    }

    private void SetupLote(params ComissaoTreinadorPeriodo[] itens) =>
        _pagamentoRepo.Setup(r => r.ListarComissaoPorTreinadorNoPeriodoAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), 10m, It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(itens);

    private static GerarNfseComissaoMensalCommand Cmd() => new(Inicio, Fim);

    [Fact]
    public async Task ComFee_CriaNotaComissaoEEnfileira()
    {
        var treinadorId = Guid.NewGuid();
        SetupLote(new ComissaoTreinadorPeriodo(treinadorId, 690m));
        _notaRepo.Setup(r => r.ListarTreinadoresComComissaoAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Inicio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        NotaFiscal? adicionada = null;
        _notaRepo.Setup(r => r.AdicionarAsync(It.IsAny<NotaFiscal>(), It.IsAny<CancellationToken>()))
            .Callback<NotaFiscal, CancellationToken>((n, _) => adicionada = n);
        string? chave = null;
        _enfileirador.Setup(e => e.Enfileirar("fx:emitir_nfse", It.IsAny<EmitirNfsePayload>(), It.IsAny<string>()))
            .Callback<string, EmitirNfsePayload, string>((_, _, c) => chave = c);

        var result = await _handler.HandleAsync(Cmd());

        result.IsSuccess.Should().BeTrue();
        result.Value.Geradas.Should().Be(1);
        result.Value.Puladas.Should().Be(0);
        adicionada.Should().NotBeNull();
        adicionada!.Tipo.Should().Be(TipoNotaFiscal.ComissaoMarketplace);
        adicionada.Valor.Should().Be(6.90m);
        adicionada.CompetenciaInicio.Should().Be(Inicio);
        adicionada.CompetenciaFim.Should().Be(Fim);
        chave.Should().Be($"fx:emitir_nfse:comissao:{treinadorId}:202605");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SemFee_NaoCriaNota()
    {
        SetupLote();

        var result = await _handler.HandleAsync(Cmd());

        result.Value.Geradas.Should().Be(0);
        _enfileirador.Verify(e => e.Enfileirar(It.IsAny<string>(), It.IsAny<EmitirNfsePayload>(), It.IsAny<string>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JaExiste_NaoDuplica()
    {
        var treinadorId = Guid.NewGuid();
        SetupLote(new ComissaoTreinadorPeriodo(treinadorId, 690m));
        _notaRepo.Setup(r => r.ListarTreinadoresComComissaoAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Inicio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { treinadorId });

        var result = await _handler.HandleAsync(Cmd());

        result.Value.Geradas.Should().Be(0);
        result.Value.Puladas.Should().Be(1);
        _notaRepo.Verify(r => r.AdicionarAsync(It.IsAny<NotaFiscal>(), It.IsAny<CancellationToken>()), Times.Never);
        _enfileirador.Verify(e => e.Enfileirar(It.IsAny<string>(), It.IsAny<EmitirNfsePayload>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CorridaUniqueViolation_PulaEContinuaLote()
    {
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        SetupLote(new ComissaoTreinadorPeriodo(t1, 690m), new ComissaoTreinadorPeriodo(t2, 500m));
        _notaRepo.Setup(r => r.ListarTreinadoresComComissaoAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Inicio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        var violacao = new InvalidOperationException("unique");
        _unitOfWork.SetupSequence(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(violacao)
            .Returns(Task.CompletedTask);
        _dbErrorInspector.Setup(i => i.EhViolacaoDeUnicidade(violacao)).Returns(true);

        var result = await _handler.HandleAsync(Cmd());

        result.IsSuccess.Should().BeTrue();
        result.Value.Geradas.Should().Be(1);
        result.Value.Puladas.Should().Be(1);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _unitOfWork.Verify(u => u.DescartarAlteracoesPendentes(), Times.Once);
    }

    [Fact]
    public async Task FeeZeroOuNegativo_Ignora()
    {
        SetupLote(new ComissaoTreinadorPeriodo(Guid.NewGuid(), 0m));
        _notaRepo.Setup(r => r.ListarTreinadoresComComissaoAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Inicio, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());

        var result = await _handler.HandleAsync(Cmd());

        result.Value.Geradas.Should().Be(0);
        _notaRepo.Verify(r => r.AdicionarAsync(It.IsAny<NotaFiscal>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PassaDateTimeUtcParaRepositorio()
    {
        DateTime capturadoInicio = default;
        DateTime capturadoFim = default;
        _pagamentoRepo.Setup(r => r.ListarComissaoPorTreinadorNoPeriodoAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), 10m, It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, DateTime, decimal, Guid?, int, CancellationToken>((inicio, fim, _, _, _, _) =>
            {
                capturadoInicio = inicio;
                capturadoFim = fim;
            })
            .ReturnsAsync([]);

        await _handler.HandleAsync(Cmd());

        capturadoInicio.Kind.Should().Be(DateTimeKind.Utc);
        capturadoFim.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task CompetenciaInvalida_Falha()
    {
        var result = await _handler.HandleAsync(new GerarNfseComissaoMensalCommand(Fim, Inicio));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("nfse_comissao.competencia_invalida");
        _pagamentoRepo.Verify(r => r.ListarComissaoPorTreinadorNoPeriodoAsync(
            It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<decimal>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
