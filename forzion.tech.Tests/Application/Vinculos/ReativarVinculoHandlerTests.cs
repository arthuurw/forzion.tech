using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Vinculos.ReativarVinculo;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Vinculos;

public class ReativarVinculoHandlerTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ILimiteTreinadorService> _limiteService = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<ReativarVinculoHandler>> _logger = new();
    private readonly ReativarVinculoHandler _handler;

    public ReativarVinculoHandlerTests()
    {
        _handler = new ReativarVinculoHandler(
            _vinculoRepo.Object,
            _alunoRepo.Object,
            _limiteService.Object,
            _logRepo.Object,
            _unitOfWork.Object, TimeProvider.System,
            _logger.Object);
    }

    private static Aluno CriarAluno()
    {
        var a = Aluno.Criar(Guid.NewGuid(), "João Silva", DateTime.UtcNow).Value;
        a.Ativar(TestData.Agora);
        return a;
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CriaVinculoAtivoERetorna()
    {
        var treinadorId = Guid.NewGuid();
        var aluno = CriarAluno();
        var pacoteId = Guid.NewGuid();
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new ReativarVinculoCommand(treinadorId, aluno.Id, pacoteId));

        result.Status.Should().Be(VinculoStatus.Ativo);
        result.TreinadorId.Should().Be(treinadorId);
        result.AlunoId.Should().Be(aluno.Id);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_LancaAlunoNaoEncontradoException()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Aluno?)null);

        var act = async () => await _handler.HandleAsync(new ReativarVinculoCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<AlunoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoJaVinculado_LancaAlunoJaVinculadoException()
    {
        var treinadorId = Guid.NewGuid();
        var aluno = CriarAluno();
        var vinculoAtivo = VinculoTreinadorAluno.Criar(treinadorId, aluno.Id, DateTime.UtcNow).Value;
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoAtivo);

        var act = async () => await _handler.HandleAsync(new ReativarVinculoCommand(treinadorId, aluno.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<AlunoJaVinculadoException>();
    }

    [Fact]
    public async Task HandleAsync_LimiteAtingido_LancaException()
    {
        var treinadorId = Guid.NewGuid();
        var aluno = CriarAluno();
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).ThrowsAsync(new LimiteAlunosAtingidoException());

        var act = async () => await _handler.HandleAsync(new ReativarVinculoCommand(treinadorId, aluno.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<LimiteAlunosAtingidoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
