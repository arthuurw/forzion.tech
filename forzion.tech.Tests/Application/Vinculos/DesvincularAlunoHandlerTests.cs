using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Vinculos.DesvincularAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Vinculos;

public class DesvincularAlunoHandlerTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<DesvincularAlunoHandler>> _logger = new();
    private readonly DesvincularAlunoHandler _handler;

    public DesvincularAlunoHandlerTests()
    {
        _assinaturaRepo.Setup(r => r.ObterPorVinculoIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        _handler = new DesvincularAlunoHandler(
            _vinculoRepo.Object, _treinoAlunoRepo.Object, _assinaturaRepo.Object,
            _logRepo.Object, _unitOfWork.Object, _userContext.Object, TimeProvider.System, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_InativaVinculoECascadeTreinoAluno()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        var treinoAluno = TreinoAluno.Criar(Guid.NewGuid(), vinculo.AlunoId, DateTime.UtcNow).Value;

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorParAsync(treinadorId, vinculo.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TreinoAluno>)new[] { treinoAluno });

        var result = await _handler.HandleAsync(new DesvincularAlunoCommand(vinculo.Id, treinadorId));

        result.IsSuccess.Should().BeTrue();
        vinculo.Status.Should().Be(VinculoStatus.Inativo);
        treinoAluno.Status.Should().Be(TreinoAlunoStatus.Inativo);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_LancaAcessoNegado()
    {
        var treinadorLogadoId = Guid.NewGuid();
        var outroTreinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(outroTreinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;

        _userContext.Setup(u => u.PerfilId).Returns(treinadorLogadoId);
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);

        var act = async () => await _handler.HandleAsync(new DesvincularAlunoCommand(vinculo.Id, treinadorLogadoId));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_VinculoNaoEncontrado_LancaException()
    {
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);

        var act = async () => await _handler.HandleAsync(new DesvincularAlunoCommand(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<VinculoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_VinculoJaInativo_RetornaFalha()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        vinculo.Inativar(TestData.Agora);

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);

        var result = await _handler.HandleAsync(new DesvincularAlunoCommand(vinculo.Id, treinadorId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("já está inativo");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_ComAssinaturaAtiva_CancelaAssinatura()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        var assinatura = AssinaturaAluno.Criar(vinculo.Id, Guid.NewGuid(), treinadorId, vinculo.AlunoId, 100m, DateTime.UtcNow).Value;

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorParAsync(treinadorId, vinculo.AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<TreinoAluno>)Array.Empty<TreinoAluno>());
        _assinaturaRepo.Setup(r => r.ObterPorVinculoIdAsync(vinculo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        await _handler.HandleAsync(new DesvincularAlunoCommand(vinculo.Id, treinadorId));

        assinatura.Status.Should().Be(AssinaturaAlunoStatus.Cancelada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
