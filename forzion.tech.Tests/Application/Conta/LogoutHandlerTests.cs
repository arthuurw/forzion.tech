using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Logout;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using DomainTokenRevogado = forzion.tech.Domain.Entities.TokenRevogado;

namespace forzion.tech.Tests.Application.ContaTestes;

public class LogoutHandlerTests
{
    private readonly Mock<ITokenRevogadoRepository> _tokenRepo = new();
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<LogoutHandler>> _logger = new();
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        _userContext.Setup(u => u.FamiliaId).Returns(Guid.Empty);
        _handler = new LogoutHandler(_tokenRepo.Object, _refresh.Object, _userContext.Object, _unitOfWork.Object, new NpgsqlDatabaseErrorInspector(), TimeProvider.System, _logger.Object);
    }

    private static PostgresException Unique() => new("dup", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);

    [Fact]
    public async Task HandleAsync_TokenValido_RevogaECommita()
    {
        var jti = Guid.NewGuid();
        _userContext.Setup(u => u.Jti).Returns(jti);
        _userContext.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddHours(1));

        var result = await _handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<DomainTokenRevogado>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ComFamilia_RevogaFamiliaDoDevice()
    {
        var familiaId = Guid.NewGuid();
        _userContext.Setup(u => u.Jti).Returns(Guid.NewGuid());
        _userContext.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddHours(1));
        _userContext.Setup(u => u.FamiliaId).Returns(familiaId);

        await _handler.HandleAsync();

        _refresh.Verify(r => r.RevogarFamiliaAsync(familiaId, MotivoRevogacaoFamilia.Logout, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_JtiVazio_NaoRevogaNemCommita()
    {
        _userContext.Setup(u => u.Jti).Returns(Guid.Empty);
        _userContext.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddHours(1));

        var result = await _handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<DomainTokenRevogado>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TokenJaExpirado_NaoRevogaNemCommita()
    {
        _userContext.Setup(u => u.Jti).Returns(Guid.NewGuid());
        _userContext.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddHours(-1));

        var result = await _handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<DomainTokenRevogado>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Unicidade23505Cru_TrataIdempotenteERetornaSuccess()
    {
        _userContext.Setup(u => u.Jti).Returns(Guid.NewGuid());
        _userContext.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddHours(1));

        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<DomainTokenRevogado>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(Unique());

        var result = await _handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Unicidade23505Reembrulhado_TrataIdempotenteERetornaSuccess()
    {
        _userContext.Setup(u => u.Jti).Returns(Guid.NewGuid());
        _userContext.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddHours(1));

        var dbEx = new InvalidOperationException("transient failure",
            new DbUpdateException("save falhou", Unique()));
        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<DomainTokenRevogado>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(dbEx);

        var result = await _handler.HandleAsync();

        result.IsSuccess.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ExcecaoNaoRelacionadaAConflito_Propaga()
    {
        _userContext.Setup(u => u.Jti).Returns(Guid.NewGuid());
        _userContext.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddHours(1));

        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<DomainTokenRevogado>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new InvalidOperationException("erro inesperado"));

        var act = async () => await _handler.HandleAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
