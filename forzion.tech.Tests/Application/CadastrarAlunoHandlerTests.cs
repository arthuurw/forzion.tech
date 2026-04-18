using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class CadastrarAlunoHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CadastrarAlunoHandler>> _logger = new();
    private readonly CadastrarAlunoCommandValidator _validator = new();
    private readonly CadastrarAlunoHandler _handler;

    public CadastrarAlunoHandlerTests()
    {
        _handler = new CadastrarAlunoHandler(
            _alunoRepo.Object,
            _unitOfWork.Object,
            _validator,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CadastraERetorna()
    {
        var command = new CadastrarAlunoCommand(Guid.NewGuid(), "Aluno", "a@e.com", "123");
        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be("Aluno");
        _alunoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Aluno>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DadosInvalidos_LancaValidationException()
    {
        var command = new CadastrarAlunoCommand(Guid.NewGuid(), "", "invalido", new string('1', 21));
        var act = async () => await _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }
}
