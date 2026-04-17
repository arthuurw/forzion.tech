using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinos.VincularFichaAoAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Application.Treinos;

public class VincularFichaAoAlunoHandlerTests
{
    private readonly Mock<ITreinoRepository> _treinoRepository = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepository = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepository = new();
    private readonly Mock<ILimiteFichasService> _limiteFichasService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly VincularFichaAoAlunoHandler _handler;

    public VincularFichaAoAlunoHandlerTests()
    {
        _handler = new VincularFichaAoAlunoHandler(
            _treinoRepository.Object,
            _treinoAlunoRepository.Object,
            _vinculoRepository.Object,
            _limiteFichasService.Object,
            _unitOfWork.Object,
            _userContext.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<VincularFichaAoAlunoHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_DeveVincularFicha_QuandoDadosValidos()
    {
        // Arrange
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var treinoId = Guid.NewGuid();
        var command = new VincularFichaAoAlunoCommand(treinoId, alunoId);

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        
        var treino = Treino.Criar("Treino Teste", ObjetivoTreino.Hipertrofia, treinadorId);
        _treinoRepository.Setup(r => r.ObterPorIdAsync(treinoId, default)).ReturnsAsync(treino);
        
        _vinculoRepository.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, default))
            .ReturnsAsync(VinculoTreinadorAluno.Criar(treinadorId, alunoId));

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _treinoAlunoRepository.Verify(r => r.AdicionarAsync(It.IsAny<TreinoAluno>(), default), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DeveLancarAcessoNegado_QuandoTreinoNaoPertenceAoTreinador()
    {
        // Arrange
        var treinadorId = Guid.NewGuid();
        var treinoId = Guid.NewGuid();
        var command = new VincularFichaAoAlunoCommand(treinoId, Guid.NewGuid());

        _userContext.Setup(u => u.PerfilId).Returns(treinadorId);
        
        var treino = Treino.Criar("Treino Teste", ObjetivoTreino.Hipertrofia, Guid.NewGuid()); // Treinador diferente
        _treinoRepository.Setup(r => r.ObterPorIdAsync(treinoId, default)).ReturnsAsync(treino);

        // Act & Assert
        await Assert.ThrowsAsync<AcessoNegadoException>(() => _handler.HandleAsync(command));
    }
}
