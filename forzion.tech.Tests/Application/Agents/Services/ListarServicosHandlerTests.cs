using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Agents.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Tests.Builders;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Application.Agents.Services;

public class ListarServicosHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly ListarServicosHandler _handler;

    public ListarServicosHandlerTests()
    {
        _handler = new ListarServicosHandler(_treinadorRepo.Object, _pacoteRepo.Object);
    }

    private static Treinador CriarTreinadorAtivoEPublicado()
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio Teste", null, null, DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        return treinador;
    }

    private static Pacote CriarPacotePublico(Guid treinadorId, string categoria, decimal preco = 100m, bool trial = false)
    {
        var pacote = Pacote.Criar(treinadorId, "Pacote " + categoria, preco, DateTime.UtcNow).Value;
        pacote.AtualizarCatalogoPublico(categoria, 60, trial, DateTime.UtcNow);
        pacote.TornarPublico(DateTime.UtcNow);
        return pacote;
    }

    private void SetupTreinadorPublicado(Treinador treinador) =>
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

    [Fact]
    public async Task HandleAsync_TreinadorInexistente_RetornaTenantNotFound()
    {
        var tenantId = Guid.NewGuid();
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var result = await _handler.HandleAsync(new ListarServicosQuery(tenantId, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_TreinadorInativo_RetornaMesmoErroDoInexistente()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        treinador.Inativar(DateTime.UtcNow);
        SetupTreinadorPublicado(treinador);

        var result = await _handler.HandleAsync(new ListarServicosQuery(treinador.Id, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_PerfilNaoPublicado_RetornaMesmoErroDoInexistente()
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        SetupTreinadorPublicado(treinador);

        var result = await _handler.HandleAsync(new ListarServicosQuery(treinador.Id, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_CatalogoVazioSemFiltro_RetornaSucessoComListaVazia()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        SetupTreinadorPublicado(treinador);
        _pacoteRepo.Setup(r => r.ListarPublicosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Pacote>());

        var result = await _handler.HandleAsync(new ListarServicosQuery(treinador.Id, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_CatalogoVazioComFiltro_RetornaSucessoComListaVazia()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        SetupTreinadorPublicado(treinador);
        _pacoteRepo.Setup(r => r.ListarPublicosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Pacote>());

        var result = await _handler.HandleAsync(new ListarServicosQuery(treinador.Id, "pilates"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ComFiltroCategoryCaseInsensitive_FiltraCorretamente()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        SetupTreinadorPublicado(treinador);
        _pacoteRepo.Setup(r => r.ListarPublicosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Pacote>
            {
                CriarPacotePublico(treinador.Id, "Pilates"),
                CriarPacotePublico(treinador.Id, "Funcional"),
            });

        var result = await _handler.HandleAsync(new ListarServicosQuery(treinador.Id, "PILATES"));

        result.Value.Should().ContainSingle(s => s.Category == "Pilates");
    }

    [Fact]
    public async Task HandleAsync_PrecoZero_NuncaOmiteOCampoPrice()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        SetupTreinadorPublicado(treinador);
        _pacoteRepo.Setup(r => r.ListarPublicosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Pacote> { CriarPacotePublico(treinador.Id, "Aula gratis", preco: 0m) });

        var result = await _handler.HandleAsync(new ListarServicosQuery(treinador.Id, null));

        result.Value.Single().Price.Should().NotBeNull();
        result.Value.Single().Price.Amount.Should().Be(0m);
        result.Value.Single().Price.Currency.Should().Be("BRL");
    }

    [Fact]
    public async Task HandleAsync_MapeiaTodosOsCamposExplicitamente()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        SetupTreinadorPublicado(treinador);
        var pacote = CriarPacotePublico(treinador.Id, "Funcional", preco: 150m, trial: true);
        _pacoteRepo.Setup(r => r.ListarPublicosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Pacote> { pacote });

        var result = await _handler.HandleAsync(new ListarServicosQuery(treinador.Id, null));

        var service = result.Value.Single();
        service.Id.Should().Be(pacote.Id.ToString());
        service.Name.Should().Be(pacote.Nome);
        service.Category.Should().Be("Funcional");
        service.DurationMinutes.Should().Be(60);
        service.TrialAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
