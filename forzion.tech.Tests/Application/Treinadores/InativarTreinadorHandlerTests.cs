using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.InativarTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class InativarTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IPacoteAlunoRepository> _pacoteRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<InativarTreinadorHandler>> _logger = new();
    private readonly InativarTreinadorHandler _handler;

    public InativarTreinadorHandlerTests()
    {
        _pacoteRepo
            .Setup(r => r.ListarAtivosPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PacoteAluno>)[]);
        _handler = new InativarTreinadorHandler(
            _treinadorRepo.Object, _vinculoRepo.Object, _treinoAlunoRepo.Object,
            _pacoteRepo.Object, _logRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_InativaCascade()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, Guid.NewGuid());
        var treinoAluno = TreinoAluno.Criar(Guid.NewGuid(), vinculo.AlunoId);

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _vinculoRepo.Setup(r => r.ListarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<VinculoTreinadorAluno>)new[] { vinculo });
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorParAsync(treinador.Id, vinculo.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TreinoAluno>)new[] { treinoAluno });

        await _handler.HandleAsync(new InativarTreinadorCommand(treinador.Id, Guid.NewGuid()));

        treinador.Status.Should().Be(TreinadorStatus.Inativo);
        vinculo.Status.Should().Be(VinculoStatus.Inativo);
        treinoAluno.Status.Should().Be(TreinoAlunoStatus.Inativo);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        treinador.Aprovar(Guid.NewGuid());
        treinador.Inativar();

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(new InativarTreinadorCommand(treinador.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*já está inativo*");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
