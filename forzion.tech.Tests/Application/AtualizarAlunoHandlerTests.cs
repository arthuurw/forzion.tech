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
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<AtualizarAlunoHandler>> _logger = new();
    private readonly AtualizarAlunoHandler _handler;

    public AtualizarAlunoHandlerTests()
    {
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _handler = new AtualizarAlunoHandler(
            _alunoRepo.Object,
            _vinculoRepo.Object,
            _unitOfWork.Object,
            _userContext.Object,
            _logger.Object);
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
    public async Task HandleAsync_AcessoNegado_LancaAcessoNegadoException()
    {
        var alunoId = Guid.NewGuid();
        var treinadorId = Guid.NewGuid();
        var aluno = Aluno.Criar(alunoId, "João");

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.IsTreinador).Returns(true);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);

        _alunoRepo.Setup(r => r.ObterPorIdAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var act = async () => await _handler.HandleAsync(
            new AtualizarAlunoCommand(alunoId, "Maria", null, null));

        await act.Should().ThrowAsync<AcessoNegadoException>();
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
        aluno.Ativar();
        aluno.Inativar();
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
