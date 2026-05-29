using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.AssinaturaAlunos.CriarAssinaturaAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.AssinaturaAlunos;

public class CriarAssinaturaAlunoHandlerTests
{
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CriarAssinaturaAlunoHandler>> _logger = new();
    private readonly CriarAssinaturaAlunoHandler _handler;

    public CriarAssinaturaAlunoHandlerTests()
    {
        _handler = new CriarAssinaturaAlunoHandler(
            _assinaturaRepo.Object,
            _contaRecebimentoRepo.Object,
            _pacoteRepo.Object,
            _unitOfWork.Object,
            TimeProvider.System,
            _logger.Object);
    }

    private static CriarAssinaturaAlunoCommand BuildCommand(Guid treinadorId, Guid pacoteId) => new(
        Guid.NewGuid(), pacoteId, treinadorId, Guid.NewGuid());

    private static ContaRecebimento ContaOnboarded(Guid treinadorId)
    {
        var conta = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        conta.ConfigurarStripeConnect("acct_123", TestData.Agora);
        conta.ConfirmarOnboarding(TestData.Agora);
        return conta;
    }

    private Pacote SetupPacote(Guid treinadorId, decimal preco = 150m)
    {
        var pacote = Pacote.Criar(treinadorId, "Mensal", preco, DateTime.UtcNow).Value;
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pacote);
        return pacote;
    }

    [Fact]
    public async Task HandleAsync_TreinadorComOnboarding_CriaAssinaturaAlunoComPrecoDoPacote()
    {
        var treinadorId = Guid.NewGuid();
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinadorId));
        var pacote = SetupPacote(treinadorId, preco: 199.90m);

        var result = await _handler.HandleAsync(BuildCommand(treinadorId, pacote.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.TreinadorId.Should().Be(treinadorId);
        result.Value.Valor.Should().Be(199.90m);
        _assinaturaRepo.Verify(r => r.AdicionarAsync(
                It.Is<AssinaturaAluno>(a => a.Valor == 199.90m),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PacoteDeOutroTreinador_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        var outroTreinadorId = Guid.NewGuid();
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinadorId));
        var pacote = SetupPacote(outroTreinadorId, preco: 100m);

        var result = await _handler.HandleAsync(BuildCommand(treinadorId, pacote.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Pacote não pertence ao treinador");
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PacoteNaoEncontrado_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContaOnboarded(treinadorId));
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pacote?)null);

        var result = await _handler.HandleAsync(BuildCommand(treinadorId, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Pacote não encontrado");
    }

    [Fact]
    public async Task HandleAsync_ContaRecebimentoSemOnboarding_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        var conta = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        conta.ConfigurarStripeConnect("acct_123", TestData.Agora);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);

        var result = await _handler.HandleAsync(BuildCommand(treinadorId, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("recebimentos");
    }

    [Fact]
    public async Task HandleAsync_SemContaRecebimento_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);

        var result = await _handler.HandleAsync(BuildCommand(treinadorId, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("recebimentos");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
