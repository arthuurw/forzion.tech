using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.DadosFiscais;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Treinadores;

public class DefinirDadosFiscaisTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<DefinirDadosFiscaisTreinadorHandler>> _logger = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly Guid _atorId = Guid.NewGuid();
    private readonly DefinirDadosFiscaisTreinadorHandler _handler;

    private static DefinirDadosFiscaisTreinadorCommand BuildCommand(Guid treinadorId) =>
        new(treinadorId, TipoDocumentoFiscal.Cpf, "11144477735", "Razao Social Ltda",
            "Rua das Flores", "123", "Centro", "2304400", "CE", "60000000");

    public DefinirDadosFiscaisTreinadorHandlerTests()
    {
        _userContext.Setup(u => u.PerfilId).Returns(_atorId);
        _handler = new DefinirDadosFiscaisTreinadorHandler(
            _treinadorRepo.Object, _logRepo.Object, _unitOfWork.Object,
            _timeProvider, _logger.Object, _userContext.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_RegistraAuditoria()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(BuildCommand(treinador.Id));

        result.IsSuccess.Should().BeTrue();
        _logRepo.Verify(r => r.AdicionarAsync(
            It.Is<LogAprovacao>(l => l.TipoAcao == TipoAcaoAprovacao.DefinicaoDadosFiscaisTreinador),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_LogRealizadoPorIdEAtorId()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        LogAprovacao? logCapturado = null;
        _logRepo.Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
            .Callback<LogAprovacao, CancellationToken>((log, _) => logCapturado = log);

        await _handler.HandleAsync(BuildCommand(treinador.Id));

        logCapturado.Should().NotBeNull();
        logCapturado!.RealizadoPorId.Should().Be(_atorId);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_RetornaFalha()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var result = await _handler.HandleAsync(BuildCommand(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TreinadorErrors.NaoEncontrado.Code);
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
