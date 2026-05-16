# Integração IA — Status de Implementação

Branch: `feat/integracao-ia`  
Framework: Microsoft Agent Framework (MAF) 1.6.1 + Microsoft.Extensions.AI 10.6.0  
Provider LLM: OpenAI SDK (interim) — DC-001 define Claude Haiku 4.5 via Anthropic API para produção  
Data de referência: 2026-05-16

---

## Arquitetura implementada

Dois agentes estritamente separados, sem compartilhamento de tools:

| Agente | Tipo | Endpoint | Tools |
|--------|------|----------|-------|
| AlunoAssistant | Interno, read-only | `POST /aluno/assistant/chat` | 4 tools (treinos, execuções, exercício) |
| TreinadorAssistant | Interno, read + write | `POST /treinador/assistant/chat` | 4R + 1W (+ `/apply-suggestion`) |

Pipeline de defesa em cada endpoint:
`Unicode normalize → Token budget check → Injection detect → Agent run (60s timeout) → Output scan → Markdown sanitize → Budget commit`

---

## O que foi implementado

### Projeto `forzion.tech.AI` (novo classlib)

| Arquivo | Descrição |
|---------|-----------|
| `forzion.tech.AI.csproj` | Projeto net8.0 com MAF 1.6.1, OpenAI 2.*, MEAI.OpenAI 10.*, Config.Abstractions 10.* |
| `GuardRails/InputNormalizer.cs` | Remove unicode tag chars (U+E0000-U+E007F), zero-width, NFC normalize, estimativa de tokens |
| `GuardRails/PromptInjectionPatterns.cs` | 8 regex: override clássico, role injection, DAN, system prompt leak, delimiter spoofing, base64 longa |
| `GuardRails/ToolResponseSanitizer.cs` | Trunca >10k chars, wrappa em `<external_data>`, remove URLs de exfiltração |
| `GuardRails/ToolArgValidators.cs` | `ValidateGuidArg`, `ValidateIntArg`, `CheckStringArgsForInjection` |
| `GuardRails/OutputScanner.cs` | Detecta CPF, CNPJ, cartão, API keys (sk-, Bearer), imagens markdown de exfiltração |
| `GuardRails/OutputSanitizer.cs` | Remove imagens markdown externas, HTML inline |
| `GuardRails/ITokenBudget.cs` + `InMemoryTokenBudget.cs` | Budget diário por usuário/tipo: Aluno=50k tokens, Treinador=100k. ConcurrentDictionary. Sprint 4: migrar para PostgreSQL |
| `Clients/IChatClientFactory.cs` + `ChatClientFactory.cs` | Provider-agnostic via MEAI `IChatClient`. Suporta endpoint customizável (para troca de provider por config) |
| `Agents/ForzionAgent.cs` | Record: `IChatClient + SystemPrompt + Temperature + MaxOutputTokens + Tools` |
| `Agents/AlunoAssistantAgent.cs` | Tools em closure sobre `alunoId`. Temperature=0.3, MaxOutputTokens=800 |
| `Agents/TreinadorAssistantAgent.cs` | Tools em closure sobre `treinadorId`. Temperature=0.3, MaxOutputTokens=1200 |
| `Agents/AgentRegistry.cs` | `GetAlunoAssistant(Guid)` e `GetTreinadorAssistant(Guid)`. Sem método genérico — força classificação explícita |
| `Tools/AlunoTools.cs` | `get_meus_treinos`, `get_historico_execucoes`, `get_proximo_treino`, `get_detalhe_exercicio` |
| `Tools/TreinadorTools.cs` | `get_meus_alunos`, `get_progresso_aluno`*, `get_fichas_aluno`*, `get_execucoes_recentes_aluno`*, `sugerir_ficha_treino`* |
| `Configuration/AiExtensions.cs` | `AddForzionAI()` — registra todos os serviços de IA no DI |

*Todas as tools do Treinador que recebem `alunoId` validam `IVinculoTreinadorAlunoRepository.ObterAtivoAsync(treinadorId, alunoGuid)` antes de retornar dados — isolamento cross-tenant.

### Projeto `forzion.tech.Api` (modificado)

| Arquivo | Alteração |
|---------|-----------|
| `forzion.tech.Api.csproj` | +Microsoft.Extensions.AI 10.*, +4 pacotes OpenTelemetry |
| `Configuration/OpenTelemetryExtensions.cs` | `AddForzionOpenTelemetry()` — OTel com sources MAF + MEAI, OTLP exporter condicional |
| `Endpoints/AlunoAssistantEndpoints.cs` | Endpoint `POST /aluno/assistant/chat` com pipeline completo de guard rails |
| `Endpoints/TreinadorAssistantEndpoints.cs` | Endpoint `POST /treinador/assistant/chat` + `POST /treinador/assistant/apply-suggestion` (approval flow) |
| `Extensions/DependencyInjectionExtensions.cs` | +`AddForzionAI()`, +`AddForzionOpenTelemetry()`, +rate limits `agent-aluno` (20/h) e `agent-treinador` (10/h) |
| `Extensions/RouteBuilderExtensions.cs` | +`MapAlunoAssistantEndpoints()`, +`MapTreinadorAssistantEndpoints()` |
| `.env.example` | +variáveis AI: model, API key placeholder, token budgets, OTEL endpoint |

### Memória de segurança

| Arquivo | Descrição |
|---------|-----------|
| `.agent-security-memory.md` | Registro de agentes, decisões arquiteturais (DC-001, DC-002), achados de segurança (F1–F9), histórico de revisão MAF |

---

## Decisões técnicas registradas

### DC-001 — Provider LLM
- **Decisão:** Claude Haiku 4.5 (`claude-haiku-4-5-20251001`) via Anthropic API  
- **Implementação atual:** OpenAI SDK como provider interim (MEAI-compatible via `AsIChatClient()`)  
- **Troca:** alterar `AI__Internal__ApiKey`, `AI__Internal__Model` e `AI__Internal__Endpoint` — sem mudança de código  
- **LGPD Art. 33:** dados de treino do aluno (peso, altura) vão ao LLM externo. Documentação e opt-out Anthropic obrigatórios antes de produção. Não incluir CPF, nome completo, dados clínicos no contexto

### DC-002 — Sem agente externo
- **Decisão:** escopo atual não inclui pesquisa web/LLM externa. Ambos os agentes são internos  
- **Se mudar:** seguir padrão `architecture-patterns.md` — agente externo em controller separado, zero tools internas

---

## O que ainda falta implementar

### Crítico (bloqueia produção)

| # | Item | Detalhe |
|---|------|---------|
| T10 | **Configurar credenciais** | `AI__Internal__ApiKey` via User Secrets (dev) e Azure Key Vault / variável de ambiente (prod). Nunca commitado. Ver `.env.example` |
| T15 | **Threat model agente-aluno** | Documento `docs/threat-model-agente-aluno.md` usando template `assets/threat-model-template.md` |
| T21 | **Threat model agente-treinador** | Documento `docs/threat-model-agente-treinador.md` |
| DC-001 LGPD | **Documentação LGPD** | Registro formal de transferência internacional de dados (Art. 33 LGPD), verificação opt-out Anthropic |
| T19 (parcial) | **apply-suggestion handler** | `POST /treinador/assistant/apply-suggestion` registra o draft mas o `TODO` de criação real da ficha de treino via handler ainda não foi conectado |

### Importante (sprint seguinte)

| # | Item | Detalhe |
|---|------|---------|
| T16 | **Testes unitários — guard rails** | `InputNormalizer`, `PromptInjectionPatterns`, `OutputScanner`, `ToolResponseSanitizer` |
| T16 | **Testes unitários — tools** | `AlunoTools` e `TreinadorTools` com mocks dos repositories, especialmente validação de vínculo cross-tenant |
| T22 | **ITokenBudget → PostgreSQL** | `InMemoryTokenBudget` não persiste entre restarts, não funciona em múltiplas instâncias. Migrar para tabela `ai_token_usage` |
| T23 | **Alertas OTel** | Dashboards e alertas para `gen_ai.client.token.usage`, latência p95, erros de tool calls |
| T24 | **Testes adversariais em CI** | Suite básica de prompt injection (override instructions, role injection, delimiter spoofing) que roda no pipeline |

### Hardening pós-Sprint 4

| # | Item | Detalhe |
|---|------|---------|
| T25 | **Revisão de segurança pré-produção** | Re-execução completa do `maf-agent-guardian` com código final antes de merge para main |
| — | **Topic boundary check** | Classificador (embedding ou LLM call curto) para validar que pergunta do aluno é sobre treinos. Opcional mas recomendado |
| — | **PII redaction** | Se provider mudar para hosted externo com processamento de dados — redaction de CPF/email antes de enviar ao LLM |
| — | **Conversation history** | Endpoints atuais são stateless (apenas 1 turno). Se multi-turn for adicionado: gerenciar histórico com TTL, criptografia em repouso, expurgo |
| — | **Migrar para Anthropic SDK** | Quando `Microsoft.Extensions.AI.Anthropic` (ou equivalente MEAI-compatible) estiver disponível, trocar provider via config |

---

## Notas de compatibilidade MEAI 10.x

MEAI 10.6.0 (GA com .NET 10) introduziu breaking changes vs. 9.x:

| MEAI 9.x | MEAI 10.x |
|-----------|-----------|
| `ChatCompletion` | `ChatResponse` |
| `IChatClient.CompleteAsync()` | `IChatClient.GetResponseAsync()` |
| `completion.Message.Text` | `response.Text` |
| `completion.Usage?.TotalTokenCount` (int?) | `response.Usage?.TotalTokenCount` (long?) |
| `IChatClient.CompleteStreamingAsync()` | `IChatClient.GetStreamingResponseAsync()` |

Código do projeto já usa a API 10.x.

---

## Como configurar para rodar localmente

```bash
# 1. Configurar User Secrets (nunca commitado)
dotnet user-secrets set "AI:Internal:ApiKey" "sk-..." --project forzion.tech.Api
dotnet user-secrets set "AI:Internal:Model" "gpt-4o-mini" --project forzion.tech.Api
# Para Claude Haiku via Anthropic (quando integração MEAI estiver disponível):
# dotnet user-secrets set "AI:Internal:Endpoint" "https://api.anthropic.com/v1/"
# dotnet user-secrets set "AI:Internal:Model" "claude-haiku-4-5-20251001"

# 2. Build
dotnet build

# 3. Run
dotnet run --project forzion.tech.Api
```
