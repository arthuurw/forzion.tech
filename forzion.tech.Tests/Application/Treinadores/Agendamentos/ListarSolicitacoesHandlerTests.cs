using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.Agendamentos;
using forzion.tech.Domain.Enums;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores.Agendamentos;

public class ListarSolicitacoesHandlerTests
{
    private readonly Mock<ISolicitacaoAgendamentoRepository> _solicitacaoRepo = new();
    private readonly ListarSolicitacoesHandler _handler;

    public ListarSolicitacoesHandlerTests()
    {
        _handler = new ListarSolicitacoesHandler(_solicitacaoRepo.Object);
    }

    private static SolicitacaoAgendamentoListItem Item(Guid id, SolicitacaoAgendamentoStatus status, DateTime inicioUtc) => new(
        id, Guid.NewGuid(), "Personal Training", inicioUtc, inicioUtc.AddMinutes(30), status, null, DateTime.UtcNow,
        Guid.NewGuid(), "Fulano", TipoContatoLead.Email, "fulano@teste.com", false);

    [Fact]
    public async Task HandleAsync_RepassaTreinadorIdStatusEPaginacaoAoRepositorio()
    {
        var treinadorId = Guid.NewGuid();
        _solicitacaoRepo.Setup(r => r.ListarPorTreinadorAsync(
                treinadorId, SolicitacaoAgendamentoStatus.PendenteAgente, 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<SolicitacaoAgendamentoListItem>)[], 0));

        await _handler.HandleAsync(new ListarSolicitacoesQuery(treinadorId, SolicitacaoAgendamentoStatus.PendenteAgente, 2, 5));

        _solicitacaoRepo.Verify(r => r.ListarPorTreinadorAsync(
            treinadorId, SolicitacaoAgendamentoStatus.PendenteAgente, 2, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DevolveItensETotalDoRepositorioComPaginacaoEcoada()
    {
        var treinadorId = Guid.NewGuid();
        var agora = DateTime.UtcNow;
        var itens = new List<SolicitacaoAgendamentoListItem>
        {
            Item(Guid.NewGuid(), SolicitacaoAgendamentoStatus.Confirmada, agora.AddDays(1)),
            Item(Guid.NewGuid(), SolicitacaoAgendamentoStatus.PendenteAgente, agora.AddDays(2)),
        };
        _solicitacaoRepo.Setup(r => r.ListarPorTreinadorAsync(
                treinadorId, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<SolicitacaoAgendamentoListItem>)itens, 2));

        var result = await _handler.HandleAsync(new ListarSolicitacoesQuery(treinadorId));

        result.Items.Should().BeEquivalentTo(itens, options => options.WithStrictOrdering());
        result.Total.Should().Be(2);
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(20);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
