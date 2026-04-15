using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application;

public class CadastrarAlunoHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IUsuarioRepository> _usuarioRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CadastrarAlunoHandler>> _logger = new();
    private readonly CadastrarAlunoHandler _handler;

    public CadastrarAlunoHandlerTests()
    {
        _handler = new CadastrarAlunoHandler(
            _alunoRepo.Object, _usuarioRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    private static Usuario CriarTreinador(Guid tenantId)
    {
        var email = Email.Criar("t@t.com");
        var u = Usuario.Criar(Guid.NewGuid(), "Treinador", email, tenantId, Role.Trainer);
        return u;
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CadastraERetorna()
    {
        var tenantId = Guid.NewGuid();
        var treinador = CriarTreinador(tenantId);
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var command = new CadastrarAlunoCommand(tenantId, treinador.Id, "João", null, null);
        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be("João");
        result.TenantId.Should().Be(tenantId);
        _alunoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Aluno>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaUsuarioNaoEncontradoException()
    {
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Usuario?)null);

        var act = async () => await _handler.HandleAsync(
            new CadastrarAlunoCommand(Guid.NewGuid(), Guid.NewGuid(), "João", null, null));

        await act.Should().ThrowAsync<UsuarioNaoEncontradoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorDeOutroTenant_LancaAcessoNegadoException()
    {
        var treinador = CriarTreinador(Guid.NewGuid());
        _usuarioRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(
            new CadastrarAlunoCommand(Guid.NewGuid(), treinador.Id, "João", null, null));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
