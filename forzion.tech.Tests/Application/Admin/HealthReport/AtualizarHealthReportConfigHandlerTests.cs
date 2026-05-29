using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Admin.HealthReport;

public class AtualizarHealthReportConfigHandlerTests
{
    private readonly Mock<IHealthReportConfigRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<AtualizarHealthReportConfigCommand>> _validator = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero));
    private readonly AtualizarHealthReportConfigHandler _handler;

    public AtualizarHealthReportConfigHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _handler = new AtualizarHealthReportConfigHandler(_repo.Object, _unitOfWork.Object, _time, _validator.Object);
    }

    private static AtualizarHealthReportConfigCommand Command(bool ativo = true) => new(
        ativo, new TimeOnly(9, 30), new[] { "ops@forzion.tech" }, true, true, true, true);

    [Fact]
    public async Task HandleAsync_SemConfigExistente_CriaEAdiciona()
    {
        _repo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync((HealthReportConfig?)null);

        var result = await _handler.HandleAsync(Command());

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.Ativo.Should().BeTrue();
        result.Value.HoraEnvioUtc.Should().Be(new TimeOnly(9, 30));
        result.Value.Destinatarios.Should().ContainSingle().Which.Should().Be("ops@forzion.tech");
        _repo.Verify(r => r.AdicionarAsync(It.IsAny<HealthReportConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ConfigExistente_AtualizaSemAdicionar()
    {
        var existente = HealthReportConfig.Criar(false, new TimeOnly(6, 0), Array.Empty<string>(),
            false, false, false, false, DateTime.UtcNow).Value;
        _repo.Setup(r => r.ObterAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existente);

        var result = await _handler.HandleAsync(Command());

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existente.Id);
        result.Value.Ativo.Should().BeTrue();
        result.Value.HoraEnvioUtc.Should().Be(new TimeOnly(9, 30));
        _repo.Verify(r => r.AdicionarAsync(It.IsAny<HealthReportConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
