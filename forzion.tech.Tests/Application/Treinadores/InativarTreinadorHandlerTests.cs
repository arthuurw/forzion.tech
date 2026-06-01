using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.InativarTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Treinadores;

public class InativarTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<InativarTreinadorHandler>> _logger = new();
    private readonly InativarTreinadorHandler _handler;

    public InativarTreinadorHandlerTests()
    {
        _pacoteRepo
            .Setup(r => r.ListarAtivosPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Pacote>)[]);
        _handler = new InativarTreinadorHandler(
            _treinadorRepo.Object, _vinculoRepo.Object, _treinoAlunoRepo.Object,
            _pacoteRepo.Object, _logRepo.Object, _unitOfWork.Object, TimeProvider.System, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_InativaCascade()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, Guid.NewGuid(), DateTime.UtcNow).Value;
        var treinoAluno = TreinoAluno.Criar(Guid.NewGuid(), vinculo.AlunoId, DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _vinculoRepo.Setup(r => r.ListarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<VinculoTreinadorAluno>)new[] { vinculo });
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TreinoAluno>)new[] { treinoAluno });

        await _handler.HandleAsync(new InativarTreinadorCommand(treinador.Id, Guid.NewGuid()));

        treinador.Status.Should().Be(TreinadorStatus.Inativo);
        vinculo.Status.Should().Be(VinculoStatus.Inativo);
        treinoAluno.Status.Should().Be(TreinoAlunoStatus.Inativo);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_MultiplosAlunos_InativaTodosOsTreinoAlunos()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var vinculo1 = VinculoTreinadorAluno.Criar(treinador.Id, Guid.NewGuid(), DateTime.UtcNow).Value;
        var vinculo2 = VinculoTreinadorAluno.Criar(treinador.Id, Guid.NewGuid(), DateTime.UtcNow).Value;
        var ta1 = TreinoAluno.Criar(Guid.NewGuid(), vinculo1.AlunoId, DateTime.UtcNow).Value;
        var ta2 = TreinoAluno.Criar(Guid.NewGuid(), vinculo1.AlunoId, DateTime.UtcNow).Value;
        var ta3 = TreinoAluno.Criar(Guid.NewGuid(), vinculo2.AlunoId, DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _vinculoRepo.Setup(r => r.ListarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<VinculoTreinadorAluno>)new[] { vinculo1, vinculo2 });
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TreinoAluno>)new[] { ta1, ta2, ta3 });

        await _handler.HandleAsync(new InativarTreinadorCommand(treinador.Id, Guid.NewGuid()));

        treinador.Status.Should().Be(TreinadorStatus.Inativo);
        new[] { vinculo1, vinculo2 }.Should().AllSatisfy(v => v.Status.Should().Be(VinculoStatus.Inativo));
        new[] { ta1, ta2, ta3 }.Should().AllSatisfy(ta => ta.Status.Should().Be(TreinoAlunoStatus.Inativo));
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        // Bulk method called once; per-pair method never called
        _treinoAlunoRepo.Verify(r => r.ListarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()), Times.Once);
        _treinoAlunoRepo.Verify(r => r.ListarAtivosPorParAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new InativarTreinadorCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorJaInativo_LancaDomainException()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), TestData.Agora);
        treinador.Inativar(TestData.Agora);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new InativarTreinadorCommand(treinador.Id, Guid.NewGuid()));
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("já está inativo");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
