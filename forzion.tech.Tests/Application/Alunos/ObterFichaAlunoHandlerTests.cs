using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ObterFichaAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Alunos;

public class ObterFichaAlunoHandlerTests
{
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly ObterFichaAlunoHandler _handler;

    public ObterFichaAlunoHandlerTests()
    {
        _handler = new ObterFichaAlunoHandler(_treinoAlunoRepo.Object, _userContext.Object);
    }

    private static TreinoAlunoDetalhe CriarDetalhe(Guid alunoId)
    {
        var treino = Treino.Criar("Treino Força", ObjetivoTreino.Forca, Guid.NewGuid());
        var treinoAluno = TreinoAluno.Criar(treino.Id, alunoId);
        return new TreinoAlunoDetalhe(treinoAluno, treino);
    }

    [Fact]
    public async Task HandleAsync_FichaEncontrada_RetornaDetalhe()
    {
        var alunoId = Guid.NewGuid();
        var detalhe = CriarDetalhe(alunoId);
        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
        _treinoAlunoRepo.Setup(r => r.ObterDetalheAsync(detalhe.TreinoAluno.Id, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detalhe);

        var result = await _handler.HandleAsync(detalhe.TreinoAluno.Id, alunoId);

        result.TreinoAlunoId.Should().Be(detalhe.TreinoAluno.Id);
        result.NomeTreino.Should().Be("Treino Força");
        result.Objetivo.Should().Be(ObjetivoTreino.Forca);
        result.Status.Should().Be(TreinoAlunoStatus.Ativo.ToString());
    }

    [Fact]
    public async Task HandleAsync_FichaNaoEncontrada_LancaTreinoNaoEncontradoException()
    {
        var alunoId = Guid.NewGuid();
        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
        _treinoAlunoRepo.Setup(r => r.ObterDetalheAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TreinoAlunoDetalhe?)null);

        var act = async () => await _handler.HandleAsync(Guid.NewGuid(), alunoId);
        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoIdDiferente_LancaAcessoNegadoException()
    {
        var alunoId = Guid.NewGuid();
        var outroId = Guid.NewGuid();
        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);

        var act = async () => await _handler.HandleAsync(Guid.NewGuid(), outroId);
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }
}
