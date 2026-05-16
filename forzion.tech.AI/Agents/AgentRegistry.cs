namespace forzion.tech.AI.Agents;

public sealed class AgentRegistry
{
    private readonly AlunoAssistantAgent _alunoAgent;
    private readonly TreinadorAssistantAgent _treinadorAgent;

    public AgentRegistry(AlunoAssistantAgent alunoAgent, TreinadorAssistantAgent treinadorAgent)
    {
        _alunoAgent = alunoAgent;
        _treinadorAgent = treinadorAgent;
    }

    // Sem método genérico — força classificação explícita, evita agentes shadow
    public ForzionAgent GetAlunoAssistant(Guid alunoId) => _alunoAgent.Build(alunoId);
    public ForzionAgent GetTreinadorAssistant(Guid treinadorId) => _treinadorAgent.Build(treinadorId);
}
