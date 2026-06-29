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

    private void SetupAlunoBaseRepos(Aluno aluno) =>
        _vinculoRepo.Setup(r => r.ListarTodosPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([]);

    private void SetupTreinadorBaseRepos(Treinador treinador)
    {
        _vinculoRepo.Setup(r => r.ListarTodosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([]);
        _pacoteRepo.Setup(r => r.ListarPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([]);
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinador.Id, 1, int.MaxValue,
                null, null, null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<(Treino, string?)>().AsReadOnly() as IReadOnlyList<(Treino, string?)>, 0));
    }

    // ── conta not found ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ContaInexistente_RetornaNotFound()
    {
        var contaId = Guid.NewGuid();
        SetupContaNotFound(contaId);

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId, contaId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
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
        SetupAlunoBaseRepos(aluno);
        _assinaturaRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([]);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<Treino>().AsReadOnly() as IReadOnlyList<Treino>, 0));
        _execucaoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                     .ReturnsAsync([]);
        SetupEmailLogs("aluno@test.com");
        SetupLogAprovacao();

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId, contaId));

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
        _vinculoRepo.Setup(r => r.ListarTodosPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([vinculo]);
        _assinaturaRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([assinatura]);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync([]);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<Treino>().AsReadOnly() as IReadOnlyList<Treino>, 0));
        _execucaoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                     .ReturnsAsync([]);
        SetupEmailLogs(conta.Email.Value);
        SetupLogAprovacao();

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId, contaId));

        result.IsSuccess.Should().BeTrue();
        var export = result.Value;

        // Vínculo exportado contém apenas IDs — sem nome do treinador ou dados de terceiros
        export.Vinculos.Should().HaveCount(1);
        export.Vinculos[0].TreinadorId.Should().Be(treinadorId);
        // O export não deve conter um campo "NomeTreinador" — o tipo VinculoExportDto não tem esse campo
        typeof(VinculoExportDto).GetProperty("NomeTreinador").Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_Aluno_VinculoPendenteSemAssinatura_Aparece()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.Aluno, "aluno@test.com");
        var aluno = Aluno.Criar(contaId, "Aluno Pendente", TestData.Agora).Value;
        var treinadorId = Guid.NewGuid();

        var vinculoPendente = VinculoTreinadorAluno.Criar(treinadorId, aluno.Id, TestData.Agora).Value;
        vinculoPendente.Status.Should().Be(VinculoStatus.AguardandoAprovacao);

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ListarTodosPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([vinculoPendente]);
        _assinaturaRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([]);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<Treino>().AsReadOnly() as IReadOnlyList<Treino>, 0));
        _execucaoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                     .ReturnsAsync([]);
        SetupEmailLogs("aluno@test.com");
        SetupLogAprovacao();

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId, contaId));

        result.IsSuccess.Should().BeTrue();
        result.Value.Vinculos.Should().HaveCount(1);
        result.Value.Vinculos[0].Status.Should().Be(VinculoStatus.AguardandoAprovacao.ToString());
        result.Value.Vinculos[0].TreinadorId.Should().Be(treinadorId);
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
        SetupTreinadorBaseRepos(treinador);
        SetupEmailLogs("treinador@test.com");
        SetupLogAprovacao();

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId, contaId));

        result.IsSuccess.Should().BeTrue();
        var export = result.Value;
        export.Treinador.Should().NotBeNull();
        export.Treinador!.Nome.Should().Be("Carlos Coach");
        export.Aluno.Should().BeNull();
        export.Assinaturas.Should().BeEmpty();
        export.Pacotes.Should().BeEmpty();
        export.Treinos.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Treinador_VinculosInativosPendentes_Aparecem()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.Treinador, "coach@test.com");
        var treinador = Treinador.Criar(contaId, "Coach", TestData.Agora).Value;

        var alunoId1 = Guid.NewGuid();
        var alunoId2 = Guid.NewGuid();
        var vinculoInativo = VinculoTreinadorAluno.Criar(treinador.Id, alunoId1, TestData.Agora).Value;
        vinculoInativo.Inativar(TestData.Agora);
        var vinculoPendente = VinculoTreinadorAluno.Criar(treinador.Id, alunoId2, TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(treinador);
        _vinculoRepo.Setup(r => r.ListarTodosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([vinculoInativo, vinculoPendente]);
        _pacoteRepo.Setup(r => r.ListarPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>()))
                   .ReturnsAsync([]);
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinador.Id, 1, int.MaxValue,
                null, null, null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<(Treino, string?)>().AsReadOnly() as IReadOnlyList<(Treino, string?)>, 0));
        SetupEmailLogs("coach@test.com");
        SetupLogAprovacao();

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId, contaId));

        result.IsSuccess.Should().BeTrue();
        result.Value.Vinculos.Should().HaveCount(2);
        result.Value.Vinculos.Should().Contain(v => v.Status == VinculoStatus.Inativo.ToString());
        result.Value.Vinculos.Should().Contain(v => v.Status == VinculoStatus.AguardandoAprovacao.ToString());
    }

    [Fact]
    public async Task HandleAsync_FalhaNoLog_RetornaFailureSemExport()
    {
        // Guid.Empty as ContaId triggers LogAprovacao.Registrar validation failure (realizadoPorId invalid).
        var contaId = Guid.Empty;
        var conta = CriarConta(TipoConta.Aluno, "log-fail@test.com");
        var aluno = Aluno.Criar(Guid.NewGuid(), "Log Fail", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ListarTodosPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                    .ReturnsAsync([]);
        _assinaturaRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([]);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<Treino>().AsReadOnly() as IReadOnlyList<Treino>, 0));
        _execucaoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                     .ReturnsAsync([]);
        SetupEmailLogs("log-fail@test.com");

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId, contaId));

        result.IsFailure.Should().BeTrue();
        _logAprovacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── aluno export TOTALMENTE populado — exercita todos os Map* e getters de DTO ──

    [Fact]
    public async Task HandleAsync_Aluno_Populado_MapeiaTodosOsCampos()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.Aluno, "joana@test.com");
        var aluno = Aluno.Criar(
            contaId, "Joana Aluna", TestData.Agora,
            email: "joana@test.com",
            telefone: "+5511999998888",
            diasDisponiveis: 4,
            tempoDisponivelMinutos: TempoDisponivel.UmaHora,
            finalidade: FinalidadeTreino.Hipertrofia,
            focoTreino: "Membros superiores",
            nivelCondicionamento: NivelCondicionamento.Intermediario,
            limitacoesFisicas: "Joelho direito",
            doencas: "Nenhuma",
            observacoesAdicionais: "Treina pela manhã").Value;

        var treinadorId = Guid.NewGuid();
        var pacoteId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, aluno.Id, TestData.Agora).Value;
        var assinatura = AssinaturaAluno.Criar(vinculo.Id, pacoteId, treinadorId, aluno.Id, 149.90m, TestData.Agora).Value;

        var pagamento = Pagamento.Criar(assinatura.Id, 149.90m, TestData.Agora, MetodoPagamento.Cartao).Value;
        pagamento.MarcarPago(TestData.Agora);

        var treino = Treino.Criar("Treino A", ObjetivoTreino.Forca, treinadorId, TestData.Agora, DificuldadeTreino.Avancado).Value;
        var execucao = ExecucaoTreino.Criar(treino.Id, aluno.Id, TestData.Agora, TestData.Agora, "Concluído com carga máxima").Value;

        var emailLog = EmailDeliveryLog.Criar("rid-1", "delivered", "joana@test.com", TestData.Agora, TestData.Agora);
        var waLog = WhatsAppDeliveryLog.Criar("mid-1", "read", "+5511999998888", TestData.Agora, TestData.Agora);

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _vinculoRepo.Setup(r => r.ListarTodosPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync([vinculo]);
        _assinaturaRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync([assinatura]);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync([pagamento]);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<Treino> { treino }.AsReadOnly() as IReadOnlyList<Treino>, 1));
        _execucaoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                     .ReturnsAsync([execucao]);
        _emailLogRepo.Setup(r => r.ListarPorEmailAsync("joana@test.com", It.IsAny<CancellationToken>())).ReturnsAsync([emailLog]);
        _waLogRepo.Setup(r => r.ListarPorTelefoneAsync("+5511999998888", It.IsAny<CancellationToken>())).ReturnsAsync([waLog]);
        SetupLogAprovacao();

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId, contaId));

        result.IsSuccess.Should().BeTrue();
        var export = result.Value;

        export.Conta.ContaId.Should().Be(conta.Id);
        export.Conta.Email.Should().Be("joana@test.com");
        export.Conta.TipoConta.Should().Be("Aluno");

        export.Aluno.Should().NotBeNull();
        var a = export.Aluno!;
        a.AlunoId.Should().Be(aluno.Id);
        a.Nome.Should().Be("Joana Aluna");
        a.Email.Should().Be("joana@test.com");
        a.Telefone.Should().Be("+5511999998888");
        a.DiasDisponiveis.Should().Be(4);
        a.TempoDisponivelMinutos.Should().Be(TempoDisponivel.UmaHora.ToString());
        a.Finalidade.Should().Be(FinalidadeTreino.Hipertrofia.ToString());
        a.FocoTreino.Should().Be("Membros superiores");
        a.NivelCondicionamento.Should().Be(NivelCondicionamento.Intermediario.ToString());
        a.LimitacoesFisicas.Should().Be("Joelho direito");
        a.Doencas.Should().Be("Nenhuma");
        a.ObservacoesAdicionais.Should().Be("Treina pela manhã");

        export.Assinaturas.Should().HaveCount(1);
        export.Assinaturas[0].AssinaturaId.Should().Be(assinatura.Id);
        export.Assinaturas[0].PacoteId.Should().Be(pacoteId);
        export.Assinaturas[0].TreinadorId.Should().Be(treinadorId);
        export.Assinaturas[0].Valor.Should().Be(149.90m);

        export.Pagamentos.Should().HaveCount(1);
        export.Pagamentos[0].PagamentoId.Should().Be(pagamento.Id);
        export.Pagamentos[0].AssinaturaId.Should().Be(assinatura.Id);
        export.Pagamentos[0].Valor.Should().Be(149.90m);
        export.Pagamentos[0].Status.Should().Be("Pago");
        export.Pagamentos[0].MetodoPagamento.Should().Be(MetodoPagamento.Cartao.ToString());
        export.Pagamentos[0].DataPagamento.Should().Be(TestData.Agora);

        export.Vinculos.Should().HaveCount(1);
        export.Vinculos[0].VinculoId.Should().Be(vinculo.Id);
        export.Vinculos[0].TreinadorId.Should().Be(treinadorId);
        export.Vinculos[0].AlunoId.Should().Be(aluno.Id);

        export.Treinos.Should().HaveCount(1);
        export.Treinos[0].TreinoId.Should().Be(treino.Id);
        export.Treinos[0].Nome.Should().Be("Treino A");
        export.Treinos[0].Objetivo.Should().Be(ObjetivoTreino.Forca.ToString());
        export.Treinos[0].Dificuldade.Should().Be(DificuldadeTreino.Avancado.ToString());

        export.Execucoes.Should().HaveCount(1);
        export.Execucoes[0].ExecucaoId.Should().Be(execucao.Id);
        export.Execucoes[0].TreinoId.Should().Be(treino.Id);
        export.Execucoes[0].Observacao.Should().Be("Concluído com carga máxima");

        export.EmailDeliveryLogs.Should().HaveCount(1);
        export.EmailDeliveryLogs[0].LogId.Should().Be(emailLog.Id);
        export.EmailDeliveryLogs[0].EventType.Should().Be("delivered");
        export.WhatsAppDeliveryLogs.Should().HaveCount(1);
        export.WhatsAppDeliveryLogs[0].LogId.Should().Be(waLog.Id);
        export.WhatsAppDeliveryLogs[0].EventType.Should().Be("read");
    }

    // ── treinador export TOTALMENTE populado — exercita MapPacote/MapTreino/MapVinculo ──

    [Fact]
    public async Task HandleAsync_Treinador_Populado_MapeiaPacotesTreinosVinculos()
    {
        var contaId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.Treinador, "coach@test.com");
        var treinador = Treinador.Criar(contaId, "Coach Pro", TestData.Agora, "+5521988887777").Value;
        treinador.Aprovar(Guid.NewGuid(), TestData.Agora);

        var alunoId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, alunoId, TestData.Agora).Value;
        var pacote = Pacote.Criar(treinador.Id, "Plano Premium", 299.90m, TestData.Agora, "Acompanhamento completo").Value;
        var treino = Treino.Criar("Treino Full Body", ObjetivoTreino.Resistencia, treinador.Id, TestData.Agora, DificuldadeTreino.Intermediario).Value;

        var emailLog = EmailDeliveryLog.Criar("rid-2", "bounced", "coach@test.com", TestData.Agora, TestData.Agora);
        var waLog = WhatsAppDeliveryLog.Criar("mid-2", "delivered", "+5521988887777", TestData.Agora, TestData.Agora);

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _vinculoRepo.Setup(r => r.ListarTodosPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync([vinculo]);
        _pacoteRepo.Setup(r => r.ListarPorTreinadorAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync([pacote]);
        _treinoRepo.Setup(r => r.ListarPorTreinadorAsync(treinador.Id, 1, int.MaxValue, null, null, null, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<(Treino, string?)> { (treino, "Aluno X") }.AsReadOnly() as IReadOnlyList<(Treino, string?)>, 1));
        _emailLogRepo.Setup(r => r.ListarPorEmailAsync("coach@test.com", It.IsAny<CancellationToken>())).ReturnsAsync([emailLog]);
        _waLogRepo.Setup(r => r.ListarPorTelefoneAsync("+5521988887777", It.IsAny<CancellationToken>())).ReturnsAsync([waLog]);
        SetupLogAprovacao();

        var result = await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId, contaId));

        result.IsSuccess.Should().BeTrue();
        var export = result.Value;

        export.Treinador.Should().NotBeNull();
        export.Treinador!.TreinadorId.Should().Be(treinador.Id);
        export.Treinador.Nome.Should().Be("Coach Pro");
        export.Treinador.Telefone.Should().Be("+5521988887777");
        export.Treinador.AprovadoEm.Should().Be(TestData.Agora);

        export.Vinculos.Should().HaveCount(1);
        export.Vinculos[0].TreinadorId.Should().Be(treinador.Id);

        export.Pacotes.Should().HaveCount(1);
        export.Pacotes[0].PacoteId.Should().Be(pacote.Id);
        export.Pacotes[0].Nome.Should().Be("Plano Premium");
        export.Pacotes[0].Preco.Should().Be(299.90m);
        export.Pacotes[0].Descricao.Should().Be("Acompanhamento completo");
        export.Pacotes[0].IsAtivo.Should().BeTrue();

        export.Treinos.Should().HaveCount(1);
        export.Treinos[0].TreinoId.Should().Be(treino.Id);
        export.Treinos[0].Objetivo.Should().Be(ObjetivoTreino.Resistencia.ToString());

        export.EmailDeliveryLogs.Should().ContainSingle().Which.EventType.Should().Be("bounced");
        export.WhatsAppDeliveryLogs.Should().ContainSingle().Which.EventType.Should().Be("delivered");
    }

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
        SetupAlunoBaseRepos(aluno);
        _assinaturaRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([]);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<Treino>().AsReadOnly() as IReadOnlyList<Treino>, 0));
        _execucaoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                     .ReturnsAsync([]);
        SetupEmailLogs(conta.Email.Value);
        SetupLogAprovacao();

        await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId, contaId));

        _logAprovacaoRepo.Verify(
            r => r.AdicionarAsync(
                It.Is<LogAprovacao>(l => l.TipoAcao == TipoAcaoAprovacao.ExportacaoDados),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void DadosPessoaisExport_NaoExpoeCredencialOuSegredoMfa()
    {
        var proibidos = new[] { "secret", "senha", "password", "recovery", "totp", "tokenhash", "codigohash" };

        var tipos = new[]
        {
            typeof(DadosPessoaisExport), typeof(ContaExportDto), typeof(AlunoExportDto),
            typeof(TreinadorExportDto), typeof(VinculoExportDto), typeof(AssinaturaExportDto),
            typeof(PagamentoExportDto), typeof(PacoteExportDto), typeof(TreinoExportDto),
            typeof(ExecucaoExportDto), typeof(EmailDeliveryLogExportDto), typeof(WhatsAppDeliveryLogExportDto),
        };

        var nomes = tipos.SelectMany(t => t.GetProperties()).Select(p => p.Name.ToLowerInvariant());

        nomes.Should().NotContain(n => proibidos.Any(n.Contains));
    }

    [Fact]
    public async Task HandleAsync_Admin_RegistraAtorReal()
    {
        var contaId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var conta = CriarConta(TipoConta.Aluno);
        var aluno = Aluno.Criar(contaId, "Titular", TestData.Agora).Value;

        _contaRepo.Setup(r => r.ObterPorIdAsync(contaId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(conta);
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(aluno);
        SetupAlunoBaseRepos(aluno);
        _assinaturaRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, It.IsAny<CancellationToken>()))
                       .ReturnsAsync([]);
        _treinoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<Treino>().AsReadOnly() as IReadOnlyList<Treino>, 0));
        _execucaoRepo.Setup(r => r.ListarPorAlunoAsync(aluno.Id, 1, int.MaxValue, It.IsAny<CancellationToken>()))
                     .ReturnsAsync([]);
        SetupEmailLogs(conta.Email.Value);
        SetupLogAprovacao();

        await _handler.HandleAsync(new ExportarDadosPessoaisCommand(contaId, adminId));

        _logAprovacaoRepo.Verify(
            r => r.AdicionarAsync(
                It.Is<LogAprovacao>(l => l.RealizadoPorId == adminId && l.EntidadeId == contaId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
