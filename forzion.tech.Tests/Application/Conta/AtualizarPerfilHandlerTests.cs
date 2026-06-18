using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.AtualizarPerfil;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.ContaTestes;

public class AtualizarPerfilHandlerTests
{
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ISystemUserRepository> _systemUserRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<AtualizarPerfilCommand>> _validator = new();
    private readonly AtualizarPerfilHandler _handler;

    public AtualizarPerfilHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _handler = new AtualizarPerfilHandler(
            _userContext.Object,
            _alunoRepo.Object,
            _treinadorRepo.Object,
            _systemUserRepo.Object,
            _unitOfWork.Object,
            TimeProvider.System,
            _validator.Object);
    }

    [Fact]
    public async Task HandleAsync_AtualizaAluno_Comita()
    {
        var contaId = Guid.NewGuid();
        var aluno = Aluno.Criar(contaId, "João", DateTime.UtcNow).Value;

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var result = await _handler.HandleAsync(new AtualizarPerfilCommand("João Silva"));

        result.IsSuccess.Should().BeTrue();
        aluno.Nome.Should().Be("João Silva");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AtualizaTreinador_Comita()
    {
        var contaId = Guid.NewGuid();
        var treinador = Treinador.Criar(contaId, "Carlos", DateTime.UtcNow).Value;

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Treinador);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new AtualizarPerfilCommand("Carlos Novo"));

        result.IsSuccess.Should().BeTrue();
        treinador.Nome.Should().Be("Carlos Novo");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PerfilNaoEncontrado_LancaEstadoInconsistente()
    {
        var contaId = Guid.NewGuid();
        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Aluno);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync((Aluno?)null);

        var act = async () => await _handler.HandleAsync(new AtualizarPerfilCommand("X"));
        await act.Should().ThrowAsync<EstadoInconsistenteException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_AtualizaSystemAdmin_Comita()
    {
        var contaId = Guid.NewGuid();
        var admin = SystemUser.Criar(contaId, "Admin", DateTime.UtcNow).Value;

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.SystemAdmin);
        _systemUserRepo.Setup(r => r.ObterPorContaIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        var result = await _handler.HandleAsync(new AtualizarPerfilCommand("Admin Novo"));

        result.IsSuccess.Should().BeTrue();
        admin.Nome.Should().Be("Admin Novo");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AdminNaoEncontrado_LancaEstadoInconsistente()
    {
        var contaId = Guid.NewGuid();
        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.SystemAdmin);
        _systemUserRepo.Setup(r => r.ObterPorContaIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync((SystemUser?)null);

        var act = async () => await _handler.HandleAsync(new AtualizarPerfilCommand("X"));
        await act.Should().ThrowAsync<EstadoInconsistenteException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaEstadoInconsistente()
    {
        var contaId = Guid.NewGuid();
        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _userContext.Setup(u => u.TipoConta).Returns(TipoConta.Treinador);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new AtualizarPerfilCommand("X"));
        await act.Should().ThrowAsync<EstadoInconsistenteException>();
    }

    [Fact]
    public async Task HandleAsync_TipoContaInvalido_LancaEstadoInconsistente()
    {
        _userContext.Setup(u => u.ContaId).Returns(Guid.NewGuid());
        _userContext.Setup(u => u.TipoConta).Returns((TipoConta)99);

        var act = async () => await _handler.HandleAsync(new AtualizarPerfilCommand("X"));

        await act.Should().ThrowAsync<EstadoInconsistenteException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
