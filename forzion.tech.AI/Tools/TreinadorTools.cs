using System.Text.Json;
using forzion.tech.Application.Interfaces.Repositories;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace forzion.tech.AI.Tools;

public sealed class TreinadorTools
{
    private readonly IVinculoTreinadorAlunoRepository _vinculo;
    private readonly IExecucaoTreinoRepository _execucao;
    private readonly ITreinoAlunoRepository _treinoAluno;
    private readonly ILogger<TreinadorTools> _logger;

    public TreinadorTools(
        IVinculoTreinadorAlunoRepository vinculo,
        IExecucaoTreinoRepository execucao,
        ITreinoAlunoRepository treinoAluno,
        ILogger<TreinadorTools> logger)
    {
        _vinculo = vinculo;
        _execucao = execucao;
        _treinoAluno = treinoAluno;
        _logger = logger;
    }

    public IList<AITool> BuildTools(Guid treinadorId)
    {
        return
        [
            AIFunctionFactory.Create(
                async () =>
                {
                    _logger.LogInformation("ToolCall get_meus_alunos TreinadorId={Id}", treinadorId);
                    var detalhes = await _vinculo.ListarComDetalhesAsync(treinadorId, status: null, pagina: 1, tamanhoPagina: 50);
                    if (!detalhes.Items.Any()) return "Nenhum aluno vinculado ativamente.";
                    var linhas = detalhes.Items.Select(v => $"- {v.NomeAluno} (ID: {v.Vinculo.AlunoId})");
                    return string.Join("\n", linhas);
                },
                name: "get_meus_alunos",
                description: "Lista todos os alunos com vínculo ativo com o treinador. Sem parâmetros."),

            AIFunctionFactory.Create(
                GetProgressoAlunoFunc(treinadorId),
                name: "get_progresso_aluno",
                description: "Retorna histórico de execuções de um aluno vinculado. Parâmetros: alunoId (GUID string), ultimas (int 1-30)."),

            AIFunctionFactory.Create(
                GetFichasAlunoFunc(treinadorId),
                name: "get_fichas_aluno",
                description: "Lista fichas de treino ativas atribuídas a um aluno vinculado. Parâmetro: alunoId (GUID string)."),

            AIFunctionFactory.Create(
                GetExecucoesRecentesAlunoFunc(treinadorId),
                name: "get_execucoes_recentes_aluno",
                description: "Retorna execuções recentes (últimos 30 dias) de um aluno vinculado. Parâmetros: alunoId (GUID string), ultimas (int 1-10)."),

            // WRITE TIER — retorna preview JSON, nunca persiste diretamente
            AIFunctionFactory.Create(
                SugerirFichaTreinoFunc(treinadorId),
                name: "sugerir_ficha_treino",
                description: "Gera sugestão de ficha de treino para um aluno. IMPORTANTE: retorna apenas preview — o treinador deve confirmar antes de salvar. Parâmetros: alunoId (GUID string), objetivo (string), dificuldade (Iniciante/Intermediário/Avançado), numeroDeTreinos (int 1-7)."),
        ];
    }

    private Func<string, int, Task<string>> GetProgressoAlunoFunc(Guid treinadorId) =>
        async (string alunoId, int ultimas) =>
        {
            _logger.LogInformation("ToolCall get_progresso_aluno TreinadorId={TId} AlunoId={AId}", treinadorId, alunoId);
            if (!Guid.TryParse(alunoId, out var alunoGuid)) return "ID de aluno inválido.";

            // CRÍTICO: validar vínculo ativo — impede Treinador X ver dados de aluno do Treinador Y
            var vinculo = await _vinculo.ObterAtivoAsync(treinadorId, alunoGuid);
            if (vinculo is null) return "Aluno não encontrado ou sem vínculo ativo com este treinador.";

            ultimas = Math.Clamp(ultimas, 1, 30);
            var execucoes = await _execucao.ListarComNomePorAlunoAsync(alunoGuid, pagina: 1, tamanhoPagina: ultimas);
            if (!execucoes.Any()) return "Aluno ainda não registrou execuções de treino.";

            var linhas = execucoes.Select(e =>
                $"- {e.DataExecucao:dd/MM/yyyy} | {e.NomeTreino} | {e.TotalExercicios} ex | {e.TotalSeries} séries");
            return $"Últimas {ultimas} execuções:\n{string.Join("\n", linhas)}";
        };

    private Func<string, Task<string>> GetFichasAlunoFunc(Guid treinadorId) =>
        async (string alunoId) =>
        {
            _logger.LogInformation("ToolCall get_fichas_aluno TreinadorId={TId} AlunoId={AId}", treinadorId, alunoId);
            if (!Guid.TryParse(alunoId, out var alunoGuid)) return "ID de aluno inválido.";

            // CRÍTICO: validar vínculo ativo
            var vinculo = await _vinculo.ObterAtivoAsync(treinadorId, alunoGuid);
            if (vinculo is null) return "Aluno não encontrado ou sem vínculo ativo com este treinador.";

            var fichas = await _treinoAluno.ListarAtivosComNomePorParAsync(treinadorId, alunoGuid);
            if (!fichas.Any()) return "Nenhuma ficha ativa atribuída a este aluno.";

            var linhas = fichas.Select(f => $"- {f.NomeTreino} (ID: {f.TreinoAluno.TreinoId})");
            return string.Join("\n", linhas);
        };

    private Func<string, int, Task<string>> GetExecucoesRecentesAlunoFunc(Guid treinadorId) =>
        async (string alunoId, int ultimas) =>
        {
            _logger.LogInformation("ToolCall get_execucoes_recentes_aluno TreinadorId={TId} AlunoId={AId}", treinadorId, alunoId);
            if (!Guid.TryParse(alunoId, out var alunoGuid)) return "ID de aluno inválido.";

            // CRÍTICO: validar vínculo ativo
            var vinculo = await _vinculo.ObterAtivoAsync(treinadorId, alunoGuid);
            if (vinculo is null) return "Aluno não encontrado ou sem vínculo ativo com este treinador.";

            ultimas = Math.Clamp(ultimas, 1, 10);
            var de = DateTime.UtcNow.AddDays(-30);
            var ate = DateTime.UtcNow;
            var execucoes = await _execucao.ListarPorAlunoComExerciciosAsync(alunoGuid, de, ate);
            if (!execucoes.Any()) return "Nenhuma execução registrada nos últimos 30 dias.";

            var linhas = execucoes.Take(ultimas).Select(e =>
                $"[{e.DataExecucao:dd/MM}] {e.Exercicios.Count} exercícios");
            return string.Join("\n", linhas);
        };

    private Func<string, string, string, int, Task<string>> SugerirFichaTreinoFunc(Guid treinadorId) =>
        async (string alunoId, string objetivo, string dificuldade, int numeroDeTreinos) =>
        {
            _logger.LogInformation("ToolCall sugerir_ficha_treino TreinadorId={TId} AlunoId={AId}", treinadorId, alunoId);
            if (!Guid.TryParse(alunoId, out var alunoGuid)) return "ID de aluno inválido.";

            // CRÍTICO: validar vínculo ativo antes de gerar sugestão
            var vinculo = await _vinculo.ObterAtivoAsync(treinadorId, alunoGuid);
            if (vinculo is null) return "Aluno não encontrado ou sem vínculo ativo com este treinador.";

            numeroDeTreinos = Math.Clamp(numeroDeTreinos, 1, 7);

            // Retorna APENAS preview — persistência ocorre no endpoint /apply-suggestion após aprovação
            var preview = new
            {
                __tipo = "sugestao_ficha_preview",
                __requerAprovacao = true,
                alunoId = alunoGuid,
                treinadorId,
                objetivo,
                dificuldade,
                numeroDeTreinos,
                geradoEm = DateTime.UtcNow
            };

            return $"PREVIEW (aguardando aprovação do treinador):\n{JsonSerializer.Serialize(preview, new JsonSerializerOptions { WriteIndented = true })}";
        };
}
