using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.PerfilPublico;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Time.Testing;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Treinadores;

public class DefinirPerfilPublicoTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero));
    private readonly DefinirPerfilPublicoTreinadorHandler _handler;

    public DefinirPerfilPublicoTreinadorHandlerTests()
    {
        _handler = new DefinirPerfilPublicoTreinadorHandler(_treinadorRepo.Object, _unitOfWork.Object, _timeProvider);
    }

    private static DefinirPerfilPublicoTreinadorCommand BuildCommand(
        Guid treinadorId, string? nomeFantasia = "Studio X", bool isPublicado = true,
        EnderecoPublicoInput? endereco = null, IReadOnlyList<HorarioFuncionamentoInput>? horarios = null,
        string fusoHorario = "America/Sao_Paulo") =>
        new(treinadorId, nomeFantasia, endereco, null, horarios ?? [], isPublicado, fusoHorario);

    [Fact]
    public async Task HandleAsync_DadosValidosPublicando_PublicaESalva()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(BuildCommand(treinador.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.NomeFantasia.Should().Be("Studio X");
        result.Value.IsPublicado.Should().BeTrue();
        treinador.PerfilPublico.IsPublicado.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PublicarSemNomeFantasia_Falha()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(BuildCommand(treinador.Id, nomeFantasia: null, isPublicado: true));

        result.IsFailure.Should().BeTrue();
        treinador.PerfilPublico.IsPublicado.Should().BeFalse();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_IsPublicadoFalse_DespublicaSemExigirNomeFantasia()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(BuildCommand(treinador.Id, nomeFantasia: null, isPublicado: false));

        result.IsSuccess.Should().BeTrue();
        treinador.PerfilPublico.IsPublicado.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_HorarioInvalido_FalhaSemCommitar()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        var horarios = new List<HorarioFuncionamentoInput> { new(1, new TimeOnly(18, 0), new TimeOnly(8, 0)) };

        var result = await _handler.HandleAsync(BuildCommand(treinador.Id, isPublicado: false, horarios: horarios));

        result.IsFailure.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_EnderecoInvalido_Falha()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        var endereco = new EnderecoPublicoInput("", null, null, "Centro", "Fortaleza", "CE", "60000000");

        var result = await _handler.HandleAsync(BuildCommand(treinador.Id, isPublicado: false, endereco: endereco));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_RetornaFalha()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var result = await _handler.HandleAsync(BuildCommand(Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TreinadorErrors.NaoEncontrado.Code);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // --- AGF3-16 / AGF3-33: fuso no contrato de perfil público ---

    [Fact]
    public async Task HandleAsync_FusoValido_PersisteEDevolveNaResposta()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(BuildCommand(treinador.Id, isPublicado: false, fusoHorario: "America/Manaus"));

        result.IsSuccess.Should().BeTrue();
        result.Value.FusoHorario.Should().Be("America/Manaus");
        treinador.FusoHorario.Should().Be("America/Manaus");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FusoInvalido_FalhaSemCommitarENaoPersisteOsHorariosDaMesmaRequisicao()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        var horarios = new List<HorarioFuncionamentoInput> { new(1, new TimeOnly(8, 0), new TimeOnly(18, 0)) };

        var result = await _handler.HandleAsync(BuildCommand(
            treinador.Id, isPublicado: false, horarios: horarios, fusoHorario: "Nao/Existe"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.FusoHorarioInvalido);
        treinador.FusoHorario.Should().Be("America/Sao_Paulo");
        treinador.PerfilPublico.HorariosFuncionamento.Should().BeEmpty();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
