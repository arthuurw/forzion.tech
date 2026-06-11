using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Handlers;

public class VinculoAprovadoCriarAssinaturaAlunoHandlerTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<VinculoAprovadoCriarAssinaturaAlunoHandler>> _logger = new();
    private readonly VinculoAprovadoCriarAssinaturaAlunoHandler _handler;

    private static readonly VinculoAprovadoEvent Evento = new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

    public VinculoAprovadoCriarAssinaturaAlunoHandlerTests()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Treinador.Criar(Guid.NewGuid(), "Treinador", DateTime.UtcNow, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value);
        var criarService = new CriarAssinaturaAlunoService(
            _pacoteRepo.Object, _assinaturaRepo.Object, Mock.Of<ILogger<CriarAssinaturaAlunoService>>());
        _handler = new VinculoAprovadoCriarAssinaturaAlunoHandler(
            _vinculoRepo.Object, _assinaturaRepo.Object, _contaRecebimentoRepo.Object, _treinadorRepo.Object,
            criarService, _unitOfWork.Object, _logger.Object);
    }

    private static ContaRecebimento ContaOnboarded(Guid treinadorId)
    {
        var conta = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        conta.ConfigurarStripeConnect("acct_123", DateTime.UtcNow);
        conta.ConfirmarOnboarding(DateTime.UtcNow);
        return conta;
    }

    [Fact]
    public async Task HandleAsync_VinculoSemPacote_NaoCriaAssinaturaAluno()
    {
        var vinculo = VinculoTreinadorAluno.Criar(Evento.TreinadorId, Evento.AlunoId, DateTime.UtcNow).Value;
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(Evento.VinculoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);

        await _handler.HandleAsync(Evento);

        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemOnboarding_NaoCriaAssinaturaAluno()
    {
        var pacoteId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(Evento.TreinadorId, Evento.AlunoId, DateTime.UtcNow).Value;
        vinculo.Aprovar(Evento.TreinadorId, pacoteId, DateTime.UtcNow);
        var contaRecebimento = ContaRecebimento.Criar(Evento.TreinadorId, DateTime.UtcNow).Value;

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(Evento.VinculoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);

        await _handler.HandleAsync(Evento);

        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorModoExterno_NaoCriaAssinaturaAluno()
    {
        var pacoteId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(Evento.TreinadorId, Evento.AlunoId, DateTime.UtcNow).Value;
        vinculo.Aprovar(Evento.TreinadorId, pacoteId, DateTime.UtcNow);
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(Evento.VinculoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Treinador.Criar(Guid.NewGuid(), "Externo", DateTime.UtcNow, modoPagamentoAluno: ModoPagamentoAluno.Externo).Value);

        await _handler.HandleAsync(Evento);

        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()), Times.Never);
        _pacoteRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_VinculoNaoEncontrado_NaoCriaAssinaturaAluno()
    {
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(Evento.VinculoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        await _handler.HandleAsync(Evento);

        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_HappyPath_CriaAssinaturaAlunoECommita()
    {
        var pacoteId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(Evento.TreinadorId, Evento.AlunoId, DateTime.UtcNow).Value;
        vinculo.Aprovar(Evento.TreinadorId, pacoteId, DateTime.UtcNow);

        var contaRecebimento = ContaOnboarded(Evento.TreinadorId);

        var pacote = Pacote.Criar(Evento.TreinadorId, "Plano mensal", 150m, DateTime.UtcNow).Value;

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(Evento.VinculoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaRecebimento);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pacote);

        await _handler.HandleAsync(Evento);

        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
