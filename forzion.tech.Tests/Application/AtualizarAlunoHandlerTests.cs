using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.AtualizarAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class AtualizarAlunoHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<AtualizarAlunoHandler>> _logger = new();
    private readonly AtualizarAlunoHandler _handler;

    public AtualizarAlunoHandlerTests()
    {
        _handler = new AtualizarAlunoHandler(_alunoRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_AtualizaERetorna()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "João");
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var result = await _handler.HandleAsync(new AtualizarAlunoCommand(aluno.Id, "Maria", null, null));

        result.Nome.Should().Be("Maria");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_LancaAlunoNaoEncontradoException()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        var act = async () => await _handler.HandleAsync(
            new AtualizarAlunoCommand(Guid.NewGuid(), "Maria", null, null));

        await act.Should().ThrowAsync<AlunoNaoEncontradoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoInativo_LancaAlunoInativoException()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "João");
        aluno.AlterarStatus(AlunoStatus.Inativo);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var act = async () => await _handler.HandleAsync(
            new AtualizarAlunoCommand(aluno.Id, "Maria", null, null));

        await act.Should().ThrowAsync<AlunoInativoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
