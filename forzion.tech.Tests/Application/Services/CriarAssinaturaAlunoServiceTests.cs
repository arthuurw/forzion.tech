using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Services;

public class CriarAssinaturaAlunoServiceTests
{
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly CriarAssinaturaAlunoService _service;

    public CriarAssinaturaAlunoServiceTests()
    {
        _service = new CriarAssinaturaAlunoService(
            _pacoteRepo.Object, _assinaturaRepo.Object, Mock.Of<ILogger<CriarAssinaturaAlunoService>>());
    }

    private VinculoTreinadorAluno VinculoComPacote(Guid treinadorId, Pacote pacote)
    {
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        vinculo.Aprovar(treinadorId, pacote.Id, DateTime.UtcNow);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pacote);
        return vinculo;
    }

    [Fact]
    public async Task CriarParaVinculoAsync_PacoteDeOutroTreinador_NaoCriaAssinatura()
    {
        var treinadorId = Guid.NewGuid();
        var pacoteAlheio = Pacote.Criar(Guid.NewGuid(), "Mensal", 100m, DateTime.UtcNow).Value;
        var vinculo = VinculoComPacote(treinadorId, pacoteAlheio);

        var resultado = await _service.CriarParaVinculoAsync(vinculo, DateTime.UtcNow, suprimirNotificacao: false);

        resultado.Should().Be(ResultadoCriacaoAssinaturaAluno.PacoteIndisponivel);
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CriarParaVinculoAsync_PacoteDoProprioTreinador_CriaAssinatura()
    {
        var treinadorId = Guid.NewGuid();
        var pacote = Pacote.Criar(treinadorId, "Mensal", 150m, DateTime.UtcNow).Value;
        var vinculo = VinculoComPacote(treinadorId, pacote);

        var resultado = await _service.CriarParaVinculoAsync(vinculo, DateTime.UtcNow, suprimirNotificacao: false);

        resultado.Should().Be(ResultadoCriacaoAssinaturaAluno.Criada);
        _assinaturaRepo.Verify(r => r.AdicionarAsync(
                It.Is<AssinaturaAluno>(a => a.TreinadorId == treinadorId && a.Valor == 150m),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
