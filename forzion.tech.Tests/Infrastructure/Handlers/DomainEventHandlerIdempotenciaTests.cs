using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Handlers;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Handlers;

// Guards de idempotência dos handlers duráveis: re-dispatch pelo worker não duplica mutação.
// Nível: unit (Moq). O fluxo completo via OutboxProcessor é coberto pelos testes de retry
// em OutboxProcessorRetryTests; aqui cobrimos o guard interno de cada handler.
public class DomainEventHandlerIdempotenciaTests
{
    // ----- PagamentoTreinadorPagoHandler — Renovação -----

    [Fact]
    public async Task PagamentoTreinadorPago_Renovacao_SegundaExecucao_NaoAvancaProximaCobranca()
    {
        var assinaturaRepo = new Mock<IAssinaturaTreinadorRepository>();
        var planoRepo = new Mock<IPlanoPlataformaRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new PagamentoTreinadorPagoHandler(
            assinaturaRepo.Object, planoRepo.Object, unitOfWork.Object,
            TimeProvider.System, Mock.Of<ILogger<PagamentoTreinadorPagoHandler>>());

        var agora = DateTime.UtcNow;
        var assinatura = AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, agora).Value;
        assinatura.Ativar(agora);

        // OcorridoEm é anterior à criação da assinatura — simula evento do passado.
        var ocorridoEm = agora.AddMinutes(-5);
        var evento = new PagamentoTreinadorPagoEvent(
            Guid.NewGuid(), assinatura.TreinadorId, assinatura.Id,
            FinalidadePagamentoTreinador.Renovacao, null, ocorridoEm);

        assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        await handler.HandleAsync(evento);
        var dataProximaCobrancaAposP1 = assinatura.DataProximaCobranca;
        dataProximaCobrancaAposP1.Should().BeAfter(agora.AddDays(20), "primeiro dispatch avança a data ~1 mês");

        // Segunda execução com o mesmo evento: guard deve barrar (DataProximaCobranca > OcorridoEm).
        await handler.HandleAsync(evento);
        assinatura.DataProximaCobranca.Should().Be(dataProximaCobrancaAposP1,
            "re-dispatch não deve avançar ProximaCobranca novamente");

        // Commit só na primeira execução.
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- PagamentoTreinadorPagoHandler — TrocaPlano -----

    [Fact]
    public async Task PagamentoTreinadorPago_TrocaPlano_SegundaExecucao_NaoReavancaPlano()
    {
        var assinaturaRepo = new Mock<IAssinaturaTreinadorRepository>();
        var planoRepo = new Mock<IPlanoPlataformaRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new PagamentoTreinadorPagoHandler(
            assinaturaRepo.Object, planoRepo.Object, unitOfWork.Object,
            TimeProvider.System, Mock.Of<ILogger<PagamentoTreinadorPagoHandler>>());

        var agora = DateTime.UtcNow;
        var planoAlvo = PlanoPlataforma.Criar("Pro", TierPlano.Pro, 100, 100m, agora).Value;
        var assinatura = AssinaturaTreinador.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, agora).Value;
        assinatura.Ativar(agora);

        var evento = new PagamentoTreinadorPagoEvent(
            Guid.NewGuid(), assinatura.TreinadorId, assinatura.Id,
            FinalidadePagamentoTreinador.TrocaPlano, planoAlvo.Id, agora);

        assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);
        planoRepo.Setup(r => r.ObterPorIdAsync(planoAlvo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(planoAlvo);

        await handler.HandleAsync(evento);
        assinatura.PlanoPlataformaId.Should().Be(planoAlvo.Id, "primeira execução aplica a troca");

        // Segunda execução: guard detecta PlanoPlataformaId == PlanoAlvoId → skip.
        await handler.HandleAsync(evento);

        // PlanoId inalterado, repo de plano consultado somente na 1ª execução.
        assinatura.PlanoPlataformaId.Should().Be(planoAlvo.Id);
        planoRepo.Verify(r => r.ObterPorIdAsync(planoAlvo.Id, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- AlunoRegistradoSincronizarAssinanteHandler -----

    [Fact]
    public async Task AlunoRegistrado_SegundaExecucao_NaoCriaSegundoAssinante()
    {
        var assinanteRepo = new Mock<IAssinanteRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new AlunoRegistradoSincronizarAssinanteHandler(
            assinanteRepo.Object, unitOfWork.Object,
            Mock.Of<ILogger<AlunoRegistradoSincronizarAssinanteHandler>>());

        var agora = DateTime.UtcNow;
        var alunoId = Guid.NewGuid();
        var evento = new AlunoRegistradoEvent(alunoId, Guid.NewGuid(), "Aluno", "a@b.com", agora);

        var criados = new List<Assinante>();
        assinanteRepo.Setup(r => r.AdicionarAsync(It.IsAny<Assinante>(), It.IsAny<CancellationToken>()))
            .Callback<Assinante, CancellationToken>((a, _) => criados.Add(a))
            .Returns(Task.CompletedTask);

        assinanteRepo.Setup(r => r.ObterPorAlunoIdAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Assinante?)null);
        await handler.HandleAsync(evento);
        criados.Should().HaveCount(1, "primeiro dispatch cria o assinante");

        assinanteRepo.Setup(r => r.ObterPorAlunoIdAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(criados[0]);
        await handler.HandleAsync(evento);

        criados.Should().HaveCount(1, "re-dispatch é no-op idempotente");
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- VinculoAprovadoCriarAssinaturaAlunoHandler -----

    [Fact]
    public async Task VinculoAprovado_SegundaExecucao_NaoCriaSegundaAssinaturaAluno()
    {
        var vinculoRepo = new Mock<IVinculoTreinadorAlunoRepository>();
        var assinaturaAlunoRepo = new Mock<IAssinaturaAlunoRepository>();
        var contaRecebimentoRepo = new Mock<IContaRecebimentoRepository>();
        var treinadorRepo = new Mock<ITreinadorRepository>();
        var pacoteRepo = new Mock<IPacoteRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var criarService = new CriarAssinaturaAlunoService(
            pacoteRepo.Object, assinaturaAlunoRepo.Object,
            Mock.Of<ILogger<CriarAssinaturaAlunoService>>());

        var handler = new VinculoAprovadoCriarAssinaturaAlunoHandler(
            vinculoRepo.Object, assinaturaAlunoRepo.Object, contaRecebimentoRepo.Object,
            treinadorRepo.Object, criarService, unitOfWork.Object,
            Mock.Of<ILogger<VinculoAprovadoCriarAssinaturaAlunoHandler>>());

        var agora = DateTime.UtcNow;
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var pacoteId = Guid.NewGuid();

        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, alunoId, agora).Value;
        vinculo.Aprovar(treinadorId, pacoteId, agora);

        var pacote = Pacote.Criar(treinadorId, "Mensal", 150m, agora).Value;
        var conta = ContaRecebimento.Criar(treinadorId, agora).Value;
        conta.ConfigurarStripeConnect("acct_test", agora);
        conta.ConfirmarOnboarding(agora);

        var evento = new VinculoAprovadoEvent(vinculo.Id, treinadorId, alunoId, pacoteId, agora);

        vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vinculo);
        treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Treinador.Criar(Guid.NewGuid(), "T", agora, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value);
        contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conta);
        pacoteRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pacote);

        var assinaturasCriadas = new List<AssinaturaAluno>();
        assinaturaAlunoRepo
            .Setup(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()))
            .Callback<AssinaturaAluno, CancellationToken>((a, _) => assinaturasCriadas.Add(a))
            .Returns(Task.CompletedTask);

        assinaturaAlunoRepo.Setup(r => r.ObterPorVinculoIdAsync(vinculo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);
        await handler.HandleAsync(evento);
        assinaturasCriadas.Should().HaveCount(1, "primeira execução cria a assinatura");

        // Segunda execução: simula persistência — ObterPorVinculoIdAsync agora retorna a assinatura.
        var assinaturaExistente = assinaturasCriadas[0];
        assinaturaAlunoRepo.Setup(r => r.ObterPorVinculoIdAsync(vinculo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinaturaExistente);

        await handler.HandleAsync(evento);

        assinaturasCriadas.Should().HaveCount(1, "re-dispatch não deve criar segunda assinatura");
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
