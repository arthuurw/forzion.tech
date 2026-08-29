using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pacotes.AtualizarPacote;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Pacotes;

public class AtualizarPacoteHandlerTests
{
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<AtualizarPacoteCommand>> _validator = new();
    private readonly AtualizarPacoteHandler _handler;

    public AtualizarPacoteHandlerTests()
    {
        _validator.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _handler = new AtualizarPacoteHandler(_pacoteRepo.Object, _unitOfWork.Object, _validator.Object, TimeProvider.System);
    }

    private static Pacote CriarPacote(Guid treinadorId) =>
        Pacote.Criar(treinadorId, "Básico", 100m, DateTime.UtcNow).Value;

    [Fact]
    public async Task HandleAsync_PacoteExistenteMesmoDono_AtualizaECommita()
    {
        var treinadorId = Guid.NewGuid();
        var pacote = CriarPacote(treinadorId);
        var command = new AtualizarPacoteCommand(treinadorId, pacote.Id, "Premium", 200m, "Desc");

        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Nome.Should().Be("Premium");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PacoteNaoEncontrado_LancaPacoteNaoEncontradoException()
    {
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pacote?)null);

        var act = async () => await _handler.HandleAsync(
            new AtualizarPacoteCommand(Guid.NewGuid(), Guid.NewGuid(), "X", 1m, null));

        await act.Should().ThrowAsync<PacoteNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_LancaAcessoNegadoException()
    {
        var pacote = CriarPacote(Guid.NewGuid());
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var act = async () => await _handler.HandleAsync(
            new AtualizarPacoteCommand(Guid.NewGuid(), pacote.Id, "X", 1m, null));

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_IsPublicoTrueComCategoria_TornaPacotePublico()
    {
        var treinadorId = Guid.NewGuid();
        var pacote = CriarPacote(treinadorId);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var command = new AtualizarPacoteCommand(treinadorId, pacote.Id, null, null, null, Categoria: "Pilates", DuracaoMinutos: 60, IsPublico: true);
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.Categoria.Should().Be("Pilates");
        result.Value.IsPublico.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_IsPublicoTrueSemCategoria_Falha()
    {
        var treinadorId = Guid.NewGuid();
        var pacote = CriarPacote(treinadorId);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var command = new AtualizarPacoteCommand(treinadorId, pacote.Id, null, null, null, IsPublico: true);
        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        pacote.IsPublico.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_IsPublicoTrueSemDuracao_Falha()
    {
        var treinadorId = Guid.NewGuid();
        var pacote = CriarPacote(treinadorId);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var command = new AtualizarPacoteCommand(treinadorId, pacote.Id, null, null, null, Categoria: "Pilates", IsPublico: true);
        var result = await _handler.HandleAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("pacote.duracao_obrigatoria_para_publico");
        pacote.IsPublico.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_IsPublicoFalse_TornaPacotePrivado()
    {
        var treinadorId = Guid.NewGuid();
        var pacote = CriarPacote(treinadorId);
        pacote.AtualizarCatalogoPublico("Pilates", 60, null, DateTime.UtcNow);
        pacote.TornarPublico(DateTime.UtcNow);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var command = new AtualizarPacoteCommand(treinadorId, pacote.Id, null, null, null, IsPublico: false);
        var result = await _handler.HandleAsync(command);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsPublico.Should().BeFalse();
    }
}
