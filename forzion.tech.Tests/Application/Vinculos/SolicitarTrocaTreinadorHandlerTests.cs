using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Vinculos.SolicitarTrocaTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Vinculos;

public class SolicitarTrocaTreinadorHandlerTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<SolicitarTrocaTreinadorHandler>> _logger = new();
    private readonly SolicitarTrocaTreinadorHandler _handler;

    public SolicitarTrocaTreinadorHandlerTests()
    {
        _handler = new SolicitarTrocaTreinadorHandler(
            _vinculoRepo.Object,
            _treinadorRepo.Object,
            _unitOfWork.Object,
            _logger.Object);
    }

    private static Treinador CriarTreinadorAtivo()
    {
        var t = Treinador.Criar(Guid.NewGuid(), "Carlos");
        t.Aprovar(Guid.NewGuid());
        return t;
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CriaVinculoPendenteERetorna()
    {
        var novoTreinador = CriarTreinadorAtivo();
        var alunoId = Guid.NewGuid();
        var vinculoAtual = VinculoTreinadorAluno.Criar(Guid.NewGuid(), alunoId);
        var pacoteId = Guid.NewGuid();

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(novoTreinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(novoTreinador);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoAtual);
        _vinculoRepo.Setup(r => r.ObterPendentePorParAsync(novoTreinador.Id, alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);

        var result = await _handler.HandleAsync(new SolicitarTrocaTreinadorCommand(alunoId, novoTreinador.Id, pacoteId));

        result.Status.Should().Be(VinculoStatus.AguardandoAprovacao);
        result.TreinadorId.Should().Be(novoTreinador.Id);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaTreinadorNaoEncontradoException()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new SolicitarTrocaTreinadorCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorInativo_LancaDomainException()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(new SolicitarTrocaTreinadorCommand(Guid.NewGuid(), treinador.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*disponível*");
    }

    [Fact]
    public async Task HandleAsync_SemVinculoAtivo_LancaDomainException()
    {
        var novoTreinador = CriarTreinadorAtivo();
        var alunoId = Guid.NewGuid();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(novoTreinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(novoTreinador);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);

        var act = async () => await _handler.HandleAsync(new SolicitarTrocaTreinadorCommand(alunoId, novoTreinador.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*vínculo ativo*");
    }

    [Fact]
    public async Task HandleAsync_MesmoTreinador_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        var novoTreinador = CriarTreinadorAtivo();
        var alunoId = Guid.NewGuid();
        var vinculoAtual = VinculoTreinadorAluno.Criar(novoTreinador.Id, alunoId);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(novoTreinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(novoTreinador);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoAtual);

        var act = async () => await _handler.HandleAsync(new SolicitarTrocaTreinadorCommand(alunoId, novoTreinador.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*já está vinculado*");
    }

    [Fact]
    public async Task HandleAsync_JaTemPendente_LancaDomainException()
    {
        var novoTreinador = CriarTreinadorAtivo();
        var alunoId = Guid.NewGuid();
        var vinculoAtual = VinculoTreinadorAluno.Criar(Guid.NewGuid(), alunoId);
        var vinculoPendente = VinculoTreinadorAluno.Criar(novoTreinador.Id, alunoId);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(novoTreinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(novoTreinador);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoAtual);
        _vinculoRepo.Setup(r => r.ObterPendentePorParAsync(novoTreinador.Id, alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoPendente);

        var act = async () => await _handler.HandleAsync(new SolicitarTrocaTreinadorCommand(alunoId, novoTreinador.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*pendente*");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
