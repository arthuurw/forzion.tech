using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.ObterPerfil;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Moq;
using DomainConta = forzion.tech.Domain.Entities.Conta;

namespace forzion.tech.Tests.Application.ContaTestes;

public class ObterPerfilHandlerTests
{
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ISystemUserRepository> _systemUserRepo = new();
    private readonly ObterPerfilHandler _handler;

    public ObterPerfilHandlerTests()
    {
        _handler = new ObterPerfilHandler(
            _userContext.Object,
            _contaRepo.Object,
            _alunoRepo.Object,
            _treinadorRepo.Object,
            _systemUserRepo.Object);
    }

    private static DomainConta CriarConta(TipoConta tipo) =>
        DomainConta.Criar(Email.Criar("user@test.com").Value, "hash", tipo, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_Aluno_RetornaPerfilComNome()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "João", DateTime.UtcNow).Value;

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var result = await _handler.HandleAsync();

        result.Nome.Should().Be("João");
        result.TipoConta.Should().Be(TipoConta.Aluno.ToString());
    }

    [Fact]
    public async Task HandleAsync_Treinador_RetornaPerfilComNome()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.Treinador);
        var treinador = Treinador.Criar(contaId, "Carlos", DateTime.UtcNow).Value;

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Treinador);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync();

        result.Nome.Should().Be("Carlos");
        result.TipoConta.Should().Be(TipoConta.Treinador.ToString());
    }

    [Fact]
    public async Task HandleAsync_Admin_RetornaPerfilComNome()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.SystemAdmin);
        var admin = SystemUser.Criar(conta.Id, "Admin", DateTime.UtcNow).Value;

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.SystemAdmin);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _systemUserRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        var result = await _handler.HandleAsync();

        result.Nome.Should().Be("Admin");
    }

    [Fact]
    public async Task HandleAsync_ContaNaoEncontrada_LancaDomainException()
    {
        var contaId = Guid.NewGuid();
        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync((DomainConta?)null);

        var act = async () => await _handler.HandleAsync();
        await act.Should().ThrowAsync<DomainException>();
    }
}
