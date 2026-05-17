using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Handlers;

public class VinculoAprovadoCriarAssinaturaHandlerTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IPacoteAlunoRepository> _pacoteRepo = new();
    private readonly Mock<IAssinaturaRepository> _assinaturaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<VinculoAprovadoCriarAssinaturaHandler>> _logger = new();
    private readonly VinculoAprovadoCriarAssinaturaHandler _handler;

    private static readonly VinculoAprovadoEvent Evento = new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

    public VinculoAprovadoCriarAssinaturaHandlerTests()
    {
        _handler = new VinculoAprovadoCriarAssinaturaHandler(
            _vinculoRepo.Object, _pacoteRepo.Object, _assinaturaRepo.Object,
            _treinadorRepo.Object, _unitOfWork.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_VinculoSemPacote_NaoCriaAssinatura()
    {
        var vinculo = VinculoTreinadorAluno.Criar(Evento.TreinadorId, Evento.AlunoId);
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(Evento.VinculoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);

        await _handler.HandleAsync(Evento);

        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<Assinatura>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemOnboarding_NaoCriaAssinatura()
    {
        var pacoteId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(Evento.TreinadorId, Evento.AlunoId);
        vinculo.Aprovar(Evento.TreinadorId, pacoteId);
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(Evento.VinculoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        await _handler.HandleAsync(Evento);

        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<Assinatura>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_VinculoNaoEncontrado_NaoCriaAssinatura()
    {
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(Evento.VinculoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        await _handler.HandleAsync(Evento);

        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<Assinatura>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_HappyPath_CriaAssinaturaECommita()
    {
        var pacoteId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(Evento.TreinadorId, Evento.AlunoId);
        vinculo.Aprovar(Evento.TreinadorId, pacoteId);

        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        treinador.ConfigurarStripeConnect("acct_123");
        treinador.ConfirmarOnboarding();

        var pacote = PacoteAluno.Criar(Evento.TreinadorId, "Plano mensal", 150m);

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(Evento.VinculoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(Evento.TreinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pacote);

        await _handler.HandleAsync(Evento);

        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<Assinatura>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
