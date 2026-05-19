using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Planos.CriarPlanoTreinador;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Planos;

public class CriarPlanoTreinadorHandlerTests
{
    private readonly Mock<IPlanoTreinadorRepository> _planoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CriarPlanoTreinadorCommand>> _validator = new();
    private readonly Mock<ILogger<CriarPlanoTreinadorHandler>> _logger = new();
    private readonly CriarPlanoTreinadorHandler _handler;

    public CriarPlanoTreinadorHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _handler = new CriarPlanoTreinadorHandler(_planoRepo.Object, _unitOfWork.Object, _validator.Object, Mock.Of<IUserContext>(), _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CriaERetornaResponse()
    {
        var command = new CriarPlanoTreinadorCommand("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 99.90m);

        var result = await _handler.HandleAsync(command);

        result.Nome.Should().Be("Starter");
        result.MaxAlunos.Should().Be(10);
        result.Preco.Should().Be(99.90m);
        _planoRepo.Verify(r => r.AdicionarAsync(It.IsAny<PlanoTreinador>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
