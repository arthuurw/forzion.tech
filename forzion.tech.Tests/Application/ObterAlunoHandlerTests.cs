using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ObterAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

using forzion.tech.Application.Interfaces;

namespace forzion.tech.Tests.Application;

public class ObterAlunoHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<ObterAlunoHandler>> _logger = new();
    private readonly ObterAlunoHandler _handler;

    public ObterAlunoHandlerTests()
    {
        _handler = new ObterAlunoHandler(
            _alunoRepo.Object,
            _vinculoRepo.Object,
            _userContext.Object,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_AlunoEncontrado_RetornaResponse()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "João", DateTime.UtcNow);
        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var result = await _handler.HandleAsync(new ObterAlunoQuery(aluno.Id));

        result.AlunoId.Should().Be(aluno.Id);
        result.Nome.Should().Be("João");
    }

    [Fact]
    public async Task HandleAsync_AcessoNegado_LancaAcessoNegadoException()
    {
        var alunoId = Guid.NewGuid();
        var treinadorId = Guid.NewGuid();
        var aluno = Aluno.Criar(alunoId, "João", DateTime.UtcNow);

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.IsTreinador).Returns(true);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);

        _alunoRepo.Setup(r => r.ObterPorIdAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var act = async () => await _handler.HandleAsync(new ObterAlunoQuery(alunoId));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_LancaAlunoNaoEncontradoException()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);

        var act = async () => await _handler.HandleAsync(new ObterAlunoQuery(Guid.NewGuid()));

        await act.Should().ThrowAsync<AlunoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_AlunoAcessandoProprioPerfil_RetornaAluno()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "João", DateTime.UtcNow);

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(aluno.Id);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var result = await _handler.HandleAsync(new ObterAlunoQuery(aluno.Id));

        result.AlunoId.Should().Be(aluno.Id);
    }

    [Fact]
    public async Task HandleAsync_AlunoAcessandoOutroAluno_LancaAcessoNegadoException()
    {
        var alunoLogadoId = Guid.NewGuid();
        var outroAluno = Aluno.Criar(Guid.NewGuid(), "Outro", DateTime.UtcNow);

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.IsTreinador).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(alunoLogadoId);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(outroAluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(outroAluno);

        var act = async () => await _handler.HandleAsync(new ObterAlunoQuery(outroAluno.Id));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorComVinculoAtivo_RetornaAluno()
    {
        var treinadorId = Guid.NewGuid();
        var aluno = Aluno.Criar(Guid.NewGuid(), "João", DateTime.UtcNow);
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, aluno.Id, DateTime.UtcNow);

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.IsTreinador).Returns(true);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, aluno.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);

        var result = await _handler.HandleAsync(new ObterAlunoQuery(aluno.Id));

        result.AlunoId.Should().Be(aluno.Id);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
