// F30 (Fase 4 test remediation) — Cross-aggregate App-level integration.
//
// Bridge entre handler unit tests (puro mock) e E2E (Testcontainers + Docker).
// Aqui orquestramos os DOIS handlers reais (AprovarVinculo + downstream
// VinculoAprovadoCriarAssinaturaAluno) compartilhando repos in-memory. O
// UnitOfWork real dispara eventos no commit; mockamos isso simulando a
// dispatch manual do evento apos AprovarVinculo retornar.
//
// O que ESTE teste pega que o unit test NAO pega:
//   - Sequenciamento correto: AprovarVinculo precede a criacao da assinatura.
//   - Shape do evento real (VinculoAprovadoEvent) gerado pelo aggregate
//     bate com o que o downstream handler le.
//   - Estado final cross-aggregate: vinculo.Status=Ativo + assinatura criada
//     com pacoteId/treinadorId/alunoId vindos do mesmo flow.

using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Handlers;
using Microsoft.Extensions.Logging;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Integration;

public class VinculoApprovalCrossAggregateTests
{
    // Repos compartilhados entre os dois handlers — espelha o que IoC faria em prod.
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ILimiteTreinadorService> _limiteService = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDbContextTransactionProvider> _transactionProvider = new();

    private readonly AprovarVinculoHandler _aprovarHandler;
    private readonly VinculoAprovadoCriarAssinaturaAlunoHandler _criarAssinaturaHandler;

    // Captura assinaturas adicionadas pra assertions cross-aggregate.
    private readonly List<AssinaturaAluno> _assinaturasCriadas = [];

    public VinculoApprovalCrossAggregateTests()
    {
        // tx noop pra AprovarVinculo
        var mockTx = new Mock<ITransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _transactionProvider
            .Setup(p => p.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTx.Object);

        _assinaturaRepo
            .Setup(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()))
            .Callback<AssinaturaAluno, CancellationToken>((a, _) => _assinaturasCriadas.Add(a))
            .Returns(Task.CompletedTask);

        var treinadorPlataforma = Treinador.Criar(Guid.NewGuid(), "Treinador", DateTime.UtcNow, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(treinadorPlataforma);

        _aprovarHandler = new AprovarVinculoHandler(
            _vinculoRepo.Object, _treinoAlunoRepo.Object, _treinoRepo.Object,
            _alunoRepo.Object, _treinadorRepo.Object, _contaRecebimentoRepo.Object, _limiteService.Object, _logRepo.Object,
            _unitOfWork.Object, _transactionProvider.Object,
            TimeProvider.System,
            Mock.Of<ILogger<AprovarVinculoHandler>>());

        _assinaturaRepo
            .Setup(r => r.ObterPorVinculoIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((forzion.tech.Domain.Entities.AssinaturaAluno?)null);

        _criarAssinaturaHandler = new VinculoAprovadoCriarAssinaturaAlunoHandler(
            _vinculoRepo.Object, _assinaturaRepo.Object, _contaRecebimentoRepo.Object, _treinadorRepo.Object,
            new forzion.tech.Application.Services.CriarAssinaturaAlunoService(
                _pacoteRepo.Object, _assinaturaRepo.Object,
                Mock.Of<ILogger<forzion.tech.Application.Services.CriarAssinaturaAlunoService>>()),
            _unitOfWork.Object,
            Mock.Of<ILogger<VinculoAprovadoCriarAssinaturaAlunoHandler>>());
    }

    private static ContaRecebimento ContaOnboarded(Guid treinadorId)
    {
        var conta = ContaRecebimento.Criar(treinadorId, DateTime.UtcNow).Value;
        conta.ConfigurarStripeConnect("acct_test", TestData.Agora);
        conta.ConfirmarOnboarding(TestData.Agora);
        return conta;
    }

    [Fact]
    public async Task AprovarVinculo_ComOnboardingCompleto_DisparaEvento_CriaAssinaturaAlunoComShapeCorreto()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var pacoteId = Guid.NewGuid();
        var precoEsperado = 199.90m;

        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, alunoId, DateTime.UtcNow).Value;
        var pacote = Pacote.Criar(treinadorId, "Premium", precoEsperado, DateTime.UtcNow).Value;
        // Forca o pacoteId esperado via reflection (factory gera Guid.NewGuid).
        // Em vez disso: usar pacote.Id como referencia e setar vinculo.PacoteId apos Aprovar.
        var conta = ContaOnboarded(treinadorId);

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Pacote: o vinculo aprovado guarda o pacoteId; ObterPorId no downstream
        // deve devolver um pacote que de match nesse Id. Stubbamos com pacote real.
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        // ETAPA 1 — AprovarVinculo (Application).
        var result = await _aprovarHandler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, pacote.Id));
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(VinculoStatus.Ativo);

        // O evento foi enfileirado no aggregate. Simula UnitOfWork dispatchando
        // (em prod, esse passo acontece dentro do CommitAsync do UoW real).
        var evento = vinculo.DomainEvents.OfType<VinculoAprovadoEvent>().Single();
        evento.TreinadorId.Should().Be(treinadorId);
        evento.AlunoId.Should().Be(alunoId);
        evento.VinculoId.Should().Be(vinculo.Id);

        // ETAPA 2 — Downstream handler (Infrastructure).
        await _criarAssinaturaHandler.HandleAsync(evento);

        // INVARIANTE CROSS-AGGREGATE: assinatura criada com referencias coerentes.
        _assinaturasCriadas.Should().HaveCount(1, "exatamente uma assinatura por vinculo aprovado");
        var assinatura = _assinaturasCriadas[0];
        assinatura.VinculoId.Should().Be(vinculo.Id);
        assinatura.TreinadorId.Should().Be(treinadorId);
        assinatura.AlunoId.Should().Be(alunoId);
        assinatura.PacoteId.Should().Be(pacote.Id);
        assinatura.Valor.Should().Be(precoEsperado);
    }

    [Fact]
    public async Task AprovarVinculo_SemOnboardingStripe_BloqueiaAprovacao()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, alunoId, DateTime.UtcNow).Value;
        var pacote = Pacote.Criar(treinadorId, "Basic", 99m, DateTime.UtcNow).Value;

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Sem conta de recebimento (onboarding nao iniciado).
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync((ContaRecebimento?)null);

        var result = await _aprovarHandler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, pacote.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador_sem_onboarding");
        vinculo.DomainEvents.OfType<VinculoAprovadoEvent>().Should().BeEmpty();
        _assinaturasCriadas.Should().BeEmpty("assinatura nao deve ser criada sem onboarding completo");
    }

    [Fact]
    public async Task AprovarVinculo_ModoExterno_AceitaSemOnboarding_NaoGeraBillingNemNotificacao()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, alunoId, DateTime.UtcNow).Value;
        var pacote = Pacote.Criar(treinadorId, "Basic", 99m, DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Treinador.Criar(Guid.NewGuid(), "Externo", DateTime.UtcNow, modoPagamentoAluno: ModoPagamentoAluno.Externo).Value);
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync((ContaRecebimento?)null);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var result = await _aprovarHandler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, pacote.Id));
        result.IsSuccess.Should().BeTrue("modo Externo dispensa onboarding Stripe");

        var evento = vinculo.DomainEvents.OfType<VinculoAprovadoEvent>().Single();
        await _criarAssinaturaHandler.HandleAsync(evento);

        _assinaturasCriadas.Should().BeEmpty("sem AssinaturaAluno não há AssinaturaAlunoCriadaEvent/Pagamento — nenhuma notificação de pagamento dispara");
    }

    [Fact]
    public async Task AprovarVinculo_PacoteSumido_DownstreamHandler_NaoCriaAssinatura()
    {
        // Race rara: vinculo aprovado, pacote deletado entre dispatch e handle.
        // Handler deve degradar gracioso (log + return), nao throw.
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, alunoId, DateTime.UtcNow).Value;
        var pacote = Pacote.Criar(treinadorId, "Pro", 299m, DateTime.UtcNow).Value;
        var conta = ContaOnboarded(treinadorId);

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        // Pacote SUMIU entre AprovarVinculo e o downstream handle.
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Pacote?)null);

        await _aprovarHandler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, pacote.Id));
        var evento = vinculo.DomainEvents.OfType<VinculoAprovadoEvent>().Single();

        var act = async () => await _criarAssinaturaHandler.HandleAsync(evento);
        await act.Should().NotThrowAsync("downstream handler deve tolerar pacote ausente sem propagar exception");
        _assinaturasCriadas.Should().BeEmpty();
    }
}
