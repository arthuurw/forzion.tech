using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Treinadores;

public class RegistrarTreinadorHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<RegistrarTreinadorHandler>> _logger = new();
    private readonly RegistrarTreinadorHandler _handler;

    public RegistrarTreinadorHandlerTests()
    {
        _passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hash");
        _handler = new RegistrarTreinadorHandler(
            _contaRepo.Object, _treinadorRepo.Object, _passwordHasher.Object,
            _unitOfWork.Object, new RegistrarTreinadorCommandValidator(), TimeProvider.System, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CriaContaETreinador()
    {
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var result = await _handler.HandleAsync(new RegistrarTreinadorCommand("ana@teste.com", "Senha123", "Ana"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Nome.Should().Be("Ana");
        result.Value.Status.Should().Be(TreinadorStatus.AguardandoAprovacao);
        _contaRepo.Verify(r => r.AdicionarAsync(It.IsAny<Conta>(), It.IsAny<CancellationToken>()), Times.Once);
        _treinadorRepo.Verify(r => r.AdicionarAsync(It.IsAny<Treinador>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmailJaCadastrado_LancaException()
    {
        var conta = Conta.Criar(Email.Criar("ana@teste.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        var act = async () => await _handler.HandleAsync(new RegistrarTreinadorCommand("ana@teste.com", "Senha123", "Ana"));
        await act.Should().ThrowAsync<EmailJaCadastradoException>();
    }

    [Fact]
    public async Task HandleAsync_DadosInvalidos_LancaValidationException()
    {
        var act = async () => await _handler.HandleAsync(new RegistrarTreinadorCommand("invalido", "123", ""));
        await act.Should().ThrowAsync<ValidationException>();
    }
}
