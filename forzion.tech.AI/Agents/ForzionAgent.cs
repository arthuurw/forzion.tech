using Microsoft.Extensions.AI;

namespace forzion.tech.AI.Agents;

// Wrapper que encapsula o IChatClient + configuração do agente
// Evita expor ChatOptions internamente nos endpoints
public sealed record ForzionAgent(
    IChatClient Client,
    string SystemPrompt,
    float Temperature,
    int MaxOutputTokens,
    IList<AITool> Tools);
