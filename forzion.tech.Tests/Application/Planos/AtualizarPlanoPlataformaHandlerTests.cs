using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Planos.AtualizarPlanoPlataforma;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Planos;

public class AtualizarPlanoPlataformaHandlerTests
{
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<AtualizarPlanoPlataformaCommand>> _validator = new();
    private readonly AtualizarPlanoPlataformaHandler _handler;

    public AtualizarPlanoPlataformaHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _handler = new AtualizarPlanoPlataformaHandler(_planoRepo.Object, _unitOfWork.Object, _validator.Object, TimeProvider.System);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_AtualizaERetornaResponse()
    {
        var plano = PlanoPlataforma.Criar("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 99.90m, DateTime.UtcNow).Value;
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);

        var result = await _handler.HandleAsync(new AtualizarPlanoPlataformaCommand(plano.Id, "Pro", null, 20, 199.90m));

        result.IsSuccess.Should().BeTrue();
        result.Value.Nome.Should().Be("Pro");
        result.Value.MaxAlunos.Should().Be(20);
        result.Value.Preco.Should().Be(199.90m);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ApenasNome_AtualizaSoNome()
    {
        var plano = PlanoPlataforma.Criar("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 99.90m, DateTime.UtcNow).Value;
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);

        var result = await _handler.HandleAsync(new AtualizarPlanoPlataformaCommand(plano.Id, "Pro", null, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Nome.Should().Be("Pro");
        result.Value.MaxAlunos.Should().Be(10);
        result.Value.Preco.Should().Be(99.90m);
    }

    [Fact]
    public async Task HandleAsync_PlanoNaoEncontrado_LancaDomainException()
    {
        _planoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PlanoPlataforma?)null);

        var act = async () => await _handler.HandleAsync(new AtualizarPlanoPlataformaCommand(Guid.NewGuid(), "Pro", null, null, null));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*não encontrado*");
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
