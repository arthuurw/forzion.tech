using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ObterFichaAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Moq;

namespace forzion.tech.Tests.Application.Alunos;

public class ObterFichaAlunoHandlerTests
{
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IExercicioRepository> _exercicioRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly ObterFichaAlunoHandler _handler;

    public ObterFichaAlunoHandlerTests()
    {
        _exercicioRepo
            .Setup(r => r.ObterInfoPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ExercicioInfo>());
        _handler = new ObterFichaAlunoHandler(_treinoAlunoRepo.Object, _exercicioRepo.Object, _userContext.Object);
    }

    private static TreinoAlunoDetalhe CriarDetalhe(Guid alunoId)
    {
        var treino = Treino.Criar("Treino Força", ObjetivoTreino.Forca, Guid.NewGuid(), DateTime.UtcNow).Value;
        var treinoAluno = TreinoAluno.Criar(treino.Id, alunoId, DateTime.UtcNow).Value;
        return new TreinoAlunoDetalhe(treinoAluno, treino);
    }

    /// <summary>
    /// Detalhe com 2 exercícios (ordem invertida) e séries — força as lambdas de projeção
    /// (OrderBy ordem + Select TreinoExercicioResponse/SerieConfigResponse) a rodar.
    /// </summary>
    private static (TreinoAlunoDetalhe Detalhe, Guid Ex1, Guid Ex2) CriarDetalheComExercicios(Guid alunoId)
    {
        var treino = Treino.Criar("Treino Força", ObjetivoTreino.Forca, Guid.NewGuid(), DateTime.UtcNow).Value;
        var ex1 = treino.AdicionarExercicio(Guid.NewGuid(), DateTime.UtcNow).Value; // Ordem 1
        ex1.AdicionarSerie(4, 8, 12, "Série pesada", 80m, 90);
        ex1.AdicionarSerie(3, 10, 12, null, 70m, 60);
        var ex2 = treino.AdicionarExercicio(Guid.NewGuid(), DateTime.UtcNow).Value; // Ordem 2
        ex2.AdicionarSerie(3, 12, 15, null, null, 45);
        var treinoAluno = TreinoAluno.Criar(treino.Id, alunoId, DateTime.UtcNow).Value;
        return (new TreinoAlunoDetalhe(treinoAluno, treino), ex1.ExercicioId, ex2.ExercicioId);
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

        result.IsSuccess.Should().BeTrue();
        result.Value.TreinoAlunoId.Should().Be(detalhe.TreinoAluno.Id);
        result.Value.NomeTreino.Should().Be("Treino Força");
        result.Value.Objetivo.Should().Be(ObjetivoTreino.Forca);
        result.Value.Status.Should().Be(TreinoAlunoStatus.Ativo.ToString());
    }

    [Fact]
    public async Task HandleAsync_FichaNaoEncontrada_RetornaFailureNotFound()
    {
        var alunoId = Guid.NewGuid();
        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
        _treinoAlunoRepo.Setup(r => r.ObterDetalheAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TreinoAlunoDetalhe?)null);

        var result = await _handler.HandleAsync(Guid.NewGuid(), alunoId);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ficha_nao_encontrada");
        result.Error.Type.Should().Be(ErrorType.NotFound);
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

    [Fact]
    public async Task HandleAsync_FichaComExercicios_MapeiaExerciciosSeriesEnomes()
    {
        var alunoId = Guid.NewGuid();
        var (detalhe, ex1Id, ex2Id) = CriarDetalheComExercicios(alunoId);
        var info = new Dictionary<Guid, ExercicioInfo>
        {
            [ex1Id] = new ExercicioInfo("Supino", "Mantenha a postura.", "dQw4w9WgXcQ"),
            [ex2Id] = new ExercicioInfo("Agachamento", null, null)
        };

        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
        _exercicioRepo
            .Setup(r => r.ObterInfoPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(info);
        _treinoAlunoRepo.Setup(r => r.ObterDetalheAsync(detalhe.TreinoAluno.Id, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detalhe);

        var result = await _handler.HandleAsync(detalhe.TreinoAluno.Id, alunoId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Exercicios.Should().HaveCount(2);
        // Ordenado por Ordem
        var primeiro = result.Value.Exercicios[0];
        primeiro.Ordem.Should().Be(1);
        primeiro.NomeExercicio.Should().Be("Supino");
        primeiro.ComoExecutar.Should().Be("Mantenha a postura.");
        primeiro.VideoId.Should().Be("dQw4w9WgXcQ");
        primeiro.Series.Should().HaveCount(2);
        primeiro.Series[0].Quantidade.Should().Be(4);
        primeiro.Series[0].RepeticoesMin.Should().Be(8);
        primeiro.Series[0].RepeticoesMax.Should().Be(12);
        primeiro.Series[0].Carga.Should().Be(80m);
        primeiro.Series[0].Descricao.Should().Be("Série pesada");
        primeiro.Series[0].Descanso.Should().Be(90);
        primeiro.Series[0].Ordem.Should().Be(1);
        var segundo = result.Value.Exercicios[1];
        segundo.Ordem.Should().Be(2);
        segundo.NomeExercicio.Should().Be("Agachamento");
        segundo.ComoExecutar.Should().BeNull();
        segundo.VideoId.Should().BeNull();
        segundo.Series.Should().ContainSingle();
    }

    [Fact]
    public async Task HandleAsync_SystemAdmin_UsaObterDetalheAdmin()
    {
        var alunoId = Guid.NewGuid();
        var (detalhe, _, _) = CriarDetalheComExercicios(alunoId);

        _userContext.Setup(u => u.IsSystemAdmin).Returns(true);
        _exercicioRepo
            .Setup(r => r.ObterInfoPorIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ExercicioInfo>());
        _treinoAlunoRepo.Setup(r => r.ObterDetalheAdminAsync(detalhe.TreinoAluno.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detalhe);

        var result = await _handler.HandleAsync(detalhe.TreinoAluno.Id, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        // Nome ausente no dicionário => string.Empty
        result.Value.Exercicios[0].NomeExercicio.Should().BeEmpty();
        _treinoAlunoRepo.Verify(r => r.ObterDetalheAdminAsync(detalhe.TreinoAluno.Id, It.IsAny<CancellationToken>()), Times.Once);
        _treinoAlunoRepo.Verify(r => r.ObterDetalheAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
