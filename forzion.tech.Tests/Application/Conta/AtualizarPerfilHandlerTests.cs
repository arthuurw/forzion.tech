using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.AtualizarPerfil;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.ContaTestes;

public class AtualizarPerfilHandlerTests
{
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ISystemUserRepository> _systemUserRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly AtualizarPerfilHandler _handler;

    public AtualizarPerfilHandlerTests()
    {
        _handler = new AtualizarPerfilHandler(
            _userContext.Object,
            _alunoRepo.Object,
            _treinadorRepo.Object,
            _systemUserRepo.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task HandleAsync_AtualizaAluno_Comita()
    {
        var contaId = Guid.NewGuid();
        var aluno = Aluno.Criar(contaId, "João");

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        await _handler.HandleAsync(new AtualizarPerfilCommand("João Silva"));

        aluno.Nome.Should().Be("João Silva");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AtualizaTreinador_Comita()
    {
        var contaId = Guid.NewGuid();
        var treinador = Treinador.Criar(contaId, "Carlos");

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Treinador);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        await _handler.HandleAsync(new AtualizarPerfilCommand("Carlos Novo"));

        treinador.Nome.Should().Be("Carlos Novo");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PerfilNaoEncontrado_LancaDomainException()
    {
        var contaId = Guid.NewGuid();
        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync((Aluno?)null);

        var act = async () => await _handler.HandleAsync(new AtualizarPerfilCommand("X"));
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
