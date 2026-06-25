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
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogger<ObterAlunoHandler>> _logger = new();
    private readonly ObterAlunoHandler _handler;

    public ObterAlunoHandlerTests()
    {
        _handler = new ObterAlunoHandler(
            _alunoRepo.Object,
            _vinculoRepo.Object,
            _pacoteRepo.Object,
            _userContext.Object,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_AlunoEncontrado_RetornaResponse()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "João", DateTime.UtcNow).Value;
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
        var aluno = Aluno.Criar(alunoId, "João", DateTime.UtcNow).Value;

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
        var aluno = Aluno.Criar(Guid.NewGuid(), "João", DateTime.UtcNow).Value;

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
        var outroAluno = Aluno.Criar(Guid.NewGuid(), "Outro", DateTime.UtcNow).Value;

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
        var aluno = Aluno.Criar(Guid.NewGuid(), "João", DateTime.UtcNow).Value;
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, aluno.Id, DateTime.UtcNow).Value;

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

    [Fact]
    public async Task HandleAsync_TreinadorVinculoAtivoComPacote_RetornaPacoteIdENome()
    {
        var treinadorId = Guid.NewGuid();
        var pacoteId = Guid.NewGuid();
        var aluno = Aluno.Criar(Guid.NewGuid(), "Ana", DateTime.UtcNow).Value;
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, aluno.Id, DateTime.UtcNow, pacoteId).Value;
        var pacote = Pacote.Criar(treinadorId, "Plano Premium", 199m, DateTime.UtcNow).Value;

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.IsTreinador).Returns(true);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacoteId, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var result = await _handler.HandleAsync(new ObterAlunoQuery(aluno.Id));

        result.PacoteId.Should().Be(pacoteId);
        result.PacoteNome.Should().Be("Plano Premium");
    }

    [Fact]
    public async Task HandleAsync_TreinadorVinculoAtivoSemPacote_PacoteNuloSemChamadaAoRepo()
    {
        var treinadorId = Guid.NewGuid();
        var aluno = Aluno.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, aluno.Id, DateTime.UtcNow).Value;

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.IsTreinador).Returns(true);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);

        var result = await _handler.HandleAsync(new ObterAlunoQuery(aluno.Id));

        result.PacoteId.Should().BeNull();
        result.PacoteNome.Should().BeNull();
        _pacoteRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorVinculoComPacoteInexistente_RetornaPacoteIdENomeNulos()
    {
        var treinadorId = Guid.NewGuid();
        var pacoteId = Guid.NewGuid();
        var aluno = Aluno.Criar(Guid.NewGuid(), "Ana", DateTime.UtcNow).Value;
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, aluno.Id, DateTime.UtcNow, pacoteId).Value;

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.IsTreinador).Returns(true);
        _userContext.Setup(c => c.PerfilId).Returns(treinadorId);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinadorId, aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacoteId, It.IsAny<CancellationToken>())).ReturnsAsync((Pacote?)null);

        var result = await _handler.HandleAsync(new ObterAlunoQuery(aluno.Id));

        result.PacoteId.Should().BeNull();
        result.PacoteNome.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_Admin_RetornaPacoteNulo()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Bia", DateTime.UtcNow).Value;

        _userContext.Setup(c => c.IsSystemAdmin).Returns(true);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var result = await _handler.HandleAsync(new ObterAlunoQuery(aluno.Id));

        result.PacoteId.Should().BeNull();
        result.PacoteNome.Should().BeNull();
        _pacoteRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoAcessandoProprioPerfil_RetornaPacoteNulo()
    {
        var aluno = Aluno.Criar(Guid.NewGuid(), "Diego", DateTime.UtcNow).Value;

        _userContext.Setup(c => c.IsSystemAdmin).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(aluno.Id);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var result = await _handler.HandleAsync(new ObterAlunoQuery(aluno.Id));

        result.PacoteId.Should().BeNull();
        result.PacoteNome.Should().BeNull();
        _pacoteRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
