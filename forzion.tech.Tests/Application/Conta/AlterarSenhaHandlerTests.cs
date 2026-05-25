using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.AlterarSenha;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Moq;
using DomainConta = forzion.tech.Domain.Entities.Conta;

namespace forzion.tech.Tests.Application.ContaTestes;

public class AlterarSenhaHandlerTests
{
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<AlterarSenhaCommand>> _validator = new();
    private readonly AlterarSenhaHandler _handler;

    public AlterarSenhaHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<AlterarSenhaCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _handler = new AlterarSenhaHandler(
            _userContext.Object,
            _contaRepo.Object,
            _passwordHasher.Object,
            _unitOfWork.Object,
            _validator.Object);
    }

    private static DomainConta CriarConta() =>
        DomainConta.Criar(Email.Criar("user@test.com"), "hash-atual", TipoConta.Aluno, DateTime.UtcNow);

    [Fact]
    public async Task HandleAsync_SenhaCorreta_AtualizaEComita()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta();

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _passwordHasher.Setup(h => h.Verify("senha123", conta.PasswordHash)).Returns(true);
        _passwordHasher.Setup(h => h.Hash("nova-senha")).Returns("novo-hash");

        await _handler.HandleAsync(new AlterarSenhaCommand("senha123", "nova-senha"));

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SenhaIncorreta_LancaCredenciaisInvalidasException()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta();

        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), conta.PasswordHash)).Returns(false);

        var act = async () => await _handler.HandleAsync(new AlterarSenhaCommand("errada", "nova-senha"));
        await act.Should().ThrowAsync<CredenciaisInvalidasException>();
    }

    [Fact]
    public async Task HandleAsync_ContaNaoEncontrada_LancaDomainException()
    {
        var contaId = Guid.NewGuid();
        _userContext.Setup(u => u.ContaId).Returns(contaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync((DomainConta?)null);

        var act = async () => await _handler.HandleAsync(new AlterarSenhaCommand("senha123", "nova-senha"));
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
