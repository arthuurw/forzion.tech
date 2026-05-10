using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Logout;
using Microsoft.Extensions.Logging;
using Moq;
using DomainTokenRevogado = forzion.tech.Domain.Entities.TokenRevogado;

namespace forzion.tech.Tests.Application.ContaTestes;

public class LogoutHandlerTests
{
    private readonly Mock<ITokenRevogadoRepository> _tokenRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<LogoutHandler>> _logger = new();
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        _handler = new LogoutHandler(_tokenRepo.Object, _userContext.Object, _unitOfWork.Object, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_TokenValido_RevogaECommita()
    {
        var jti = Guid.NewGuid();
        _userContext.Setup(u => u.Jti).Returns(jti);
        _userContext.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddHours(1));

        await _handler.HandleAsync();

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<DomainTokenRevogado>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_JtiVazio_NaoRevogaNemCommita()
    {
        _userContext.Setup(u => u.Jti).Returns(Guid.Empty);
        _userContext.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddHours(1));

        await _handler.HandleAsync();

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<DomainTokenRevogado>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TokenJaExpirado_NaoRevogaNemCommita()
    {
        _userContext.Setup(u => u.Jti).Returns(Guid.NewGuid());
        _userContext.Setup(u => u.TokenExpiraEm).Returns(DateTime.UtcNow.AddHours(-1));

        await _handler.HandleAsync();

        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<DomainTokenRevogado>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
