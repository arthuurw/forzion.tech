using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Agents.BusinessInfo;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.Builders;
using Moq;
using Xunit;

namespace forzion.tech.Tests.Application.Agents.BusinessInfo;

public class ObterBusinessInfoHandlerTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly ObterBusinessInfoHandler _handler;

    public ObterBusinessInfoHandlerTests()
    {
        _handler = new ObterBusinessInfoHandler(_treinadorRepo.Object, _pacoteRepo.Object);
    }

    private static Treinador CriarTreinadorAtivoEPublicado(string nomeFantasia = "Studio Teste")
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados(nomeFantasia, null, null, DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        return treinador;
    }

    private static Pacote CriarPacotePublico(Guid treinadorId, string categoria)
    {
        var pacote = Pacote.Criar(treinadorId, "Pacote " + categoria, 100m, DateTime.UtcNow).Value;
        pacote.AtualizarCatalogoPublico(categoria, 60, false, DateTime.UtcNow);
        pacote.TornarPublico(DateTime.UtcNow);
        return pacote;
    }

    private void SetupSemPacotesPublicos(Guid treinadorId) =>
        _pacoteRepo.Setup(r => r.ListarPublicosPorTreinadorAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Pacote>());

    [Fact]
    public async Task HandleAsync_TreinadorInexistente_RetornaTenantNotFound()
    {
        var tenantId = Guid.NewGuid();
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var result = await _handler.HandleAsync(new ObterBusinessInfoQuery(tenantId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TreinadorErrors.NaoEncontrado.Code);
    }

    [Fact]
    public async Task HandleAsync_TreinadorInativo_RetornaMesmoErroDoInexistente()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        treinador.Inativar(DateTime.UtcNow);
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new ObterBusinessInfoQuery(treinador.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_PerfilNaoPublicado_RetornaMesmoErroDoInexistente()
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.PerfilPublico.AtualizarDados("Studio Teste", null, null, DateTime.UtcNow);
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new ObterBusinessInfoQuery(treinador.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Should().Be(TreinadorErrors.NaoEncontrado);
    }

    [Fact]
    public async Task HandleAsync_PublicadoSemPacotes_RetornaSucessoComModalitiesVazio()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        SetupSemPacotesPublicos(treinador.Id);

        var result = await _handler.HandleAsync(new ObterBusinessInfoQuery(treinador.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Modalities.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PacotesComCategoriaDuplicadaCaseInsensitive_DeduplicaPreservandoPrimeiraGrafia()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _pacoteRepo.Setup(r => r.ListarPublicosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Pacote>
            {
                CriarPacotePublico(treinador.Id, "Pilates"),
                CriarPacotePublico(treinador.Id, "pilates"),
                CriarPacotePublico(treinador.Id, "Funcional"),
            });

        var result = await _handler.HandleAsync(new ObterBusinessInfoQuery(treinador.Id));

        result.Value.Modalities.Should().BeEquivalentTo(["Pilates", "Funcional"], o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task HandleAsync_SemEnderecoNemPoliticas_OmiteAmbosDoJson()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        SetupSemPacotesPublicos(treinador.Id);

        var result = await _handler.HandleAsync(new ObterBusinessInfoQuery(treinador.Id));

        var json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var documento = JsonDocument.Parse(json);

        documento.RootElement.EnumerateObject().Select(p => p.Name)
            .Should().NotIntersectWith(["address", "openingHours", "policies"]);
    }

    [Fact]
    public async Task HandleAsync_ComEnderecoEPoliticas_MapeiaExplicitamente()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        var endereco = EnderecoPublico.Criar("Rua das Flores", "Centro", "Fortaleza", "CE", "60000000", "123").Value;
        var politicas = new Dictionary<string, string> { ["cancelamento"] = "24h de antecedencia" };
        treinador.PerfilPublico.AtualizarDados("Studio Teste", endereco, politicas, DateTime.UtcNow);
        treinador.PerfilPublico.Publicar(DateTime.UtcNow);
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        SetupSemPacotesPublicos(treinador.Id);

        var result = await _handler.HandleAsync(new ObterBusinessInfoQuery(treinador.Id));

        result.Value.Address!.Street.Should().Be("Rua das Flores");
        result.Value.Address!.PostalCode.Should().Be("60000000");
        result.Value.Policies.Should().ContainKey("cancelamento");
    }

    [Fact]
    public async Task HandleAsync_TreinadorComFusoNaoDefault_EmiteOFusoPersistidoDoTreinador()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        treinador.DefinirFusoHorario("America/Manaus", DateTime.UtcNow);
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        SetupSemPacotesPublicos(treinador.Id);

        var result = await _handler.HandleAsync(new ObterBusinessInfoQuery(treinador.Id));

        result.Value.Timezone.Should().Be("America/Manaus");
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemFusoDefinido_EmiteODefaultDaEntidade()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        SetupSemPacotesPublicos(treinador.Id);

        var result = await _handler.HandleAsync(new ObterBusinessInfoQuery(treinador.Id));

        result.Value.Timezone.Should().Be("America/Sao_Paulo");
    }

    [Fact]
    public async Task HandleAsync_SemEnderecoNemPoliticas_TimezoneContinuaNoJson()
    {
        var treinador = CriarTreinadorAtivoEPublicado();
        treinador.DefinirFusoHorario("America/Manaus", DateTime.UtcNow);
        _treinadorRepo.Setup(r => r.ObterPorIdSemTrackingAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        SetupSemPacotesPublicos(treinador.Id);

        var result = await _handler.HandleAsync(new ObterBusinessInfoQuery(treinador.Id));

        var json = JsonSerializer.Serialize(result.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var documento = JsonDocument.Parse(json);
        documento.RootElement.GetProperty("timezone").GetString().Should().Be("America/Manaus");
    }

    [Fact]
    public void BusinessInfoResponse_NuncaExpoePropriedadeDePlanos()
    {
        typeof(BusinessInfoResponse).GetProperties().Select(p => p.Name)
            .Should().NotContain(n => n.Contains("Plan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
