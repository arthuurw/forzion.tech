using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.VincularFichaAoAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class VincularFichaAoAlunoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly VincularFichaAoAlunoHandler _handler;

    public VincularFichaAoAlunoHandlerTests()
    {
        _handler = new VincularFichaAoAlunoHandler(
            _treinoRepo.Object,
            _treinoAlunoRepo.Object,
            _vinculoRepo.Object,
            _unitOfWork.Object,
            _userContext.Object,
            Mock.Of<ILogger<VincularFichaAoAlunoHandler>>());
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_VinculaECommita()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var treino = Treino.Criar("Treino Teste", ObjetivoTreino.Hipertrofia, treinadorId);

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VinculoTreinadorAluno.Criar(treinadorId, alunoId));
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorTreinoIdAsync(treino.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TreinoAlunoVinculado>());

        await _handler.HandleAsync(new VincularFichaAoAlunoCommand(treino.Id, alunoId));

        _treinoAlunoRepo.Verify(r => r.AdicionarAsync(It.IsAny<TreinoAluno>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FichaJaVinculadaAOutroAluno_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var treino = Treino.Criar("Treino Teste", ObjetivoTreino.Hipertrofia, treinadorId);
        var vinculoExistente = new TreinoAlunoVinculado(Guid.NewGuid(), Guid.NewGuid(), "Aluno Existente", TreinoAlunoStatus.Ativo);

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VinculoTreinadorAluno.Criar(treinadorId, alunoId));
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorTreinoIdAsync(treino.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { vinculoExistente });

        var act = async () => await _handler.HandleAsync(new VincularFichaAoAlunoCommand(treino.Id, alunoId));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Aluno Existente*");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        var treinadorId = Guid.NewGuid();
        var treinoId = Guid.NewGuid();

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treinoId, It.IsAny<CancellationToken>())).ReturnsAsync((Treino?)null);

        var act = async () => await _handler.HandleAsync(new VincularFichaAoAlunoCommand(treinoId, Guid.NewGuid()));

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinoDeOutroTreinador_LancaAcessoNegadoException()
    {
        var treinadorId = Guid.NewGuid();
        var treino = Treino.Criar("Treino Teste", ObjetivoTreino.Hipertrofia, Guid.NewGuid());

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var act = async () => await _handler.HandleAsync(new VincularFichaAoAlunoCommand(treino.Id, Guid.NewGuid()));

        await act.Should().ThrowAsync<AcessoNegadoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_VinculoNaoEncontrado_LancaVinculoNaoEncontradoException()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var treino = Treino.Criar("Treino Teste", ObjetivoTreino.Hipertrofia, treinadorId);

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var act = async () => await _handler.HandleAsync(new VincularFichaAoAlunoCommand(treino.Id, alunoId));

        await act.Should().ThrowAsync<VinculoNaoEncontradoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
