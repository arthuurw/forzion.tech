using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Vinculos;

public class AprovarVinculoHandlerTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<ILimiteTreinadorService> _limiteService = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<AprovarVinculoHandler>> _logger = new();
    private readonly AprovarVinculoHandler _handler;

    public AprovarVinculoHandlerTests()
    {
        _handler = new AprovarVinculoHandler(
            _vinculoRepo.Object,
            _treinoAlunoRepo.Object,
            _treinoRepo.Object,
            _limiteService.Object,
            _logRepo.Object,
            _unitOfWork.Object,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_VinculoValido_Aprova()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid());
        var pacoteId = Guid.NewGuid();

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(vinculo.AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, pacoteId));

        result.Status.Should().Be(VinculoStatus.Ativo);
        result.PacoteAlunoId.Should().Be(pacoteId);
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoJaVinculado_LancaException()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var vinculoPendente = VinculoTreinadorAluno.Criar(treinadorId, alunoId);
        var vinculoAtivo = VinculoTreinadorAluno.Criar(Guid.NewGuid(), alunoId);

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculoPendente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoPendente);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoAtivo);

        var act = async () => await _handler.HandleAsync(new AprovarVinculoCommand(vinculoPendente.Id, treinadorId, Guid.NewGuid()));
        await act.Should().ThrowAsync<AlunoJaVinculadoException>();
    }

    [Fact]
    public async Task HandleAsync_LimiteAtingido_LancaException()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid());

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(vinculo.AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).ThrowsAsync(new LimiteAlunosAtingidoException());

        var act = async () => await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, Guid.NewGuid()));
        await act.Should().ThrowAsync<LimiteAlunosAtingidoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_LancaAcessoNegado()
    {
        var vinculo = VinculoTreinadorAluno.Criar(Guid.NewGuid(), Guid.NewGuid());
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);

        var act = async () => await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_VinculoNaoEncontrado_LancaVinculoNaoEncontradoException()
    {
        var vinculoId = Guid.NewGuid();
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);

        var act = async () => await _handler.HandleAsync(new AprovarVinculoCommand(vinculoId, Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<VinculoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_VinculoJaAtivo_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid());
        vinculo.Aprovar(treinadorId, Guid.NewGuid());

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(vinculo.AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var act = async () => await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*aguardando aprovação*");
    }

    [Fact]
    public async Task HandleAsync_VinculoInativo_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid());
        vinculo.Inativar();

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(vinculo.AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var act = async () => await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*aguardando aprovação*");
    }

    [Fact]
    public async Task HandleAsync_VinculoValido_CommitaUmaVez()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid());
        var pacoteId = Guid.NewGuid();

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(vinculo.AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, pacoteId));

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
