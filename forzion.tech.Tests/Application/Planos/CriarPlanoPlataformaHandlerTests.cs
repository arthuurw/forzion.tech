using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Planos.CriarPlanoPlataforma;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Planos;

public class CriarPlanoPlataformaHandlerTests
{
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CriarPlanoPlataformaCommand>> _validator = new();
    private readonly Mock<ILogger<CriarPlanoPlataformaHandler>> _logger = new();
    private readonly CriarPlanoPlataformaHandler _handler;

    public CriarPlanoPlataformaHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _handler = new CriarPlanoPlataformaHandler(_planoRepo.Object, _unitOfWork.Object, _validator.Object, Mock.Of<IUserContext>(), TimeProvider.System, _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CriaERetornaResponse()
    {
        var command = new CriarPlanoPlataformaCommand("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 99.90m);

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Nome.Should().Be("Starter");
        result.Value.MaxAlunos.Should().Be(10);
        result.Value.Preco.Should().Be(99.90m);
        _planoRepo.Verify(r => r.AdicionarAsync(It.IsAny<PlanoPlataforma>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
