using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.CriarTreino;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinos;

public class CriarTreinoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CriarTreinoHandler>> _logger = new();
    private readonly CriarTreinoCommandValidator _validator = new();
    private readonly CriarTreinoHandler _handler;

    public CriarTreinoHandlerTests()
    {
        _handler = new CriarTreinoHandler(
            _treinoRepo.Object, 
            _treinoAlunoRepo.Object, 
            _alunoRepo.Object, 
            _unitOfWork.Object, 
            _validator,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CadastraERetorna()
    {
        var tenantId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var treinadorId = Guid.NewGuid();
        var aluno = Aluno.Criar("João", tenantId, treinadorId);
        
        _alunoRepo.Setup(r => r.ObterPorIdAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var command = new CriarTreinoCommand(tenantId, treinadorId, alunoId, "Treino A", ObjetivoTreino.Hipertrofia);
        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be("Treino A");
        result.TenantId.Should().Be(tenantId);
        _treinoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Treino>(), It.IsAny<CancellationToken>()), Times.Once);
        _treinoAlunoRepo.Verify(r => r.AdicionarAsync(It.IsAny<TreinoAluno>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DadosInvalidos_LancaValidationException()
    {
        var command = new CriarTreinoCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "", (ObjetivoTreino)99);
        var act = async () => await _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }
}
