using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class AlterarStatusAlunoHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<AlterarStatusAlunoHandler>> _logger = new();
    private readonly AlterarStatusAlunoHandler _handler;

    public AlterarStatusAlunoHandlerTests()
    {
        _handler = new AlterarStatusAlunoHandler(
            _alunoRepo.Object, _userContext.Object, _unitOfWork.Object, TimeProvider.System, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_AlunoExistente_E_UsuarioSystemAdmin_AlteraStatus()
    {
        var aluno = new AlunoBuilder().ComNome("João").Build();
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _userContext.Setup(u => u.IsSystemAdmin).Returns(true);

        var result = await _handler.HandleAsync(
            new AlterarStatusAlunoCommand(aluno.Id, AlunoStatus.Inativo));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(AlunoStatus.Inativo);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoExistente_Mas_UsuarioNaoSystemAdmin_LancaAcessoNegadoException()
    {
        var aluno = new AlunoBuilder().ComNome("João").Build();
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _userContext.Setup(u => u.IsSystemAdmin).Returns(false);

        var act = async () => await _handler.HandleAsync(
            new AlterarStatusAlunoCommand(aluno.Id, AlunoStatus.Inativo));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_LancaAlunoNaoEncontradoException()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        var act = async () => await _handler.HandleAsync(
            new AlterarStatusAlunoCommand(Guid.NewGuid(), AlunoStatus.Inativo));

        await act.Should().ThrowAsync<AlunoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
