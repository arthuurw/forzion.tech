using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace forzion.tech.AI.Tools;

public sealed class AlunoTools
{
    private readonly ITreinoAlunoRepository _treinoAluno;
    private readonly IExecucaoTreinoRepository _execucao;
    private readonly IExercicioRepository _exercicio;
    private readonly ILogger<AlunoTools> _logger;

    public AlunoTools(
        ITreinoAlunoRepository treinoAluno,
        IExecucaoTreinoRepository execucao,
        IExercicioRepository exercicio,
        ILogger<AlunoTools> logger)
    {
        _treinoAluno = treinoAluno;
        _execucao = execucao;
        _exercicio = exercicio;
        _logger = logger;
    }

    // Tools com alunoId capturado em closure — LLM nunca controla qual usuário é consultado
    public IList<AITool> BuildTools(Guid alunoId)
    {
        return
        [
            AIFunctionFactory.Create(
                async () =>
                {
                    _logger.LogInformation("ToolCall get_meus_treinos AlunoId={AlunoId}", alunoId);
                    var treinos = await _treinoAluno.ListarAtivosComNomePorAlunoAsync(alunoId);
                    if (!treinos.Any()) return "Nenhuma ficha de treino ativa no momento.";
                    var linhas = treinos.Select((t, i) =>
                        $"{i + 1}. {t.NomeTreino} (ID: {t.TreinoAluno.TreinoId})");
                    return string.Join("\n", linhas);
                },
                name: "get_meus_treinos",
                description: "Retorna as fichas de treino ativas do aluno autenticado. Sem parâmetros."),

            AIFunctionFactory.Create(
                GetHistoricoExecucoesFunc(alunoId),
                name: "get_historico_execucoes",
                description: "Retorna as últimas N execuções de treino do aluno. Parâmetro: ultimas (int, 1-20)."),

            AIFunctionFactory.Create(
                async () =>
                {
                    _logger.LogInformation("ToolCall get_proximo_treino AlunoId={AlunoId}", alunoId);
                    var treinos = await _treinoAluno.ListarAtivosComNomePorAlunoAsync(alunoId);
                    if (!treinos.Any()) return "Nenhuma ficha de treino ativa atribuída.";
                    var proximo = treinos[0];
                    return $"Próxima ficha sugerida: {proximo.NomeTreino} (ID: {proximo.TreinoAluno.TreinoId})";
                },
                name: "get_proximo_treino",
                description: "Sugere a próxima ficha de treino para o aluno. Sem parâmetros."),

            AIFunctionFactory.Create(
                GetDetalhesExercicioFunc(alunoId),
                name: "get_detalhe_exercicio",
                description: "Retorna detalhes de um exercício pelo ID (nome, grupo muscular, descrição). Parâmetro: exercicioId (GUID string)."),
        ];
    }

    // Separação das lambdas com parâmetros para resolver ambiguidade de tipo do compilador
    private Func<int, Task<string>> GetHistoricoExecucoesFunc(Guid alunoId) =>
        async (int ultimas) =>
        {
            _logger.LogInformation("ToolCall get_historico_execucoes AlunoId={AlunoId} Ultimas={N}", alunoId, ultimas);
            ultimas = Math.Clamp(ultimas, 1, 20);
            var execucoes = await _execucao.ListarComNomePorAlunoAsync(alunoId, pagina: 1, tamanhoPagina: ultimas);
            if (!execucoes.Any()) return "Nenhuma execução registrada ainda.";
            var linhas = execucoes.Select(e =>
                $"- {e.DataExecucao:dd/MM/yyyy} | {e.NomeTreino} | {e.TotalExercicios} exercícios | {e.TotalSeries} séries");
            return string.Join("\n", linhas);
        };

    private Func<string, Task<string>> GetDetalhesExercicioFunc(Guid alunoId) =>
        async (string exercicioId) =>
        {
            _logger.LogInformation("ToolCall get_detalhe_exercicio ExercicioId={Id}", exercicioId);
            if (!Guid.TryParse(exercicioId, out var id))
                return "ID de exercício inválido.";
            var exercicio = await _exercicio.ObterPorIdAsync(id);
            if (exercicio is null) return "Exercício não encontrado.";
            return $"Nome: {exercicio.Nome}\nGrupo muscular: {exercicio.GrupoMuscular}\nDescrição: {exercicio.Descricao ?? "Sem descrição cadastrada."}";
        };
}
