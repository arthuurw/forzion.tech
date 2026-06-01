using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ListarAlunos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class ListarAlunosHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<ListarAlunosHandler>> _logger = new();
    private readonly ListarAlunosHandler _handler;

    public ListarAlunosHandlerTests()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.IsTreinador).Returns(true);
        _handler = new ListarAlunosHandler(_alunoRepo.Object, _userContext.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_RetornaListaPaginada()
    {
        var treinadorId = Guid.NewGuid();
        var alunos = new List<Aluno>
        {
            Aluno.Criar(Guid.NewGuid(), "Ana", DateTime.UtcNow).Value,
            Aluno.Criar(Guid.NewGuid(), "Bruno", DateTime.UtcNow).Value
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

    [Fact]
    public async Task HandleAsync_Aluno_LancaAcessoNegadoException()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.IsTreinador).Returns(false);

        var act = async () => await _handler.HandleAsync(new ListarAlunosQuery(Guid.NewGuid()));

        await act.Should().ThrowAsync<AcessoNegadoException>();
        _alunoRepo.Verify(r => r.ListarPorTreinadorAsync(
            It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SystemAdmin_AcessaQualquerTreinador()
    {
        var treinadorId = Guid.NewGuid();
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _userContext.Setup(c => c.IsTreinador).Returns(false);
        _alunoRepo.Setup(r => r.ListarPorTreinadorAsync(treinadorId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Aluno>)[], 0));

        var act = async () => await _handler.HandleAsync(new ListarAlunosQuery(treinadorId));

        await act.Should().NotThrowAsync();
    }
}
