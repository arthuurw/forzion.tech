using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class ObterAlunoHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ILogger<ObterAlunoHandler>> _logger = new();
    private readonly ObterAlunoHandler _handler;

    public ObterAlunoHandlerTests()
    {
        _handler = new ObterAlunoHandler(_alunoRepo.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_AlunoEncontrado_RetornaResponse()
    {
        var tenantId = Guid.NewGuid();
        var aluno = Aluno.Criar("João", tenantId, Guid.NewGuid());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var result = await _handler.HandleAsync(new ObterAlunoQuery(tenantId, aluno.Id));

        result.AlunoId.Should().Be(aluno.Id);
        result.Nome.Should().Be("João");
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_LancaAlunoNaoEncontradoException()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        var act = async () => await _handler.HandleAsync(new ObterAlunoQuery(Guid.NewGuid(), Guid.NewGuid()));

        await act.Should().ThrowAsync<AlunoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoDeOutroTenant_LancaAcessoNegadoException()
    {
        var aluno = Aluno.Criar("João", Guid.NewGuid(), Guid.NewGuid());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var act = async () => await _handler.HandleAsync(new ObterAlunoQuery(Guid.NewGuid(), aluno.Id));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
