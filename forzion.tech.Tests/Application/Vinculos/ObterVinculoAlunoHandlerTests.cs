using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Vinculos.ObterVinculoAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Moq;

namespace forzion.tech.Tests.Application.Vinculos;

public class ObterVinculoAlunoHandlerTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly ObterVinculoAlunoHandler _handler;

    public ObterVinculoAlunoHandlerTests()
    {
        _handler = new ObterVinculoAlunoHandler(_vinculoRepo.Object, _treinadorRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_AlunoSemVinculos_RetornaNulos()
    {
        var alunoId = Guid.NewGuid();
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _vinculoRepo.Setup(r => r.ObterPendentePorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);

        var result = await _handler.HandleAsync(alunoId);

        result.VinculoAtivo.Should().BeNull();
        result.VinculoPendente.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ComVinculoAtivo_RetornaVinculoAtivo()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, alunoId);
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");

        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterPendentePorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(alunoId);

        result.VinculoAtivo.Should().NotBeNull();
        result.VinculoAtivo!.TreinadorId.Should().Be(treinadorId);
        result.VinculoAtivo.NomeTreinador.Should().Be("Carlos");
        result.VinculoPendente.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ComVinculoPendente_RetornaVinculoPendente()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var vinculoPendente = VinculoTreinadorAluno.Criar(treinadorId, alunoId);
        var treinador = Treinador.Criar(Guid.NewGuid(), "Ana");

        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _vinculoRepo.Setup(r => r.ObterPendentePorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoPendente);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(alunoId);

        result.VinculoAtivo.Should().BeNull();
        result.VinculoPendente.Should().NotBeNull();
        result.VinculoPendente!.NomeTreinador.Should().Be("Ana");
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_UsaNomePadrao()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, alunoId);

        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterPendentePorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var result = await _handler.HandleAsync(alunoId);

        result.VinculoAtivo!.NomeTreinador.Should().Be("—");
    }
}
