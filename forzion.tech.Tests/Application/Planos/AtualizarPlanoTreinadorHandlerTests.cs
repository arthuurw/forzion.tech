using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Planos.AtualizarPlanoTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Planos;

public class AtualizarPlanoTreinadorHandlerTests
{
    private readonly Mock<IPlanoTreinadorRepository> _planoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<AtualizarPlanoTreinadorCommand>> _validator = new();
    private readonly AtualizarPlanoTreinadorHandler _handler;

    public AtualizarPlanoTreinadorHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _handler = new AtualizarPlanoTreinadorHandler(_planoRepo.Object, _unitOfWork.Object, _validator.Object);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_AtualizaERetornaResponse()
    {
        var plano = PlanoTreinador.Criar("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 99.90m);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);

        var result = await _handler.HandleAsync(new AtualizarPlanoTreinadorCommand(plano.Id, "Pro", null, 20, 199.90m));

        result.Nome.Should().Be("Pro");
        result.MaxAlunos.Should().Be(20);
        result.Preco.Should().Be(199.90m);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ApenasNome_AtualizaSoNome()
    {
        var plano = PlanoTreinador.Criar("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 99.90m);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);

        var result = await _handler.HandleAsync(new AtualizarPlanoTreinadorCommand(plano.Id, "Pro", null, null, null));

        result.Nome.Should().Be("Pro");
        result.MaxAlunos.Should().Be(10);
        result.Preco.Should().Be(99.90m);
    }

    [Fact]
    public async Task HandleAsync_PlanoNaoEncontrado_LancaDomainException()
    {
        _planoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PlanoTreinador?)null);

        var act = async () => await _handler.HandleAsync(new AtualizarPlanoTreinadorCommand(Guid.NewGuid(), "Pro", null, null, null));
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
