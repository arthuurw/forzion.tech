using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Application.UseCases.Pagamentos.ListarRecebimentosTreinador;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public class ListarRecebimentosTreinadorHandlerTests
{
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly ListarRecebimentosTreinadorHandler _handler;

    public ListarRecebimentosTreinadorHandlerTests()
    {
        _handler = new ListarRecebimentosTreinadorHandler(
            _pagamentoRepo.Object, Options.Create(new PaymentSettings { TaxaPlataformaPercent = 10m }));
    }

    private static RecebimentoTreinadorItem Item(decimal valor, DateTime? createdAt = null) =>
        new(Guid.NewGuid(), valor, PagamentoStatus.Pago, MetodoPagamento.Pix, "Aluno X",
            createdAt ?? DateTime.UtcNow, createdAt ?? DateTime.UtcNow);

    [Fact]
    public async Task HandleAsync_CalculaLiquidoEstimadoComTaxa()
    {
        _pagamentoRepo.Setup(r => r.ListarPorTreinadorAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecebimentoTreinadorItem> { Item(100m) });

        var resultado = await _handler.HandleAsync(new ListarRecebimentosTreinadorQuery(Guid.NewGuid(), null, 20));

        var item = resultado.Itens.Should().ContainSingle().Subject;
        item.Bruto.Should().Be(100m);
        item.TaxaPercent.Should().Be(10m);
        item.LiquidoEstimado.Should().Be(90m);
    }

    [Fact]
    public async Task HandleAsync_EscopaPeloTreinadorAutenticado()
    {
        var treinadorId = Guid.NewGuid();
        _pagamentoRepo.Setup(r => r.ListarPorTreinadorAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecebimentoTreinadorItem>());

        await _handler.HandleAsync(new ListarRecebimentosTreinadorQuery(treinadorId, null, 20));

        _pagamentoRepo.Verify(r => r.ListarPorTreinadorAsync(
            treinadorId, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaginaCheia_EmiteProximoCursor()
    {
        _pagamentoRepo.Setup(r => r.ListarPorTreinadorAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecebimentoTreinadorItem> { Item(10m), Item(20m), Item(30m) });

        var resultado = await _handler.HandleAsync(new ListarRecebimentosTreinadorQuery(Guid.NewGuid(), null, 2));

        resultado.Itens.Should().HaveCount(2);
        resultado.ProximoCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HandleAsync_UltimaPagina_SemProximoCursor()
    {
        _pagamentoRepo.Setup(r => r.ListarPorTreinadorAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecebimentoTreinadorItem> { Item(10m) });

        var resultado = await _handler.HandleAsync(new ListarRecebimentosTreinadorQuery(Guid.NewGuid(), null, 2));

        resultado.ProximoCursor.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_CursorEmitido_DecodificaNaProximaChamada()
    {
        var createdAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var ultimo = new RecebimentoTreinadorItem(Guid.NewGuid(), 30m, PagamentoStatus.Pago,
            MetodoPagamento.Pix, "A", createdAt, createdAt);
        _pagamentoRepo.Setup(r => r.ListarPorTreinadorAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecebimentoTreinadorItem>());
        _pagamentoRepo.Setup(r => r.ListarPorTreinadorAsync(
                It.IsAny<Guid>(), null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecebimentoTreinadorItem> { ultimo, Item(10m) });

        var pagina1 = await _handler.HandleAsync(new ListarRecebimentosTreinadorQuery(Guid.NewGuid(), null, 1));
        await _handler.HandleAsync(new ListarRecebimentosTreinadorQuery(Guid.NewGuid(), pagina1.ProximoCursor, 1));

        _pagamentoRepo.Verify(r => r.ListarPorTreinadorAsync(
            It.IsAny<Guid>(), createdAt, ultimo.PagamentoId, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CursorInvalido_TrataComoPrimeiraPagina()
    {
        _pagamentoRepo.Setup(r => r.ListarPorTreinadorAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RecebimentoTreinadorItem>());

        await _handler.HandleAsync(new ListarRecebimentosTreinadorQuery(Guid.NewGuid(), "lixo!!!", 20));

        _pagamentoRepo.Verify(r => r.ListarPorTreinadorAsync(
            It.IsAny<Guid>(), null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
