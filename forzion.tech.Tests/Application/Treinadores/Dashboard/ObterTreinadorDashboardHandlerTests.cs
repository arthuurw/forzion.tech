using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.Dashboard;
using forzion.tech.Application.UseCases.Treinadores.VerificarOnboarding;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores.Dashboard;

public class ObterTreinadorDashboardHandlerTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<VerificarOnboardingTreinadorHandler> _onboardingHandler =
        new(null!, null!, null!, null!, null!, null!);
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Guid _treinadorId = Guid.NewGuid();
    private readonly ObterTreinadorDashboardHandler _handler;

    public ObterTreinadorDashboardHandlerTests()
    {
        _userContext.SetupGet(u => u.PerfilId).Returns(_treinadorId);

        _vinculoRepo
            .Setup(r => r.ContarPorStatusAsync(_treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<VinculoStatus, int>());
        _vinculoRepo
            .Setup(r => r.SomarReceitaPorPacoteAsync(_treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReceitaPorPacote>());
        _vinculoRepo
            .Setup(r => r.ListarComDetalhesAsync(_treinadorId, VinculoStatus.AguardandoAprovacao, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<VinculoComDetalheAluno>(), 0));
        _treinoRepo
            .Setup(r => r.ContarPorObjetivoAsync(_treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ObjetivoContagem>());
        _assinaturaRepo
            .Setup(r => r.ObterAtualPorTreinadorAsync(_treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((forzion.tech.Domain.Entities.AssinaturaTreinador?)null);
        _onboardingHandler
            .Setup(h => h.HandleAsync(It.IsAny<VerificarOnboardingTreinadorQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new OnboardingStatusResponse(true, true, ModoPagamentoAluno.Plataforma, null)));

        _handler = new ObterTreinadorDashboardHandler(
            _vinculoRepo.Object, _treinoRepo.Object, _assinaturaRepo.Object,
            _treinadorRepo.Object, _planoRepo.Object, _onboardingHandler.Object, _userContext.Object);
    }

    private Treinador SetupTreinador(bool comDados, TierPlano? tierPlano, ModoPagamentoAluno modo)
    {
        Guid? planoId = null;
        if (tierPlano is { } tier)
        {
            var plano = PlanoPlataforma.Criar("Plano", tier, 50, tier == TierPlano.Free ? 0m : 99.90m, DateTime.UtcNow).Value;
            planoId = plano.Id;
            _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);
        }

        var treinador = Treinador.Criar(Guid.NewGuid(), "Treinador", DateTime.UtcNow, null, planoId, modo).Value;
        if (comDados)
        {
            var endereco = EnderecoFiscal.Criar("Rua", "1", "Centro", "3550308", "SP", "01001000", null).Value;
            var dados = DadosFiscais.Criar(TipoDocumentoFiscal.Cpf, "11144477735", "Razao", endereco, null).Value;
            treinador.DefinirDadosFiscais(dados, DateTime.UtcNow);
        }

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(_treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        return treinador;
    }

    [Fact]
    public async Task HandleAsync_MrrSomaTodosOsAtivos_NaoTruncaEm100Vinculos()
    {
        _vinculoRepo
            .Setup(r => r.SomarReceitaPorPacoteAsync(_treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReceitaPorPacote>
            {
                new(Guid.NewGuid(), "Premium", 150, 150 * 99.90m),
                new(Guid.NewGuid(), "Basico", 30, 30 * 49.90m),
            });

        var result = await _handler.HandleAsync();

        result.Mrr.Should().Be((150 * 99.90m) + (30 * 49.90m));
        result.ReceitaPorPacote.Should().ContainSingle(p => p.Nome == "Premium" && p.Alunos == 150);
        result.ReceitaPorPacote.First().Nome.Should().Be("Premium");
    }

    [Fact]
    public async Task HandleAsync_CountsPorStatus_MapeiaCadaStatus()
    {
        _vinculoRepo
            .Setup(r => r.ContarPorStatusAsync(_treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<VinculoStatus, int>
            {
                [VinculoStatus.Ativo] = 150,
                [VinculoStatus.AguardandoAprovacao] = 3,
                [VinculoStatus.Inativo] = 7,
            });

        var result = await _handler.HandleAsync();

        result.Counts.Ativos.Should().Be(150);
        result.Counts.Aguardando.Should().Be(3);
        result.Counts.Inativos.Should().Be(7);
    }

    [Fact]
    public async Task HandleAsync_CountsStatusAusente_RetornaZero()
    {
        _vinculoRepo
            .Setup(r => r.ContarPorStatusAsync(_treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<VinculoStatus, int> { [VinculoStatus.Ativo] = 5 });

        var result = await _handler.HandleAsync();

        result.Counts.Ativos.Should().Be(5);
        result.Counts.Aguardando.Should().Be(0);
        result.Counts.Inativos.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_Objetivos_HistogramaOrdenadoComTotalFichasSomado()
    {
        _treinoRepo
            .Setup(r => r.ContarPorObjetivoAsync(_treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ObjetivoContagem>
            {
                new(ObjetivoTreino.Emagrecimento, 4),
                new(ObjetivoTreino.Hipertrofia, 9),
                new(ObjetivoTreino.Forca, 2),
            });

        var result = await _handler.HandleAsync();

        result.TotalFichas.Should().Be(15);
        result.Objetivos.Should().HaveCount(3);
        result.Objetivos.First().Objetivo.Should().Be(ObjetivoTreino.Hipertrofia);
        result.Objetivos.First().Total.Should().Be(9);
    }

    [Fact]
    public async Task HandleAsync_ScopaPeloPerfilIdDoUsuario()
    {
        await _handler.HandleAsync();

        _vinculoRepo.Verify(r => r.ContarPorStatusAsync(_treinadorId, It.IsAny<CancellationToken>()), Times.Once);
        _vinculoRepo.Verify(r => r.SomarReceitaPorPacoteAsync(_treinadorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ComAssinatura_RefleteStatusDaAssinatura()
    {
        var assinatura = forzion.tech.Domain.Entities.AssinaturaTreinador
            .Criar(_treinadorId, Guid.NewGuid(), 49.90m, DateTime.UtcNow).Value;
        _assinaturaRepo
            .Setup(r => r.ObterAtualPorTreinadorAsync(_treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync();

        result.Plano.Status.Should().Be(AssinaturaTreinadorStatus.Pendente);
    }

    [Fact]
    public async Task HandleAsync_SemAssinatura_PlanoStatusNulo()
    {
        var result = await _handler.HandleAsync();

        result.Plano.Status.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_SemDadosFiscaisEPlanoPago_DadosFiscaisPendentesTrue()
    {
        SetupTreinador(comDados: false, TierPlano.Pro, ModoPagamentoAluno.Externo);

        var result = await _handler.HandleAsync();

        result.DadosFiscaisPendentes.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_SemDadosFiscaisEModoInterno_DadosFiscaisPendentesTrue()
    {
        SetupTreinador(comDados: false, TierPlano.Free, ModoPagamentoAluno.Plataforma);

        var result = await _handler.HandleAsync();

        result.DadosFiscaisPendentes.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_SemDadosFiscaisPlanoGratuitoEModoExterno_DadosFiscaisPendentesFalse()
    {
        SetupTreinador(comDados: false, TierPlano.Free, ModoPagamentoAluno.Externo);

        var result = await _handler.HandleAsync();

        result.DadosFiscaisPendentes.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_ComDadosFiscais_DadosFiscaisPendentesFalse()
    {
        SetupTreinador(comDados: true, TierPlano.Pro, ModoPagamentoAluno.Plataforma);

        var result = await _handler.HandleAsync();

        result.DadosFiscaisPendentes.Should().BeFalse();
    }
}
