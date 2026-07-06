using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.ProcessarLimiteAlunos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class ProcessarLimiteAlunosHandlerTests
{
    private static readonly DateTimeOffset Instante = new(2026, 7, 5, 3, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPlanoEfetivoResolver> _planoEfetivoResolver = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<INotificacaoRepository> _notificacaoRepo = new();
    private readonly Mock<ILimiteAlunosEmailSender> _emailSender = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDatabaseErrorInspector> _dbErrorInspector = new();
    private readonly FakeTimeProvider _time = new(Instante);
    private readonly ProcessarLimiteAlunosHandler _handler;

    public ProcessarLimiteAlunosHandlerTests()
    {
        _notificacaoRepo
            .Setup(r => r.AdicionarAsync(It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _dbErrorInspector
            .Setup(i => i.EhConflitoDeConcorrenciaOtimista(It.IsAny<Exception>()))
            .Returns(false);

        var scopeFactory = new ServiceCollection()
            .AddSingleton(_treinadorRepo.Object)
            .AddSingleton(_planoEfetivoResolver.Object)
            .AddSingleton(_vinculoRepo.Object)
            .AddSingleton(_notificacaoRepo.Object)
            .AddSingleton(_emailSender.Object)
            .AddSingleton(_unitOfWork.Object)
            .AddSingleton(_dbErrorInspector.Object)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        _handler = new ProcessarLimiteAlunosHandler(
            _treinadorRepo.Object, scopeFactory, _time, Mock.Of<ILogger<ProcessarLimiteAlunosHandler>>());
    }

    private Treinador SetupUmTreinador()
    {
        var treinador = new TreinadorBuilder().Build();
        _treinadorRepo
            .Setup(r => r.ListarAtivosKeysetAsync(null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([treinador]);
        _treinadorRepo
            .Setup(r => r.ListarAtivosKeysetAsync(treinador.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _treinadorRepo
            .Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinador);
        return treinador;
    }

    private void SetupPlanoEAtivos(int maxAlunos, int ativos)
    {
        _planoEfetivoResolver
            .Setup(r => r.ResolverAsync(It.IsAny<Treinador>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanoEfetivo(Guid.NewGuid(), TierPlano.Basic, maxAlunos, false));
        _vinculoRepo
            .Setup(r => r.ContarAtivosPorTreinadorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ativos);
    }

    // Setup "vivo": Contar/Listar derivam do estado REAL da lista (Status mutável via Inativar),
    // reproduzindo o comportamento de um DB real — necessário pros testes de idempotência/apara.
    private List<VinculoTreinadorAluno> SetupVinculosVivos(Treinador treinador, int maxAlunos, params VinculoTreinadorAluno[] vinculos)
    {
        var lista = vinculos.ToList();
        _planoEfetivoResolver
            .Setup(r => r.ResolverAsync(treinador, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanoEfetivo(Guid.NewGuid(), TierPlano.Basic, maxAlunos, false));
        _vinculoRepo
            .Setup(r => r.ContarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => lista.Count(v => v.Status == VinculoStatus.Ativo));
        _vinculoRepo
            .Setup(r => r.ListarAtivosPorTreinadorOrdenadoAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => lista.Where(v => v.Status == VinculoStatus.Ativo).ToList());
        return lista;
    }

    private static VinculoTreinadorAluno VinculoAtivoEm(Guid treinadorId, DateTime createdAt, bool preservar = false)
    {
        var vinculo = new VinculoTreinadorAlunoBuilder().ComTreinadorId(treinadorId).Em(createdAt).Build();
        vinculo.Aprovar(treinadorId, Guid.NewGuid(), createdAt);
        if (preservar) vinculo.DefinirPreservacao(true, createdAt);
        return vinculo;
    }

    [Fact]
    public async Task HandleAsync_Regularizado_LimpaCarimboENaoNotifica()
    {
        var treinador = SetupUmTreinador();
        treinador.MarcarAcimaDoCap(_time.GetUtcNow().UtcDateTime.AddDays(-10));
        SetupPlanoEAtivos(maxAlunos: 10, ativos: 2);

        var resultado = await _handler.HandleAsync();

        treinador.AlunosAcimaDoCapDesde.Should().BeNull();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notificacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()), Times.Never);
        resultado.Should().Be(new ProcessarLimiteAlunosResultado(0, 0, 0));
    }

    [Fact]
    public async Task HandleAsync_SemCarimboESemExcedente_NaoFazNada()
    {
        SetupUmTreinador();
        SetupPlanoEAtivos(maxAlunos: 10, ativos: 2);

        var resultado = await _handler.HandleAsync();

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        resultado.Should().Be(new ProcessarLimiteAlunosResultado(0, 0, 0));
    }

    [Fact]
    public async Task HandleAsync_NovoExcedente_CarimbaEEnviaInicio()
    {
        var treinador = SetupUmTreinador();
        SetupPlanoEAtivos(maxAlunos: 3, ativos: 5);

        var resultado = await _handler.HandleAsync();

        treinador.AlunosAcimaDoCapDesde.Should().Be(_time.GetUtcNow().UtcDateTime);
        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n => n.Tipo == TipoNotificacao.LimiteAlunosExcedido && n.Corpo.Contains('2')),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailSender.Verify(e => e.EnviarInicioAsync(
            treinador.ContaId, treinador.Nome, 2, _time.GetUtcNow().UtcDateTime.AddMonths(3), It.IsAny<CancellationToken>()), Times.Once);
        resultado.Should().Be(new ProcessarLimiteAlunosResultado(1, 0, 0));
    }

    [Fact]
    public async Task HandleAsync_NovoExcedente_NotificacaoDeduplicada_NaoEnviaEmail()
    {
        SetupUmTreinador();
        SetupPlanoEAtivos(maxAlunos: 3, ativos: 5);
        _notificacaoRepo
            .Setup(r => r.AdicionarAsync(It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _handler.HandleAsync();

        _emailSender.Verify(e => e.EnviarInicioAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(7)]
    [InlineData(1)]
    public async Task HandleAsync_DiaDeLembrete_EnviaLembreteComExcedenteEDataAtuais(int diasParaLimite)
    {
        var treinador = SetupUmTreinador();
        var agora = _time.GetUtcNow().UtcDateTime;
        var dataLimite = agora.AddDays(diasParaLimite);
        treinador.MarcarAcimaDoCap(dataLimite.AddMonths(-3));
        SetupPlanoEAtivos(maxAlunos: 3, ativos: 5);

        var resultado = await _handler.HandleAsync();

        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n => n.Tipo == TipoNotificacao.LimiteAlunosLembrete),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailSender.Verify(e => e.EnviarLembreteAsync(treinador.ContaId, treinador.Nome, 2, dataLimite, It.IsAny<CancellationToken>()), Times.Once);
        resultado.Should().Be(new ProcessarLimiteAlunosResultado(0, 1, 0));
    }

    [Fact]
    public async Task HandleAsync_NaoEDiaDeLembreteNemDeadline_NaoFazNada()
    {
        var treinador = SetupUmTreinador();
        var agora = _time.GetUtcNow().UtcDateTime;
        treinador.MarcarAcimaDoCap(agora.AddDays(15).AddMonths(-3));
        SetupPlanoEAtivos(maxAlunos: 3, ativos: 5);

        var resultado = await _handler.HandleAsync();

        _notificacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        resultado.Should().Be(new ProcessarLimiteAlunosResultado(0, 0, 0));
    }

    [Fact]
    public async Task HandleAsync_Deadline_PreservadosAbaixoDoCap_MantemPreservadosEOsMaisAntigosNaoPreservados()
    {
        var treinador = SetupUmTreinador();
        var agora = _time.GetUtcNow().UtcDateTime;
        treinador.MarcarAcimaDoCap(agora.AddMonths(-3));

        var v0 = VinculoAtivoEm(treinador.Id, agora.AddDays(-50));
        var v1 = VinculoAtivoEm(treinador.Id, agora.AddDays(-40));
        var v2 = VinculoAtivoEm(treinador.Id, agora.AddDays(-30));
        var v3 = VinculoAtivoEm(treinador.Id, agora.AddDays(-20));
        var v4 = VinculoAtivoEm(treinador.Id, agora.AddDays(-10), preservar: true);
        var lista = SetupVinculosVivos(treinador, maxAlunos: 3, v0, v1, v2, v3, v4);

        var resultado = await _handler.HandleAsync();

        v4.Status.Should().Be(VinculoStatus.Ativo, "preservado explicitamente");
        v0.Status.Should().Be(VinculoStatus.Ativo, "mais antigo não-preservado preenche a vaga restante");
        v1.Status.Should().Be(VinculoStatus.Ativo, "segundo mais antigo não-preservado preenche a vaga restante");
        v2.Status.Should().Be(VinculoStatus.Inativo, "não-preservado mais recente entre os cortados");
        v3.Status.Should().Be(VinculoStatus.Inativo, "não-preservado mais recente entre os cortados");
        lista.Count(v => v.Status == VinculoStatus.Ativo).Should().Be(3);
        treinador.AlunosAcimaDoCapDesde.Should().BeNull();
        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n => n.Tipo == TipoNotificacao.LimiteAlunosAplicado && n.Corpo.Contains('2')),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailSender.Verify(e => e.EnviarAplicadoAsync(treinador.ContaId, treinador.Nome, 2, It.IsAny<CancellationToken>()), Times.Once);
        resultado.Should().Be(new ProcessarLimiteAlunosResultado(0, 0, 1));
    }

    [Fact]
    public async Task HandleAsync_Deadline_PreservadosAcimaDoCap_CortaOsMaisRecentesDosPreservadosETodosNaoPreservados()
    {
        var treinador = SetupUmTreinador();
        var agora = _time.GetUtcNow().UtcDateTime;
        treinador.MarcarAcimaDoCap(agora.AddMonths(-3));

        var v0 = VinculoAtivoEm(treinador.Id, agora.AddDays(-50), preservar: true);
        var v1 = VinculoAtivoEm(treinador.Id, agora.AddDays(-40), preservar: true);
        var v2 = VinculoAtivoEm(treinador.Id, agora.AddDays(-30), preservar: true);
        var v3 = VinculoAtivoEm(treinador.Id, agora.AddDays(-20), preservar: true);
        var v4 = VinculoAtivoEm(treinador.Id, agora.AddDays(-10));
        var lista = SetupVinculosVivos(treinador, maxAlunos: 2, v0, v1, v2, v3, v4);

        var resultado = await _handler.HandleAsync();

        v0.Status.Should().Be(VinculoStatus.Ativo, "mais antigo entre os preservados");
        v1.Status.Should().Be(VinculoStatus.Ativo, "segundo mais antigo entre os preservados");
        v2.Status.Should().Be(VinculoStatus.Inativo, "preservado mais recente, cortado por excesso de preservados");
        v3.Status.Should().Be(VinculoStatus.Inativo, "preservado mais recente, cortado por excesso de preservados");
        v4.Status.Should().Be(VinculoStatus.Inativo, "não-preservado cortado quando preservados já excedem o cap");
        lista.Count(v => v.Status == VinculoStatus.Ativo).Should().Be(2);
        resultado.Should().Be(new ProcessarLimiteAlunosResultado(0, 0, 1));
    }

    [Fact]
    public async Task HandleAsync_Deadline_RegularizadoNoRecomputoAoVivo_CancelaApara()
    {
        var treinador = SetupUmTreinador();
        var agora = _time.GetUtcNow().UtcDateTime;
        treinador.MarcarAcimaDoCap(agora.AddMonths(-3));

        var v0 = VinculoAtivoEm(treinador.Id, agora.AddDays(-50));
        var v1 = VinculoAtivoEm(treinador.Id, agora.AddDays(-40));
        var lista = SetupVinculosVivos(treinador, maxAlunos: 5, v0, v1);
        // ativos(2) <= cap(5): excedente vivo é 0 mesmo com carimbo vencido — simula uma
        // regularização (upgrade/pagamento) que aconteceu entre a leitura inicial e a apara.

        var resultado = await _handler.HandleAsync();

        v0.Status.Should().Be(VinculoStatus.Ativo);
        v1.Status.Should().Be(VinculoStatus.Ativo);
        treinador.AlunosAcimaDoCapDesde.Should().BeNull();
        _vinculoRepo.Verify(r => r.ListarAtivosPorTreinadorOrdenadoAsync(treinador.Id, It.IsAny<CancellationToken>()), Times.Never);
        _notificacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()), Times.Never);
        resultado.Should().Be(new ProcessarLimiteAlunosResultado(0, 0, 0));
    }

    [Fact]
    public async Task HandleAsync_Deadline_RecomputoAoVivoDivergeDaLeituraInicial_CancelaApara()
    {
        var treinador = SetupUmTreinador();
        var agora = _time.GetUtcNow().UtcDateTime;
        treinador.MarcarAcimaDoCap(agora.AddMonths(-3));

        _vinculoRepo
            .Setup(r => r.ContarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _planoEfetivoResolver
            .SetupSequence(r => r.ResolverAsync(treinador, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanoEfetivo(Guid.NewGuid(), TierPlano.Basic, 3, false))
            .ReturnsAsync(new PlanoEfetivo(Guid.NewGuid(), TierPlano.ProPlus, 10, false));

        var resultado = await _handler.HandleAsync();

        treinador.AlunosAcimaDoCapDesde.Should().BeNull();
        _vinculoRepo.Verify(r => r.ListarAtivosPorTreinadorOrdenadoAsync(treinador.Id, It.IsAny<CancellationToken>()), Times.Never);
        _notificacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Notificacao>(), It.IsAny<CancellationToken>()), Times.Never);
        resultado.Should().Be(new ProcessarLimiteAlunosResultado(0, 0, 0));
    }

    [Fact]
    public async Task HandleAsync_NovoExcedente_TreinadorFree_EnviaInicioMesmoAssim()
    {
        var treinador = SetupUmTreinador();
        _planoEfetivoResolver
            .Setup(r => r.ResolverAsync(treinador, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanoEfetivo(Guid.NewGuid(), TierPlano.Free, 3, true));
        _vinculoRepo
            .Setup(r => r.ContarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        await _handler.HandleAsync();

        _emailSender.Verify(e => e.EnviarInicioAsync(
            treinador.ContaId, treinador.Nome, 2, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_RodandoDuasVezesSeguidas_NaoAparaNemNotificaDeNovo()
    {
        var treinador = SetupUmTreinador();
        var agora = _time.GetUtcNow().UtcDateTime;
        treinador.MarcarAcimaDoCap(agora.AddMonths(-3));

        var v0 = VinculoAtivoEm(treinador.Id, agora.AddDays(-50));
        var v1 = VinculoAtivoEm(treinador.Id, agora.AddDays(-40));
        var v2 = VinculoAtivoEm(treinador.Id, agora.AddDays(-30));
        SetupVinculosVivos(treinador, maxAlunos: 1, v0, v1, v2);

        var primeira = await _handler.HandleAsync();
        var segunda = await _handler.HandleAsync();

        primeira.Aparados.Should().Be(1);
        segunda.Should().Be(new ProcessarLimiteAlunosResultado(0, 0, 0));
        _notificacaoRepo.Verify(r => r.AdicionarAsync(
            It.Is<Notificacao>(n => n.Tipo == TipoNotificacao.LimiteAlunosAplicado),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailSender.Verify(e => e.EnviarAplicadoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_RetornaVazioSemLancar()
    {
        var fantasma = new TreinadorBuilder().Build();
        _treinadorRepo
            .Setup(r => r.ListarAtivosKeysetAsync(null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([fantasma]);
        _treinadorRepo
            .Setup(r => r.ListarAtivosKeysetAsync(fantasma.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _treinadorRepo
            .Setup(r => r.ObterPorIdAsync(fantasma.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treinador?)null);

        var resultado = await _handler.HandleAsync();

        resultado.Should().Be(new ProcessarLimiteAlunosResultado(0, 0, 0));
    }

    [Fact]
    public async Task HandleAsync_ConflitoDeConcorrencia_DescartaESeguePraProximo()
    {
        var treinador = SetupUmTreinador();
        SetupPlanoEAtivos(maxAlunos: 3, ativos: 5);
        _dbErrorInspector
            .Setup(i => i.EhConflitoDeConcorrenciaOtimista(It.IsAny<Exception>()))
            .Returns(true);
        _unitOfWork
            .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyExceptionStub());

        var resultado = await _handler.HandleAsync();

        _unitOfWork.Verify(u => u.DescartarAlteracoesPendentes(), Times.Once);
        _emailSender.Verify(e => e.EnviarInicioAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        resultado.Should().Be(new ProcessarLimiteAlunosResultado(0, 0, 0));
    }

    private sealed class DbUpdateConcurrencyExceptionStub : Exception;
}
