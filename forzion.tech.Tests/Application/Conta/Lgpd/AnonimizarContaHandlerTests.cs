using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Lgpd;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.Builders;
using Moq;

namespace forzion.tech.Tests.Application.Lgpd;

public class AnonimizarContaHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IAssinanteRepository> _assinanteRepo = new();
    private readonly Mock<IEmailDeliveryLogRepository> _emailLogRepo = new();
    private readonly Mock<IWhatsAppDeliveryLogRepository> _waLogRepo = new();
    private readonly Mock<IMensagemSuporteRepository> _mensagemSuporteRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logAprovacaoRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IDbContextTransactionProvider> _transactionProvider = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ITokenRevogadoRepository> _tokenRevogadoRepo = new();
    private readonly Mock<IDatabaseErrorInspector> _dbErrorInspector = new();
    private readonly Mock<IRefreshTokenFamilyRepository> _refreshFamilyRepo = new();

    private readonly AnonimizarContaHandler _handler;

    private const string SenhaCorreta = "Senha@123";
    private const string HashCorreto = "$2a$12$hash_correto";

    public AnonimizarContaHandlerTests()
    {
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(TestData.Agora);
        _passwordHasher.Setup(p => p.Verify(SenhaCorreta, HashCorreto)).Returns(true);
        _passwordHasher.Setup(p => p.Verify(It.Is<string>(s => s != SenhaCorreta), It.IsAny<string>())).Returns(false);

        _logAprovacaoRepo.Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
                         .Returns(Task.CompletedTask);
        _emailLogRepo.Setup(r => r.AnonimizarPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);
        _waLogRepo.Setup(r => r.AnonimizarPorTelefoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                  .Returns(Task.CompletedTask);
        _mensagemSuporteRepo.Setup(r => r.ExcluirPorContaIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                            .Returns(Task.CompletedTask);
        _assinanteRepo.Setup(r => r.AnonimizarPorAlunoIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);
        _vinculoRepo.Setup(r => r.ListarAtivosEPendentesPorAlunoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync([]);
        _execucaoRepo.Setup(r => r.AnonimizarObservacoesPorAlunoIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);

        // tx noop: sem transação real, BeginTransactionAsync devolveria null (Moq) e o
        // `await using` quebraria — devolve um ITransaction mockado com commit/dispose noop.
        var mockTx = new Mock<ITransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _transactionProvider
            .Setup(p => p.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTx.Object);

        _handler = new AnonimizarContaHandler(
            _contaRepo.Object,
            _alunoRepo.Object,
            _treinadorRepo.Object,
            _vinculoRepo.Object,
            _execucaoRepo.Object,
            _assinanteRepo.Object,
            _emailLogRepo.Object,
            _waLogRepo.Object,
            _mensagemSuporteRepo.Object,
            _logAprovacaoRepo.Object,
            _passwordHasher.Object,
            _uow.Object,
            _transactionProvider.Object,
            _timeProvider.Object,
            _userContext.Object,
            _tokenRevogadoRepo.Object,
            _dbErrorInspector.Object,
            _refreshFamilyRepo.Object);
    }

    private static Conta CriarContaComHash(TipoConta tipo, string email = "user@test.com") =>
        Conta.Criar(Email.Criar(email).Value, HashCorreto, tipo, TestData.Agora).Value;

    // ── conta not found ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ContaInexistente_RetornaNotFound()
    {
        var contaId = Guid.NewGuid();
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((Conta?)null);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    // ── idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ContaJaAnonimizada_RetornaSuccessIdempotente()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        conta.Anonimizar(TestData.Agora); // already anonymized

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsSuccess.Should().BeTrue();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── password verification ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_SelfSenhaErrada_RetornaValidationError()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, "SenhaErrada@1"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error!.Code.Should().Be("conta.senha_incorreta");
    }

    [Fact]
    public async Task HandleAsync_SelfSenhaVazia_RetornaValidationError()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error!.Code.Should().Be("conta.senha_obrigatoria");
    }

    // ── aluno anonymization ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Aluno_AnonimizaContaEAluno()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno, "aluno@test.com");
        var aluno = Aluno.Criar(contaId, "Nome Real", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsSuccess.Should().BeTrue();
        conta.AnonimizadaEm.Should().Be(TestData.Agora);
        conta.PasswordHash.Should().BeEmpty();
        aluno.Nome.Should().Be("Usuário anonimizado");
        aluno.Email.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_Aluno_AnonimizaAssinanteReadModel()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Teste", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);

        await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        _assinanteRepo.Verify(
            r => r.AnonimizarPorAlunoIdAsync(aluno.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PurgaFamiliasDeRefreshDoTitular()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Teste", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);

        await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        _refreshFamilyRepo.Verify(
            r => r.ExcluirPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Aluno_ScrubDeliveryLogs()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno, "aluno@scrub.com");
        var aluno = Aluno.Criar(contaId, "Teste", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);

        await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        _emailLogRepo.Verify(
            r => r.AnonimizarPorEmailAsync("aluno@scrub.com", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── treinador anonymization ───────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_TreinadorComVinculosAtivos_RetornaBusinessError()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Treinador);
        var treinador = Treinador.Criar(contaId, "Coach Bloqueado", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(treinador);
        _vinculoRepo.Setup(r => r.TemVinculosAtivosAsync(treinador.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("conta.offboarding_necessario");
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // TemVinculosAtivosAsync também conta vínculos AguardandoAprovacao: pendentes
    // bloqueiam a anonimização (op LGPD irreversível) para não orfanar a solicitação.
    [Fact]
    public async Task HandleAsync_TreinadorComVinculoPendente_RetornaBusinessError()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Treinador);
        var treinador = Treinador.Criar(contaId, "Coach Pendente", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(treinador);
        _vinculoRepo.Setup(r => r.TemVinculosAtivosAsync(treinador.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("conta.offboarding_necessario");
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemVinculos_AnonimizaComSucesso()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Treinador, "treinador@test.com");
        var treinador = Treinador.Criar(contaId, "Coach Livre", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(treinador);
        _vinculoRepo.Setup(r => r.TemVinculosAtivosAsync(treinador.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsSuccess.Should().BeTrue();
        conta.AnonimizadaEm.Should().Be(TestData.Agora);
        treinador.Nome.Should().Be("Usuário anonimizado");
    }

    // ── admin bypass ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_AdminSemSenha_AnonimizaComSucesso()
    {
        var adminId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Target User", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);

        // Admin: RealizadoPorId != ContaId, SenhaAtual = null
        var result = await _handler.HandleAsync(
            new AnonimizarContaCommand(contaId, adminId, SenhaAtual: null));

        result.IsSuccess.Should().BeTrue();
        conta.AnonimizadaEm.Should().NotBeNull();
        // passwordHasher.Verify should never be called for admin path
        _passwordHasher.Verify(p => p.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── pagamentos retained ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Aluno_NaoExcluiPagamentosOuAssinaturas()
    {
        // The handler RETAINS financial records — no delete calls expected
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Aluno Paga", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);

        await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        // No methods that would delete financial data should be called
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaAluno>(), It.IsAny<CancellationToken>()), Times.Never);
        // Specifically verify we do NOT call any "delete" on pagamento/assinatura
        // (there are no Delete methods in those repos, verifying no unintended interactions)
    }

    // ── audit log + single commit ─────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Aluno_GravaLogAuditoriaECommit()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Audit", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);

        await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        _logAprovacaoRepo.Verify(
            r => r.AdicionarAsync(
                It.Is<LogAprovacao>(l =>
                    l.TipoAcao == TipoAcaoAprovacao.AnonimizacaoConta &&
                    l.EntidadeId == contaId &&
                    l.RealizadoPorId == contaId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoComVinculosAtivosEPendentes_InativaVinculosAntesDeCommit()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Vinculado", TestData.Agora).Value;
        var vinculo = VinculoTreinadorAluno.Criar(Guid.NewGuid(), aluno.Id, TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ListarAtivosEPendentesPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([vinculo]);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsSuccess.Should().BeTrue();
        vinculo.Status.Should().Be(VinculoStatus.Inativo);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoSemVinculos_SemErro()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Sem Vinculo", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_Aluno_ScrubaObservacoesExecucaoAntesDeCommit()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "ExecAluno", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);

        var commitCallOrder = new List<string>();
        _execucaoRepo.Setup(r => r.AnonimizarObservacoesPorAlunoIdAsync(aluno.Id, It.IsAny<CancellationToken>()))
                     .Callback(() => commitCallOrder.Add("scrub"))
                     .Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => commitCallOrder.Add("commit"))
            .Returns(Task.CompletedTask);

        await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        _execucaoRepo.Verify(r => r.AnonimizarObservacoesPorAlunoIdAsync(aluno.Id, It.IsAny<CancellationToken>()), Times.Once);
        // scrub must precede commit
        commitCallOrder.Should().Equal("scrub", "commit");
    }

    [Fact]
    public async Task HandleAsync_ApagaMensagensSuporteDoTitularAntesDoCommit()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);

        var ordem = new List<string>();
        _mensagemSuporteRepo.Setup(r => r.ExcluirPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                            .Callback(() => ordem.Add("excluir-suporte"))
                            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("commit"))
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsSuccess.Should().BeTrue();
        _mensagemSuporteRepo.Verify(r => r.ExcluirPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()), Times.Once);
        ordem.Should().Equal("excluir-suporte", "commit");
    }

    [Fact]
    public async Task HandleAsync_LogAprovacaoFalha_RetornaFailureSemCommit()
    {
        // LogAprovacao.Registrar only fails on invalid args; use Guid.Empty to force it.
        var contaId = Guid.Empty;
        var conta = CriarContaComHash(TipoConta.Aluno);
        var aluno = Aluno.Criar(Guid.NewGuid(), "Fail Log", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);

        // ContaId == Guid.Empty → LogAprovacao.Registrar produces failure (entidadeId guard)
        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsFailure.Should().BeTrue();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // JWT-01: a revogação é enfileirada ANTES do CommitAsync (mesma transação), não num
    // commit separado pós-anonimização — fecha a janela em que o jti seguiria válido se o
    // processo caísse entre os dois commits.
    [Fact]
    public async Task HandleAsync_SelfComJtiValido_EnfileiraRevogacaoAntesDoCommit()
    {
        var contaId = Guid.NewGuid();
        var jti = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Self Logout", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);
        _userContext.Setup(u => u.Jti).Returns(jti);
        _userContext.Setup(u => u.TokenExpiraEm).Returns(TestData.Agora.AddHours(1));

        var ordem = new List<string>();
        _tokenRevogadoRepo.Setup(r => r.AdicionarAsync(It.IsAny<TokenRevogado>(), It.IsAny<CancellationToken>()))
                          .Callback(() => ordem.Add("revoga")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => ordem.Add("commit")).Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsSuccess.Should().BeTrue();
        _tokenRevogadoRepo.Verify(
            r => r.AdicionarAsync(It.Is<TokenRevogado>(t => t.Jti == jti), It.IsAny<CancellationToken>()),
            Times.Once);
        ordem.Should().Equal("revoga", "commit");
    }

    // JWT-01 caminho idempotente: conta já anonimizada (1ª chamada pode ter falhado só na
    // revogação) + self + jti ativo → o retry ainda revoga e comita.
    [Fact]
    public async Task HandleAsync_ContaJaAnonimizada_SelfComJtiAtivo_RevogaToken()
    {
        var contaId = Guid.NewGuid();
        var jti = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        conta.Anonimizar(TestData.Agora);

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _userContext.Setup(u => u.Jti).Returns(jti);
        _userContext.Setup(u => u.TokenExpiraEm).Returns(TestData.Agora.AddHours(1));

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsSuccess.Should().BeTrue();
        _tokenRevogadoRepo.Verify(
            r => r.AdicionarAsync(It.Is<TokenRevogado>(t => t.Jti == jti), It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // JWT-01: jti já revogado (logout concorrente) → não duplica nem comita. A pré-checagem
    // evita o unique-violation que abortaria a transação.
    [Fact]
    public async Task HandleAsync_ContaJaAnonimizada_JtiJaRevogado_NaoDuplica()
    {
        var contaId = Guid.NewGuid();
        var jti = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        conta.Anonimizar(TestData.Agora);

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _userContext.Setup(u => u.Jti).Returns(jti);
        _userContext.Setup(u => u.TokenExpiraEm).Returns(TestData.Agora.AddHours(1));
        _tokenRevogadoRepo.Setup(r => r.EstaRevogadoAsync(jti, It.IsAny<CancellationToken>()))
                          .ReturnsAsync(true);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsSuccess.Should().BeTrue();
        _tokenRevogadoRepo.Verify(
            r => r.AdicionarAsync(It.IsAny<TokenRevogado>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ERRDB-01: a race de revogação concorrente colide no índice único (jti) → 23505. O handler
    // engole APENAS unique-violation (idempotente), confiando no inspector — não em substring de
    // mensagem, que quebra por wording/locale do driver.
    [Fact]
    public async Task HandleAsync_RevogacaoIdempotente_ViolacaoDeUnicidade_EngoleERetornaSuccess()
    {
        var contaId = Guid.NewGuid();
        var jti = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        conta.Anonimizar(TestData.Agora);

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _userContext.Setup(u => u.Jti).Returns(jti);
        _userContext.Setup(u => u.TokenExpiraEm).Returns(TestData.Agora.AddHours(1));
        _tokenRevogadoRepo.Setup(r => r.AdicionarAsync(It.IsAny<TokenRevogado>(), It.IsAny<CancellationToken>()))
                          .ThrowsAsync(new InvalidOperationException("23505"));
        _dbErrorInspector.Setup(i => i.EhViolacaoDeUnicidade(It.IsAny<Exception>())).Returns(true);

        var result = await _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_RevogacaoIdempotente_ErroNaoUnico_Propaga()
    {
        var contaId = Guid.NewGuid();
        var jti = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        conta.Anonimizar(TestData.Agora);

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _userContext.Setup(u => u.Jti).Returns(jti);
        _userContext.Setup(u => u.TokenExpiraEm).Returns(TestData.Agora.AddHours(1));
        _tokenRevogadoRepo.Setup(r => r.AdicionarAsync(It.IsAny<TokenRevogado>(), It.IsAny<CancellationToken>()))
                          .ThrowsAsync(new InvalidOperationException("connection reset"));
        _dbErrorInspector.Setup(i => i.EhViolacaoDeUnicidade(It.IsAny<Exception>())).Returns(false);

        var act = () => _handler.HandleAsync(new AnonimizarContaCommand(contaId, contaId, SenhaCorreta));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HandleAsync_AdminComJti_NaoRevogaTokenDoOperador()
    {
        var adminId = Guid.NewGuid();
        var contaId = Guid.NewGuid();
        var conta = CriarContaComHash(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Target", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);
        _userContext.Setup(u => u.Jti).Returns(Guid.NewGuid());
        _userContext.Setup(u => u.TokenExpiraEm).Returns(TestData.Agora.AddHours(1));

        var result = await _handler.HandleAsync(
            new AnonimizarContaCommand(contaId, adminId, SenhaAtual: null));

        result.IsSuccess.Should().BeTrue();
        _tokenRevogadoRepo.Verify(
            r => r.AdicionarAsync(It.IsAny<TokenRevogado>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
