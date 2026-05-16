using FluentAssertions;
using forzion.tech.AI.GuardRails;
using forzion.tech.AI.Tools;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.AI.Tools;

public class TreinadorToolsTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<IDraftSuggestionService> _draftService = new();
    private readonly Mock<IDraftRequestTracker> _draftTracker = new();
    private readonly Mock<ILogger<TreinadorTools>> _logger = new();

    private readonly Guid _treinadorId = Guid.NewGuid();
    private readonly Guid _alunoId = Guid.NewGuid();

    private TreinadorTools BuildSut() => new(
        _vinculoRepo.Object,
        _execucaoRepo.Object,
        _treinoAlunoRepo.Object,
        _draftService.Object,
        _draftTracker.Object,
        _logger.Object);

    private AIFunction GetTool(TreinadorTools sut, string name)
    {
        var tools = sut.BuildTools(_treinadorId);
        return (AIFunction)tools.First(t => t.Name == name);
    }

    private Task<object?> InvokeTool(AIFunction tool, Dictionary<string, object?> args)
        => tool.InvokeAsync(new AIFunctionArguments(args)).AsTask();

    // ── cross-tenant: vínculo inativo bloqueia acesso ────────────────────────

    [Fact]
    public async Task GetProgressoAluno_SemVinculo_RetornaMensagemDeAcesso()
    {
        _vinculoRepo.Setup(v => v.ObterAtivoAsync(_treinadorId, _alunoId, default))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var tool = GetTool(BuildSut(), "get_progresso_aluno");
        var result = await InvokeTool(tool, new()
        {
            ["alunoId"] = _alunoId.ToString(),
            ["ultimas"] = 5
        });

        result.ToString().Should().Contain("vínculo ativo");
        _execucaoRepo.Verify(e => e.ListarComNomePorAlunoAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task GetFichasAluno_SemVinculo_RetornaMensagemDeAcesso()
    {
        _vinculoRepo.Setup(v => v.ObterAtivoAsync(_treinadorId, _alunoId, default))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var tool = GetTool(BuildSut(), "get_fichas_aluno");
        var result = await InvokeTool(tool, new() { ["alunoId"] = _alunoId.ToString() });

        result.ToString().Should().Contain("vínculo ativo");
    }

    [Fact]
    public async Task GetExecucoesRecentesAluno_SemVinculo_RetornaMensagemDeAcesso()
    {
        _vinculoRepo.Setup(v => v.ObterAtivoAsync(_treinadorId, _alunoId, default))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var tool = GetTool(BuildSut(), "get_execucoes_recentes_aluno");
        var result = await InvokeTool(tool, new()
        {
            ["alunoId"] = _alunoId.ToString(),
            ["ultimas"] = 5
        });

        result.ToString().Should().Contain("vínculo ativo");
    }

    [Fact]
    public async Task SugerirFichaTreino_SemVinculo_NaoStoraDraft()
    {
        _vinculoRepo.Setup(v => v.ObterAtivoAsync(_treinadorId, _alunoId, default))
            .ReturnsAsync((VinculoTreinadorAluno?)null);

        var tool = GetTool(BuildSut(), "sugerir_ficha_treino");
        await InvokeTool(tool, new()
        {
            ["alunoId"] = _alunoId.ToString(),
            ["objetivo"] = "Hipertrofia",
            ["dificuldade"] = "Intermediário",
            ["numeroDeTreinos"] = 3
        });

        _draftService.Verify(d => d.StoreDraft(It.IsAny<SugestaoDraft>()), Times.Never);
    }

    // ── args inválidos ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetProgressoAluno_AlunoIdInvalido_RetornaMensagemErro()
    {
        var tool = GetTool(BuildSut(), "get_progresso_aluno");
        var result = await InvokeTool(tool, new()
        {
            ["alunoId"] = "nao-e-um-guid",
            ["ultimas"] = 5
        });

        result.ToString().Should().Contain("inválido");
        _vinculoRepo.Verify(v => v.ObterAtivoAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default), Times.Never);
    }

    // ── sugerir_ficha_treino com vínculo ativo ───────────────────────────────

    [Fact]
    public async Task SugerirFichaTreino_ComVinculo_StoraDraftESinaliza()
    {
        _vinculoRepo.Setup(v => v.ObterAtivoAsync(_treinadorId, _alunoId, default))
            .ReturnsAsync(VinculoTreinadorAluno.Criar(Guid.NewGuid(), Guid.NewGuid()));

        var draftId = Guid.NewGuid();
        _draftService.Setup(d => d.StoreDraft(It.IsAny<SugestaoDraft>())).Returns(draftId);

        var sut = BuildSut();
        var tool = GetTool(sut, "sugerir_ficha_treino");

        await InvokeTool(tool, new()
        {
            ["alunoId"] = _alunoId.ToString(),
            ["objetivo"] = "Hipertrofia",
            ["dificuldade"] = "Intermediário",
            ["numeroDeTreinos"] = 4
        });

        _draftService.Verify(d => d.StoreDraft(It.Is<SugestaoDraft>(
            draft => draft.TreinadorId == _treinadorId &&
                     draft.AlunoId == _alunoId &&
                     draft.NumeroDeTreinos == 4)), Times.Once);

        _draftTracker.VerifySet(t => t.PendingDraftId = draftId, Times.Once);
    }

    // ── numeroDeTreinos clamp ────────────────────────────────────────────────

    [Fact]
    public async Task SugerirFichaTreino_NumeroDeTreinosFora_EhClampeado()
    {
        _vinculoRepo.Setup(v => v.ObterAtivoAsync(_treinadorId, _alunoId, default))
            .ReturnsAsync(VinculoTreinadorAluno.Criar(Guid.NewGuid(), Guid.NewGuid()));

        SugestaoDraft? storedDraft = null;
        _draftService.Setup(d => d.StoreDraft(It.IsAny<SugestaoDraft>()))
            .Callback<SugestaoDraft>(d => storedDraft = d)
            .Returns(Guid.NewGuid());

        var tool = GetTool(BuildSut(), "sugerir_ficha_treino");
        await InvokeTool(tool, new()
        {
            ["alunoId"] = _alunoId.ToString(),
            ["objetivo"] = "Hipertrofia",
            ["dificuldade"] = "Iniciante",
            ["numeroDeTreinos"] = 99 // deve ser clampeado para 7
        });

        storedDraft.Should().NotBeNull();
        storedDraft!.NumeroDeTreinos.Should().Be(7);
    }
}
