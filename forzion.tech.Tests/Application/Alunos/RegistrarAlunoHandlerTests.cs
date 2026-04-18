using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.RegistrarAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Alunos;

public class RegistrarAlunoHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<RegistrarAlunoHandler>> _logger = new();
    private readonly RegistrarAlunoHandler _handler;

    public RegistrarAlunoHandlerTests()
    {
        _passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hash");
        _handler = new RegistrarAlunoHandler(
            _contaRepo.Object, _alunoRepo.Object, _vinculoRepo.Object,
            _treinadorRepo.Object, _passwordHasher.Object, _unitOfWork.Object,
            new RegistrarAlunoCommandValidator(), _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CriaContaAlunoEVinculo()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");

        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new RegistrarAlunoCommand("joao@teste.com", "senha123", "João", treinadorId));

        result.Nome.Should().Be("João");
        result.Status.Should().Be(AlunoStatus.AguardandoAprovacao);
        _contaRepo.Verify(r => r.AdicionarAsync(It.IsAny<Conta>(), It.IsAny<CancellationToken>()), Times.Once);
        _alunoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Aluno>(), It.IsAny<CancellationToken>()), Times.Once);
        _vinculoRepo.Verify(r => r.AdicionarAsync(It.IsAny<VinculoTreinadorAluno>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmailJaCadastrado_LancaException()
    {
        var conta = Conta.Criar(Email.Criar("joao@teste.com"), "hash", TipoConta.Aluno);
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        var act = async () => await _handler.HandleAsync(new RegistrarAlunoCommand("joao@teste.com", "senha123", "João", Guid.NewGuid()));
        await act.Should().ThrowAsync<EmailJaCadastradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaException()
    {
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new RegistrarAlunoCommand("joao@teste.com", "senha123", "João", Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_DadosInvalidos_LancaValidationException()
    {
        var act = async () => await _handler.HandleAsync(new RegistrarAlunoCommand("invalido", "123", "", Guid.Empty));
        await act.Should().ThrowAsync<ValidationException>();
    }
}
