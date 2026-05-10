using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ListarExecucoesAluno;
using Moq;

namespace forzion.tech.Tests.Application.Alunos;

public class ListarExecucoesAlunoHandlerTests
{
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly ListarExecucoesAlunoHandler _handler;

    public ListarExecucoesAlunoHandlerTests()
    {
        _handler = new ListarExecucoesAlunoHandler(_execucaoRepo.Object);
    }

    private static ExecucaoComNome CriarExecucaoComNome(Guid alunoId) =>
        new(Guid.NewGuid(), Guid.NewGuid(), alunoId, DateTime.UtcNow, null, DateTime.UtcNow, "Treino A", 3, 9);

    [Fact]
    public async Task HandleAsync_SemExecucoes_RetornaListaVazia()
    {
        var alunoId = Guid.NewGuid();
        _execucaoRepo.Setup(r => r.ListarComNomePorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecucaoComNome>());
        _execucaoRepo.Setup(r => r.ContarPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _handler.HandleAsync(alunoId, 1, 10);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_ComExecucoes_RetornaItemsMapeados()
    {
        var alunoId = Guid.NewGuid();
        var execucoes = new List<ExecucaoComNome> { CriarExecucaoComNome(alunoId), CriarExecucaoComNome(alunoId) };
        _execucaoRepo.Setup(r => r.ListarComNomePorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execucoes);
        _execucaoRepo.Setup(r => r.ContarPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _handler.HandleAsync(alunoId, 1, 10);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Items.Should().AllSatisfy(e => e.AlunoId.Should().Be(alunoId));
    }

    [Fact]
    public async Task HandleAsync_PaginacaoCorreta_RefleteNaResposta()
    {
        var alunoId = Guid.NewGuid();
        _execucaoRepo.Setup(r => r.ListarComNomePorAlunoAsync(alunoId, 3, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecucaoComNome>());
        _execucaoRepo.Setup(r => r.ContarPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _handler.HandleAsync(alunoId, 3, 5);

        result.Pagina.Should().Be(3);
        result.TamanhoPagina.Should().Be(5);
    }
}
