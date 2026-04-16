using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class RegistrarExecucaoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<RegistrarExecucaoHandler>> _logger = new();
    private readonly RegistrarExecucaoHandler _handler;

    public RegistrarExecucaoHandlerTests()
    {
        _handler = new RegistrarExecucaoHandler(
            _treinoRepo.Object, _alunoRepo.Object, _execucaoRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    private static RegistrarExecucaoCommand ComandoValido(Guid tenantId, Guid treinoId, Guid alunoId) =>
        new(tenantId, treinoId, alunoId, DateTime.UtcNow, null, []);

    [Fact]
    public async Task HandleAsync_DadosValidos_RegistraERetorna()
    {
        var tenantId = Guid.NewGuid();
        var treinadorId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, tenantId, treinadorId);
        var aluno = Aluno.Criar("João", tenantId, treinadorId);

        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var result = await _handler.HandleAsync(ComandoValido(tenantId, treino.Id, aluno.Id));

        result.TreinoId.Should().Be(treino.Id);
        result.AlunoId.Should().Be(aluno.Id);
        result.TenantId.Should().Be(tenantId);
        _execucaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<ExecucaoTreino>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinoNaoEncontrado_LancaTreinoNaoEncontradoException()
    {
        var tenantId = Guid.NewGuid();
        var treinoId = Guid.NewGuid();
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treinoId, It.IsAny<CancellationToken>())).ReturnsAsync((Treino?)null);

        var act = async () => await _handler.HandleAsync(ComandoValido(tenantId, treinoId, Guid.NewGuid()));

        await act.Should().ThrowAsync<TreinoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinoTenantDiferente_LancaAcessoNegadoException()
    {
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, Guid.NewGuid(), Guid.NewGuid());
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);

        var act = async () => await _handler.HandleAsync(ComandoValido(Guid.NewGuid(), treino.Id, Guid.NewGuid()));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_LancaAlunoNaoEncontradoException()
    {
        var tenantId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, tenantId, Guid.NewGuid());
        var alunoId = Guid.NewGuid();

        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((Aluno?)null);

        var act = async () => await _handler.HandleAsync(ComandoValido(tenantId, treino.Id, alunoId));

        await act.Should().ThrowAsync<AlunoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoTenantDiferente_LancaAcessoNegadoException()
    {
        var tenantId = Guid.NewGuid();
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, tenantId, Guid.NewGuid());
        var aluno = Aluno.Criar("João", Guid.NewGuid(), Guid.NewGuid());

        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var act = async () => await _handler.HandleAsync(ComandoValido(tenantId, treino.Id, aluno.Id));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
