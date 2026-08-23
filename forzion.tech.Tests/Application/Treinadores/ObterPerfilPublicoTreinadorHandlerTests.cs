using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.PerfilPublico;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared.Errors;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Treinadores;

public class ObterPerfilPublicoTreinadorHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly ObterPerfilPublicoTreinadorHandler _handler;

    public ObterPerfilPublicoTreinadorHandlerTests()
    {
        _handler = new ObterPerfilPublicoTreinadorHandler(_treinadorRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNuncaConfigurouPerfil_RetornaPerfilVazioNaoPublicado()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(treinador.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.NomeFantasia.Should().BeNull();
        result.Value.IsPublicado.Should().BeFalse();
        result.Value.HorariosFuncionamento.Should().BeEmpty();
        result.Value.FusoHorario.Should().Be("America/Sao_Paulo");
    }

    [Fact]
    public async Task HandleAsync_TreinadorComPerfilConfigurado_RetornaDadosSalvos()
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.DefinirFusoHorario("America/Manaus", DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio X", null, null, DateTime.UtcNow);
        treinador.PerfilPublico.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(12, 0), DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(treinador.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.NomeFantasia.Should().Be("Studio X");
        result.Value.IsPublicado.Should().BeTrue();
        result.Value.HorariosFuncionamento.Should().ContainSingle(h => h.AbreAs == "08:00" && h.FechaAs == "12:00");
        result.Value.FusoHorario.Should().Be("America/Manaus");
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_RetornaFalha()
    {
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var result = await _handler.HandleAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TreinadorErrors.NaoEncontrado.Code);
    }
}
