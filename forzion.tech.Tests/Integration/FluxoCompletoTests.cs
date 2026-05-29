using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Services;
using forzion.tech.Application.UseCases.Alunos.RegistrarAluno;
using forzion.tech.Application.UseCases.Treinadores.AprovarTreinador;
using forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;
using forzion.tech.Application.UseCases.Treinos.RegistrarExecucao;
using forzion.tech.Application.UseCases.Treinos.VincularFichaAoAluno;
using forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Integration;

public class FluxoCompletoTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();

    public FluxoCompletoTests()
    {
        _passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hash_bcrypt");
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((global::forzion.tech.Domain.Entities.Conta?)null);
    }

    // --- Etapa 1: Registrar Treinador ---

    [Fact]
    public async Task RegistrarTreinador_DadosValidos_CriaTreinadorAguardandoAprovacao()
    {
        var handler = new RegistrarTreinadorHandler(
            _contaRepo.Object, _treinadorRepo.Object, _passwordHasher.Object,
            _unitOfWork.Object, new RegistrarTreinadorCommandValidator(), TimeProvider.System,
            Mock.Of<ILogger<RegistrarTreinadorHandler>>());

        var result = await handler.HandleAsync(
            new RegistrarTreinadorCommand("treinador@teste.com", "Senha123", "Carlos"));

        result.Status.Should().Be(TreinadorStatus.AguardandoAprovacao);
        _treinadorRepo.Verify(r => r.AdicionarAsync(It.IsAny<Treinador>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Etapa 2: Aprovar Treinador ---

    [Fact]
    public async Task AprovarTreinador_TreinadorPendente_AlteraStatusParaAtivo()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var handler = new AprovarTreinadorHandler(
            _treinadorRepo.Object, _logRepo.Object, _unitOfWork.Object, TimeProvider.System,
            Mock.Of<ILogger<AprovarTreinadorHandler>>());

        var result = await handler.HandleAsync(new AprovarTreinadorCommand(treinador.Id, Guid.NewGuid()));

        result.Value.Status.Should().Be(TreinadorStatus.Ativo);
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Etapa 3: Registrar Aluno ---

    [Fact]
    public async Task RegistrarAluno_TreinadorAtivo_CriaAlunoComVinculoPendente()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        var pacote = Pacote.Criar(treinador.Id, "Pacote Basico", 0, DateTime.UtcNow).Value;

        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var handler = new RegistrarAlunoHandler(
            _contaRepo.Object, _alunoRepo.Object, _vinculoRepo.Object, _treinadorRepo.Object,
            _pacoteRepo.Object, _passwordHasher.Object, _unitOfWork.Object,
            new RegistrarAlunoCommandValidator(), Mock.Of<IWhatsAppNotifier>(), TimeProvider.System,
            Mock.Of<ILogger<RegistrarAlunoHandler>>());

        var result = await handler.HandleAsync(
            new RegistrarAlunoCommand("aluno@teste.com", "Senha123", "Joao", treinador.Id, pacote.Id));

        result.Value.Status.Should().Be(AlunoStatus.AguardandoAprovacao);
        _vinculoRepo.Verify(r => r.AdicionarAsync(It.IsAny<VinculoTreinadorAluno>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Etapa 4: Aprovar Vínculo com verificação de limite ---

    [Fact]
    public async Task AprovarVinculo_DentroDoLimite_AprovaSemExcecao()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var plano = PlanoPlataforma.Criar("Starter", forzion.tech.Domain.Enums.TierPlano.Basic, 10, 0, DateTime.UtcNow).Value;
        treinador.AtribuirPlano(plano.Id, DateTime.UtcNow);
        var aluno = Aluno.Criar(Guid.NewGuid(), "Joao", DateTime.UtcNow).Value;
        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
        var pacote = Pacote.Criar(treinador.Id, "Pacote", 0, DateTime.UtcNow).Value;

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _vinculoRepo.Setup(r => r.ContarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Aluno?)null);

        var mockTx = new Mock<ITransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var mockTxProvider = new Mock<IDbContextTransactionProvider>();
        mockTxProvider.Setup(p => p.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTx.Object);

        var limiteService = new LimiteTreinadorService(_treinadorRepo.Object, _planoRepo.Object, _vinculoRepo.Object);

        var handler = new AprovarVinculoHandler(
            _vinculoRepo.Object, _treinoAlunoRepo.Object, _treinoRepo.Object,
            _alunoRepo.Object, limiteService, _logRepo.Object, _unitOfWork.Object,
            mockTxProvider.Object, Mock.Of<IWhatsAppNotifier>(), TimeProvider.System,
            Mock.Of<ILogger<AprovarVinculoHandler>>());

        var resultado = await handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinador.Id, pacote.Id));

        resultado.Status.Should().Be(VinculoStatus.Ativo);
    }

    // --- Etapa 5: Vincular Ficha ---

    [Fact]
    public async Task VincularFicha_AlunoComVinculoAtivo_VinculaFicha()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var aluno = Aluno.Criar(Guid.NewGuid(), "Joao", DateTime.UtcNow).Value;
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinador.Id, DateTime.UtcNow).Value;
        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;

        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinador.Id, aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorTreinoIdAsync(treino.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TreinoAlunoVinculado>());
        _userContext.Setup(u => u.PerfilId).Returns(treinador.Id);

        var handler = new VincularFichaAoAlunoHandler(
            _treinoRepo.Object, _treinoAlunoRepo.Object, _vinculoRepo.Object,
            _unitOfWork.Object, _userContext.Object, TimeProvider.System,
            Mock.Of<ILogger<VincularFichaAoAlunoHandler>>());

        await handler.HandleAsync(new VincularFichaAoAlunoCommand(treino.Id, aluno.Id));

        _treinoAlunoRepo.Verify(r => r.AdicionarAsync(It.IsAny<TreinoAluno>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Etapa 6: Registrar Execução ---

    [Fact]
    public async Task RegistrarExecucao_FichaVinculada_PersisteDados()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        var aluno = Aluno.Criar(Guid.NewGuid(), "Joao", DateTime.UtcNow).Value;
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinador.Id, DateTime.UtcNow).Value;
        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
        var treinoAluno = TreinoAluno.Criar(treino.Id, aluno.Id, DateTime.UtcNow).Value;

        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _treinoAlunoRepo.Setup(r => r.ObterAsync(treino.Id, aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinoAluno);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinador.Id, aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _userContext.Setup(u => u.PerfilId).Returns(aluno.Id);

        var handler = new RegistrarExecucaoHandler(
            _treinoRepo.Object, _alunoRepo.Object, _treinoAlunoRepo.Object,
            _vinculoRepo.Object, _execucaoRepo.Object, _unitOfWork.Object,
            _userContext.Object, TimeProvider.System, Mock.Of<ILogger<RegistrarExecucaoHandler>>());

        var execucao = await handler.HandleAsync(new RegistrarExecucaoCommand(
            treino.Id, aluno.Id, DateTime.UtcNow, null, []));

        execucao.TreinoId.Should().Be(treino.Id);
        execucao.AlunoId.Should().Be(aluno.Id);
        _execucaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<ExecucaoTreino>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
