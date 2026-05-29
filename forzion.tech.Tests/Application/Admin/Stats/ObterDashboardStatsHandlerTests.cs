using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.Stats;
using Moq;

namespace forzion.tech.Tests.Application.Admin.Stats;

public class ObterDashboardStatsHandlerTests
{
    private readonly Mock<IAdminStatsRepository> _statsRepo = new();
    private readonly ObterDashboardStatsHandler _handler;

    public ObterDashboardStatsHandlerTests()
    {
        _handler = new ObterDashboardStatsHandler(_statsRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_RetornaDashboardComDistribuicoes()
    {
        var planoDistribuicao = new List<PlanoDistribuicaoItem>
        {
            new("Basic", 5),
            new("Pro", 3),
            new("SemPlano", 2)
        };
        var alunoFinalidade = new List<AlunoFinalidadeItem>
        {
            new("Hipertrofia", 10),
            new("Emagrecimento", 7),
            new("NaoInformado", 3)
        };

        _statsRepo
            .Setup(r => r.ObterDistribuicaoPorPlanoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(planoDistribuicao);
        _statsRepo
            .Setup(r => r.ObterDistribuicaoPorFinalidadeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(alunoFinalidade);

        var result = await _handler.HandleAsync();

        result.PlanoDistribuicao.Should().HaveCount(3);
        result.PlanoDistribuicao.Should().ContainSingle(x => x.Tier == "Basic" && x.Total == 5);
        result.PlanoDistribuicao.Should().ContainSingle(x => x.Tier == "SemPlano" && x.Total == 2);

        result.AlunoFinalidade.Should().HaveCount(3);
        result.AlunoFinalidade.Should().ContainSingle(x => x.Finalidade == "Hipertrofia" && x.Total == 10);
        result.AlunoFinalidade.Should().ContainSingle(x => x.Finalidade == "NaoInformado" && x.Total == 3);
    }

    [Fact]
    public async Task HandleAsync_DistribuicaoVazia_RetornaListasVazias()
    {
        _statsRepo
            .Setup(r => r.ObterDistribuicaoPorPlanoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlanoDistribuicaoItem>());
        _statsRepo
            .Setup(r => r.ObterDistribuicaoPorFinalidadeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlunoFinalidadeItem>());

        var result = await _handler.HandleAsync();

        result.PlanoDistribuicao.Should().BeEmpty();
        result.AlunoFinalidade.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ChamaRepositorioDuasVezes()
    {
        _statsRepo
            .Setup(r => r.ObterDistribuicaoPorPlanoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PlanoDistribuicaoItem>());
        _statsRepo
            .Setup(r => r.ObterDistribuicaoPorFinalidadeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlunoFinalidadeItem>());

        await _handler.HandleAsync();

        _statsRepo.Verify(r => r.ObterDistribuicaoPorPlanoAsync(It.IsAny<CancellationToken>()), Times.Once);
        _statsRepo.Verify(r => r.ObterDistribuicaoPorFinalidadeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
