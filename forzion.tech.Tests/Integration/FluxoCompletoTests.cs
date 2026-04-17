using FluentAssertions;
using FluentValidation;
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
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Integration;

/// <summary>
/// Testa o fluxo completo: cadastro → aprovação → vínculo → ficha → execução.
/// Cada etapa usa os handlers reais com repositórios mockados.
/// </summary>
public class FluxoCompletoTests
{
    // --- Mocks compartilhados ---
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IPlanoTreinadorRepository> _planoRepo = new();
    private readonly Mock<IPacoteAlunoRepository> _pacoteRepo = new();
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
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
    }

    [Fact]
    public async Task FluxoCompleto_CadastroAteExecucao_SucedidoEmCadaEtapa()
    {
        // -------------------------------------------------------
        // 1. Registrar treinador
        // -------------------------------------------------------
        var registrarTreinadorHandler = new RegistrarTreinadorHandler(
            _contaRepo.Object, _treinadorRepo.Object, _passwordHasher.Object,
            _unitOfWork.Object, new RegistrarTreinadorCommandValidator(),
            Mock.Of<ILogger<RegistrarTreinadorHandler>>());

        var treinadorResult = await registrarTreinadorHandler.HandleAsync(
            new RegistrarTreinadorCommand("treinador@teste.com", "senha123", "Carlos"));

        treinadorResult.Status.Should().Be(TreinadorStatus.AguardandoAprovacao);
        _treinadorRepo.Verify(r => r.AdicionarAsync(It.IsAny<Treinador>(), It.IsAny<CancellationToken>()), Times.Once);

        // -------------------------------------------------------
        // 2. Aprovar treinador (admin)
        // -------------------------------------------------------
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos");
        var adminId = Guid.NewGuid();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var aprovarTreinadorHandler = new AprovarTreinadorHandler(
            _treinadorRepo.Object, _logRepo.Object, _unitOfWork.Object,
            Mock.Of<ILogger<AprovarTreinadorHandler>>());

        var treinadorAprovado = await aprovarTreinadorHandler.HandleAsync(
            new AprovarTreinadorCommand(treinador.Id, adminId));

        treinadorAprovado.Status.Should().Be(TreinadorStatus.Ativo);
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // -------------------------------------------------------
        // 3. Registrar aluno (com vínculo pendente ao treinador)
        // -------------------------------------------------------
        _contaRepo.Setup(r => r.ObterPorEmailAsync("aluno@teste.com", It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var registrarAlunoHandler = new RegistrarAlunoHandler(
            _contaRepo.Object, _alunoRepo.Object, _vinculoRepo.Object, _treinadorRepo.Object,
            _passwordHasher.Object, _unitOfWork.Object,
            new RegistrarAlunoCommandValidator(),
            Mock.Of<ILogger<RegistrarAlunoHandler>>());

        var alunoResult = await registrarAlunoHandler.HandleAsync(
            new RegistrarAlunoCommand("aluno@teste.com", "senha123", "João", treinador.Id));

        alunoResult.Status.Should().Be(AlunoStatus.AguardandoAprovacao);
        _vinculoRepo.Verify(r => r.AdicionarAsync(It.IsAny<VinculoTreinadorAluno>(), It.IsAny<CancellationToken>()), Times.Once);

        // -------------------------------------------------------
        // 4. Aprovar vínculo (treinador)
        // -------------------------------------------------------
        var aluno = Aluno.Criar(Guid.NewGuid(), "João");
        var pacote = PacoteAluno.Criar(treinador.Id, "Pacote Básico", 3, 0);
        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id);

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);

        var plano = PlanoTreinador.Criar("Starter", 10, 0);
        treinador.AtribuirPlano(plano.Id);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);
        _vinculoRepo.Setup(r => r.ContarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var limiteService = new LimiteTreinadorService(_treinadorRepo.Object, _planoRepo.Object, _vinculoRepo.Object);

        var aprovarVinculoHandler = new AprovarVinculoHandler(
            _vinculoRepo.Object, limiteService, _logRepo.Object, _unitOfWork.Object,
            Mock.Of<ILogger<AprovarVinculoHandler>>());

        var vinculoAprovado = await aprovarVinculoHandler.HandleAsync(
            new AprovarVinculoCommand(vinculo.Id, treinador.Id, pacote.Id));

        vinculoAprovado.Status.Should().Be(VinculoStatus.Ativo);

        // -------------------------------------------------------
        // 5. Vincular ficha ao aluno (treinador)
        // -------------------------------------------------------
        var treino = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinador.Id);

        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _vinculoRepo.Setup(r => r.ObterAtivoAsync(treinador.Id, aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _treinoAlunoRepo.Setup(r => r.ContarAtivosPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _userContext.Setup(u => u.PerfilId).Returns(treinador.Id);

        var limiteFichasService = new LimiteFichasService(_vinculoRepo.Object, _pacoteRepo.Object, _treinoAlunoRepo.Object);

        var vincularFichaHandler = new VincularFichaAoAlunoHandler(
            _treinoRepo.Object, _treinoAlunoRepo.Object, _vinculoRepo.Object,
            limiteFichasService, _unitOfWork.Object, _userContext.Object,
            Mock.Of<ILogger<VincularFichaAoAlunoHandler>>());

        await vincularFichaHandler.HandleAsync(new VincularFichaAoAlunoCommand(treino.Id, aluno.Id));
        _treinoAlunoRepo.Verify(r => r.AdicionarAsync(It.IsAny<TreinoAluno>(), It.IsAny<CancellationToken>()), Times.Once);

        // -------------------------------------------------------
        // 6. Registrar execução (aluno)
        // -------------------------------------------------------
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treino.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treino);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _userContext.Setup(u => u.PerfilId).Returns(aluno.Id);

        var registrarExecucaoHandler = new RegistrarExecucaoHandler(
            _treinoRepo.Object, _alunoRepo.Object, _execucaoRepo.Object,
            _unitOfWork.Object, _userContext.Object,
            Mock.Of<ILogger<RegistrarExecucaoHandler>>());

        var execucao = await registrarExecucaoHandler.HandleAsync(new RegistrarExecucaoCommand(
            treino.Id, aluno.Id, DateTime.UtcNow, null, []));

        execucao.TreinoId.Should().Be(treino.Id);
        execucao.AlunoId.Should().Be(aluno.Id);
        _execucaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<ExecucaoTreino>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
