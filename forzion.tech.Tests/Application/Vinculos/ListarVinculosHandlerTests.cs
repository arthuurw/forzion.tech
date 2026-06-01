using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Vinculos.ListarVinculos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Moq;

namespace forzion.tech.Tests.Application.Vinculos;

public class ListarVinculosHandlerTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly ListarVinculosHandler _handler;

    public ListarVinculosHandlerTests()
    {
        _handler = new ListarVinculosHandler(_vinculoRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_ExistemVinculos_RetornaPaginado()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        var items = new List<VinculoComDetalheAluno>
        {
            new(vinculo, "João Silva", "joao@email.com", false)
        };
        _vinculoRepo.Setup(r => r.ListarComDetalhesAsync(treinadorId, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        var result = await _handler.HandleAsync(treinadorId, null, 1, 20);

        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(20);
    }

    [Fact]
    public async Task HandleAsync_FiltroStatus_PassaStatusParaRepo()
    {
        var treinadorId = Guid.NewGuid();
        _vinculoRepo.Setup(r => r.ListarComDetalhesAsync(treinadorId, VinculoStatus.AguardandoAprovacao, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<VinculoComDetalheAluno>(), 0));

        var result = await _handler.HandleAsync(treinadorId, VinculoStatus.AguardandoAprovacao, 1, 20);

        result.Total.Should().Be(0);
        _vinculoRepo.Verify(r => r.ListarComDetalhesAsync(treinadorId, VinculoStatus.AguardandoAprovacao, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SemVinculos_RetornaListaVazia()
    {
        var treinadorId = Guid.NewGuid();
        _vinculoRepo.Setup(r => r.ListarComDetalhesAsync(treinadorId, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<VinculoComDetalheAluno>(), 0));

        var result = await _handler.HandleAsync(treinadorId, null, 1, 20);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_MapeiaCamposCorretamente()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, alunoId, DateTime.UtcNow).Value;
        var items = new List<VinculoComDetalheAluno> { new(vinculo, "Maria", "maria@email.com", true) };
        _vinculoRepo.Setup(r => r.ListarComDetalhesAsync(treinadorId, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        var result = await _handler.HandleAsync(treinadorId, null, 1, 20);

        var item = result.Items[0];
        item.TreinadorId.Should().Be(treinadorId);
        item.AlunoId.Should().Be(alunoId);
        item.NomeAluno.Should().Be("Maria");
        item.TemVinculoAtivoPrevio.Should().BeTrue();
    }
}
