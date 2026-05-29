using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Conta.Lgpd;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.Builders;
using Moq;

namespace forzion.tech.Tests.Application.Lgpd;

public class ExportarDadosPessoaisHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IEmailDeliveryLogRepository> _emailLogRepo = new();
    private readonly Mock<IWhatsAppDeliveryLogRepository> _waLogRepo = new();
    private readonly Mock<ILogAprovacaoRepository> _logAprovacaoRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<TimeProvider> _timeProvider = new();

    private readonly ExportarDadosPessoaisHandler _handler;

    public ExportarDadosPessoaisHandlerTests()
    {
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(TestData.Agora);

        _handler = new ExportarDadosPessoaisHandler(
            _contaRepo.Object,
            _alunoRepo.Object,
            _treinadorRepo.Object,
            _vinculoRepo.Object,
            _assinaturaRepo.Object,
            _pagamentoRepo.Object,
            _pacoteRepo.Object,
            _treinoRepo.Object,
            _execucaoRepo.Object,
            _emailLogRepo.Object,
            _waLogRepo.Object,
            _logAprovacaoRepo.Object,
            _uow.Object,
            _timeProvider.Object);
    }

    private static Conta CriarConta(TipoConta tipo, string email = "user@test.com") =>
        Conta.Criar(Email.Criar(email).Value, "hash", tipo, TestData.Agora).Value;

    // ── helpers ──────────────────────────────────────────────────────────────

    private void SetupContaNotFound(Guid contaId) =>
        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((Conta?)null);

    private void SetupEmailLogs(string email) =>
        _emailLogRepo.Setup(r => r.ListarPorEmailAsync(email, It.IsAny<CancellationToken>()))
                     .ReturnsAsync([]);

    private void SetupLogAprovacao() =>
        _logAprovacaoRepo.Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
                         .Returns(Task.CompletedTask);

    // ── conta not found ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ContaInexistente_RetornaNotFound()
    {
        var contaId = Guid.NewGuid();
        SetupContaNotFound(contaId);

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(forzion.tech.Domain.Shared.ErrorType.NotFound);
    }

    // ── aluno export ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Aluno_AgregaTodasAsSecoes()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.Aluno, "aluno@test.com");
        var aluno = Aluno.Criar(contaId, "João Silva", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);
        _assinaturaRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([]);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<Treino>().AsReadOnly() as IReadOnlyList<Treino>, 0));
        _execucaoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                     .ReturnsAsync([]);
        SetupEmailLogs("aluno@test.com");
        SetupLogAprovacao();

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId));

        result.IsSuccess.Should().BeTrue();
        var export = result.Value;
        export.Versao.Should().Be("1.0");
        export.Aluno.Should().NotBeNull();
        export.Aluno!.Nome.Should().Be("João Silva");
        export.Treinador.Should().BeNull();
        export.Assinaturas.Should().BeEmpty();
        export.Pagamentos.Should().BeEmpty();
        export.Pacotes.Should().BeEmpty();
        export.Treinos.Should().BeEmpty();
        export.Execucoes.Should().BeEmpty();
        export.EmailDeliveryLogs.Should().BeEmpty();
        export.WhatsAppDeliveryLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Aluno_NaoIncluiDadosDeTerceiros()
    {
        // Vínculos retornam apenas o VinculoId — o nome do treinador NÃO aparece no DTO
        var contaId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Maria", TestData.Agora).Value;
        var treinadorId = Guid.NewGuid();
        var vinculoId = Guid.NewGuid();
        var assinatura = AssinaturaAluno.Criar(vinculoId, Guid.NewGuid(), treinadorId, aluno.Id, 100m, TestData.Agora).Value;

        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, aluno.Id, TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);
        _assinaturaRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([assinatura]);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync([]);
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(assinatura.VinculoId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(vinculo);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<Treino>().AsReadOnly() as IReadOnlyList<Treino>, 0));
        _execucaoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                     .ReturnsAsync([]);
        SetupEmailLogs(conta.Email.Value);
        SetupLogAprovacao();

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId));

        result.IsSuccess.Should().BeTrue();
        var export = result.Value;

        // Vínculo exportado contém apenas IDs — sem nome do treinador ou dados de terceiros
        export.Vinculos.Should().HaveCount(1);
        export.Vinculos[0].TreinadorId.Should().Be(treinadorId);
        // O export não deve conter um campo "NomeTreinador" — o tipo VinculoExportDto não tem esse campo
        typeof(VinculoExportDto).GetProperty("NomeTreinador").Should().BeNull();
    }

    // ── treinador export ──────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Treinador_AgregaTodasAsSecoes()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.Treinador, "treinador@test.com");
        var treinador = Treinador.Criar(contaId, "Carlos Coach", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(treinador);
        _vinculoRepo.Setup(r => r.ListarAtivosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([]);
        _pacoteRepo.Setup(r => r.ListarPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([]);
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinador.Id, 1, int.MaxValue,
                null, null, null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<(Treino, string?)>().AsReadOnly() as IReadOnlyList<(Treino, string?)>, 0));
        SetupEmailLogs("treinador@test.com");
        SetupLogAprovacao();

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId));

        result.IsSuccess.Should().BeTrue();
        var export = result.Value;
        export.Treinador.Should().NotBeNull();
        export.Treinador!.Nome.Should().Be("Carlos Coach");
        export.Aluno.Should().BeNull();
        export.Assinaturas.Should().BeEmpty();
        export.Pacotes.Should().BeEmpty();
        export.Treinos.Should().BeEmpty();
    }

    // ── audit log ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Aluno_GravaLogAuditoria()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Audit Test", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);
        _assinaturaRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([]);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<Treino>().AsReadOnly() as IReadOnlyList<Treino>, 0));
        _execucaoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                     .ReturnsAsync([]);
        SetupEmailLogs(conta.Email.Value);
        SetupLogAprovacao();

        await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId));

        _logAprovacaoRepo.Verify(
            r => r.AdicionarAsync(
                It.Is<LogAprovacao>(l => l.TipoAcao == TipoAcaoAprovacao.ExportacaoDados),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
