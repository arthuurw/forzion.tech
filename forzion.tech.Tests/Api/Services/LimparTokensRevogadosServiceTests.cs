using forzion.tech.Api.Services;
using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace forzion.tech.Tests.Api.Services;

public class LimparTokensRevogadosServiceTests
{
    private readonly Mock<ITokenRevogadoRepository> _tokenRepo = new();
    private readonly Mock<IRefreshTokenFamilyRepository> _familyRepo = new();
    private readonly Mock<IMfaChallengeRepository> _challengeRepo = new();
    private readonly Mock<ITrustedDeviceRepository> _deviceRepo = new();

    private LimparTokensRevogadosService CriarService()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _tokenRepo.Object);
        services.AddScoped(_ => _familyRepo.Object);
        services.AddScoped(_ => _challengeRepo.Object);
        services.AddScoped(_ => _deviceRepo.Object);
        services.AddSingleton(TimeProvider.System);
        return new LimparTokensRevogadosService(
            services.BuildServiceProvider(),
            NullLogger<LimparTokensRevogadosService>.Instance);
    }

    [Fact]
    public async Task LimparAsync_PurgaTokensFamiliasDesafiosEDispositivos()
    {
        _tokenRepo.Setup(r => r.LimparExpiradosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);
        _familyRepo.Setup(r => r.LimparExpiradasAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _challengeRepo.Setup(r => r.LimparExpiradosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(4);
        _deviceRepo.Setup(r => r.LimparExpiradosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CriarService().LimparAsync(CancellationToken.None);

        _tokenRepo.Verify(r => r.LimparExpiradosAsync(It.IsAny<CancellationToken>()), Times.Once);
        _familyRepo.Verify(r => r.LimparExpiradasAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _challengeRepo.Verify(r => r.LimparExpiradosAsync(It.IsAny<CancellationToken>()), Times.Once);
        _deviceRepo.Verify(r => r.LimparExpiradosAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LimparAsync_FalhaNaPrimeiraPurga_NaoPulaASegunda()
    {
        // try/catch independente: a purga de famílias roda mesmo se a de tokens revogados falhar.
        _tokenRepo.Setup(r => r.LimparExpiradosAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));
        _familyRepo.Setup(r => r.LimparExpiradasAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CriarService().LimparAsync(CancellationToken.None);

        _familyRepo.Verify(r => r.LimparExpiradasAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LimparAsync_FalhaNoDesafio_NaoPulaDispositivos()
    {
        _challengeRepo.Setup(r => r.LimparExpiradosAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));
        _deviceRepo.Setup(r => r.LimparExpiradosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        await CriarService().LimparAsync(CancellationToken.None);

        _deviceRepo.Verify(r => r.LimparExpiradosAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
