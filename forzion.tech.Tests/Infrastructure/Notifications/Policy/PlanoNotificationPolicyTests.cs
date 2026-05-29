using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Notifications;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Policy;

public class PlanoNotificationPolicyTests
{
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<ILogger<PlanoNotificationPolicy>> _logger = new();
    private readonly PlanoNotificationPolicy _policy;

    public PlanoNotificationPolicyTests()
    {
        _policy = new PlanoNotificationPolicy(
            _treinadorRepo.Object,
            _planoRepo.Object,
            _vinculoRepo.Object,
            _assinaturaRepo.Object,
            _logger.Object);
    }

    // --- Helpers ---

    private static PlanoPlataforma CriarPlano(TierPlano tier) =>
        PlanoPlataforma.Criar($"Plano {tier}", tier, 10, 99m, TestData.Agora).Value;

    private static Treinador CriarTreinadorComPlano(Guid planoId)
    {
        var treinador = new TreinadorBuilder().Build();
        treinador.AtribuirPlano(planoId, TestData.Agora);
        return treinador;
    }

    private static Treinador CriarTreinadorSemPlano() =>
        new TreinadorBuilder().Build();

    // ─── ResolverPorTreinadorAsync ───────────────────────────────────────────

    [Theory]
    [InlineData(TierPlano.Free, false, false)]
    [InlineData(TierPlano.Basic, false, false)]
    [InlineData(TierPlano.Pro, true, false)]
    [InlineData(TierPlano.ProPlus, true, true)]
    [InlineData(TierPlano.Elite, true, true)]
    public async Task ResolverPorTreinadorAsync_TierVariado_RetornaCanaisCorretos(
        TierPlano tier, bool emailEsperado, bool whatsAppEsperado)
    {
        var plano = CriarPlano(tier);
        var treinador = CriarTreinadorComPlano(plano.Id);

        _treinadorRepo
            .Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _planoRepo
            .Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plano);

        var canais = await _policy.ResolverPorTreinadorAsync(treinador.Id);

        canais.Email.Should().Be(emailEsperado);
        canais.WhatsApp.Should().Be(whatsAppEsperado);
    }

    [Fact]
    public async Task ResolverPorTreinadorAsync_TreinadorSemPlano_RetornaNenhum()
    {
        var treinador = CriarTreinadorSemPlano();

        _treinadorRepo
            .Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);

        var canais = await _policy.ResolverPorTreinadorAsync(treinador.Id);

        canais.Should().Be(CanaisNotificacao.Nenhum);
        _planoRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolverPorTreinadorAsync_TreinadorNaoEncontrado_RetornaNenhum()
    {
        _treinadorRepo
            .Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        var canais = await _policy.ResolverPorTreinadorAsync(Guid.NewGuid());

        canais.Should().Be(CanaisNotificacao.Nenhum);
        _planoRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── ResolverPorAlunoAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ResolverPorAlunoAsync_VinculoAtivo_ResolveViaVinculoTreinadorId()
    {
        var plano = CriarPlano(TierPlano.ProPlus);
        var treinador = CriarTreinadorComPlano(plano.Id);
        var alunoId = Guid.NewGuid();
        var vinculo = new VinculoTreinadorAlunoBuilder()
            .ComTreinadorId(treinador.Id)
            .ComAlunoId(alunoId)
            .Build();

        _vinculoRepo
            .Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        _treinadorRepo
            .Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _planoRepo
            .Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plano);

        var canais = await _policy.ResolverPorAlunoAsync(alunoId);

        canais.Email.Should().BeTrue();
        canais.WhatsApp.Should().BeTrue();
        _assinaturaRepo.Verify(r => r.ObterAtualPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolverPorAlunoAsync_SemVinculoComAssinatura_ResolveViaAssinaturaTreinadorId()
    {
        var plano = CriarPlano(TierPlano.Pro);
        var treinador = CriarTreinadorComPlano(plano.Id);
        var alunoId = Guid.NewGuid();
        var assinatura = new AssinaturaAlunoBuilder()
            .ComTreinadorId(treinador.Id)
            .ComAlunoId(alunoId)
            .Build();

        _vinculoRepo
            .Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);
        _assinaturaRepo
            .Setup(r => r.ObterAtualPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        _treinadorRepo
            .Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        _planoRepo
            .Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plano);

        var canais = await _policy.ResolverPorAlunoAsync(alunoId);

        canais.Email.Should().BeTrue();
        canais.WhatsApp.Should().BeFalse();
    }

    [Fact]
    public async Task ResolverPorAlunoAsync_SemVinculoSemAssinatura_RetornaNenhum()
    {
        var alunoId = Guid.NewGuid();

        _vinculoRepo
            .Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VinculoTreinadorAluno?)null);
        _assinaturaRepo
            .Setup(r => r.ObterAtualPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        var canais = await _policy.ResolverPorAlunoAsync(alunoId);

        canais.Should().Be(CanaisNotificacao.Nenhum);
        _treinadorRepo.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
