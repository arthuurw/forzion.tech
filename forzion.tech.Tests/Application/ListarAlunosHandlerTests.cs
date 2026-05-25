using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class ListarAlunosHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ILogger<ListarAlunosHandler>> _logger = new();
    private readonly ListarAlunosHandler _handler;

    public ListarAlunosHandlerTests()
    {
        _handler = new ListarAlunosHandler(_alunoRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_RetornaListaPaginada()
    {
        var treinadorId = Guid.NewGuid();
        var alunos = new List<Aluno>
        {
            Aluno.Criar(Guid.NewGuid(), "Ana", DateTime.UtcNow),
            Aluno.Criar(Guid.NewGuid(), "Bruno", DateTime.UtcNow)
        };
        _alunoRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Aluno>)alunos, 2));

        var result = await _handler.HandleAsync(new ListarAlunosQuery(treinadorId));

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(20);
    }

    [Fact]
    public async Task HandleAsync_SemAlunos_RetornaVazio()
    {
        var treinadorId = Guid.NewGuid();
        _alunoRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Aluno>)new List<Aluno>(), 0));

        var result = await _handler.HandleAsync(new ListarAlunosQuery(treinadorId));

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
