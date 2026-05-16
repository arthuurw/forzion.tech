# Integração IA — Status de Implementação

**Branch:** `feat/integracao-ia`  
**Framework:** MAF 1.6.1 + Microsoft.Extensions.AI 10.6.0  
**Provider:** OpenAI SDK (interim) — DC-001: Claude Haiku 4.5 via Anthropic API em produção  
**Build:** ✅ 0 erros · 0 warnings  
**Testes AI:** ✅ 109/109 (guard rails 60+38=98, tools 7, endpoints 4)  
**Testes total:** ✅ 702/702  
**Última atualização:** 2026-05-16

---

## Progresso por Sprint

| Sprint | Escopo | Status |
|--------|--------|--------|
| Sprint 1 — Infraestrutura | Projeto AI, guard rails, DI, OTel, env vars | ✅ Completo |
| Sprint 2 — Agente Aluno | AlunoTools, AlunoAssistantAgent, endpoint `/aluno/assistant/chat` | ✅ Completo |
| Sprint 3 — Agente Treinador | TreinadorTools, TreinadorAssistantAgent, endpoint `/treinador/assistant/chat`, approval flow | ✅ Completo |
| Sprint 4 — Hardening | Token budget PostgreSQL, testes adversariais, OTel alertas, revisão de segurança | 🔲 Não iniciado |

---

## Arquitetura

Dois agentes internos estritamente separados — sem compartilhamento de tools ou pipeline:

```
POST /aluno/assistant/chat        [Authorize("Aluno")]    rate: 20/h/user
  └─ AlunoAssistant (read-only)
       └─ get_meus_treinos · get_historico_execucoes · get_proximo_treino · get_detalhe_exercicio

POST /treinador/assistant/chat    [Authorize("Treinador")]  rate: 10/h/user
  └─ TreinadorAssistant (read + write draft)
       └─ get_meus_alunos · get_progresso_aluno · get_fichas_aluno · get_execucoes_recentes_aluno
          · sugerir_ficha_treino → retorna preview JSON, nunca persiste

POST /treinador/assistant/apply-suggestion   confirma draft → chama CriarTreinoHandler
```

Pipeline em ambos os endpoints:
```
Request → Unicode normalize → Token budget check → Injection detect (log) →
Agent.GetResponseAsync (60s timeout) → OutputScanner → SanitizeMarkdown → Budget commit → Response
```

---

## O que foi implementado

### `forzion.tech.AI` — novo classlib (net8.0)

**Guard Rails**

| Arquivo | O que faz |
|---------|-----------|
| `GuardRails/InputNormalizer.cs` | Remove unicode tag chars (U+E0000–U+E007F) e zero-width; NFC normalize; estimativa de tokens (`length / 4`) |
| `GuardRails/PromptInjectionPatterns.cs` | 8 regex: override clássico, role injection, DAN, system prompt leak, delimiter spoofing, base64 longa (>200 chars) |
| `GuardRails/ToolResponseSanitizer.cs` | Trunca >10k chars, encapsula em `<external_data>`, remove URLs de exfiltração |
| `GuardRails/ToolArgValidators.cs` | `ValidateGuidArg`, `ValidateIntArg(min,max)`, `CheckStringArgsForInjection` |
| `GuardRails/OutputScanner.cs` | Detecta CPF, CNPJ, número de cartão, API keys (`sk-`, `Bearer`), pixel tracking via markdown image |
| `GuardRails/OutputSanitizer.cs` | Remove imagens markdown externas e HTML inline do output do LLM |
| `GuardRails/ITokenBudget.cs` | Interface: `WouldExceedDailyAsync`, `CommitAsync`, `GetDailyUsageAsync` — enum `AgentType { Aluno, Treinador }` |
| `GuardRails/InMemoryTokenBudget.cs` | `ConcurrentDictionary` keyed por `userId:agentType:date`. Limite: 50k/dia Aluno · 100k/dia Treinador (config) |
| `GuardRails/SugestaoDraft.cs` | Record imutável com TTL: `TreinadorId, AlunoId, Objetivo, Dificuldade, NumeroDeTreinos, ExpiresAt` |
| `GuardRails/IDraftSuggestionService.cs` | Singleton: `StoreDraft → Guid`, `GetDraft(id, treinadorId) → null se expirado/ownership errado`, `RemoveDraft` |
| `GuardRails/InMemoryDraftSuggestionService.cs` | `ConcurrentDictionary<Guid, SugestaoDraft>` com verificação de ownership e TTL. Purge automático em cada `StoreDraft` |
| `GuardRails/IDraftRequestTracker.cs` + `DraftRequestTracker` | Scoped — sinaliza tool→endpoint no mesmo request via `PendingDraftId`/`PendingDraftExpiresAt`. Elimina parsing frágil de JSON do LLM |

**Clients / Agents**

| Arquivo | O que faz |
|---------|-----------|
| `Clients/IChatClientFactory.cs` + `ChatClientFactory.cs` | Provider-agnostic via `IChatClient`. Lê `AI:Internal:ApiKey`, `AI:Internal:Model`, `AI:Internal:Endpoint` (opcional). Trocar provider = trocar config, sem mudar código |
| `Agents/ForzionAgent.cs` | Record: `IChatClient + SystemPrompt + Temperature + MaxOutputTokens + Tools` — separa system prompt das `ChatOptions` (MEAI 10 não tem `ChatOptions.SystemMessage`) |
| `Agents/AlunoAssistantAgent.cs` | `Build(Guid alunoId)` → tools em closure sobre `alunoId`. Temperature=0.3, MaxOutputTokens=800 |
| `Agents/TreinadorAssistantAgent.cs` | `Build(Guid treinadorId)` → tools em closure sobre `treinadorId`. Temperature=0.3, MaxOutputTokens=1200 |
| `Agents/AgentRegistry.cs` | `GetAlunoAssistant(Guid)` · `GetTreinadorAssistant(Guid)` — sem `GetAgent()` genérico, forçando classificação explícita |

**Tools**

| Arquivo | Tools expostas |
|---------|---------------|
| `Tools/AlunoTools.cs` | `get_meus_treinos` (sem params) · `get_historico_execucoes(int ultimas, max=20)` · `get_proximo_treino` · `get_detalhe_exercicio(string exercicioId)` |
| `Tools/TreinadorTools.cs` | `get_meus_alunos` · `get_progresso_aluno(alunoId, ultimas)` · `get_fichas_aluno(alunoId)` · `get_execucoes_recentes_aluno(alunoId, ultimas)` · `sugerir_ficha_treino(alunoId, objetivo, dificuldade, numeroDeTreinos)` |

⚠️ Todas as tools do Treinador que aceitam `alunoId` chamam `IVinculoTreinadorAlunoRepository.ObterAtivoAsync(treinadorId, alunoGuid)` antes de retornar dados. Sem vínculo ativo → retorna "aluno não encontrado". Isolamento cross-tenant por código, não só por autenticação.

`sugerir_ficha_treino` é tier Write: armazena `SugestaoDraft` via `IDraftSuggestionService` (singleton) e sinaliza `IDraftRequestTracker.PendingDraftId` (scoped) para o endpoint devolver `pendingApproval: true`. Nunca persiste diretamente — requer aprovação via `apply-suggestion`.

**Configuração**

| Arquivo | O que faz |
|---------|-----------|
| `Configuration/AiExtensions.cs` | `AddForzionAI()` — registra `IChatClientFactory` (singleton), `ITokenBudget` (singleton), `IDraftSuggestionService` (singleton), `IDraftRequestTracker` (scoped), `AlunoTools` + `TreinadorTools` (scoped), `AlunoAssistantAgent` + `TreinadorAssistantAgent` (scoped), `AgentRegistry` (scoped) |

---

### `forzion.tech.Api` — modificações

| Arquivo | Alteração |
|---------|-----------|
| `forzion.tech.Api.csproj` | +`Microsoft.Extensions.AI 10.*` · +4 pacotes OpenTelemetry |
| `Configuration/OpenTelemetryExtensions.cs` | `AddForzionOpenTelemetry()` — sources `Microsoft.Agents.AI` e `Microsoft.Extensions.AI`, OTLP exporter condicional por `OTEL_EXPORTER_OTLP_ENDPOINT` |
| `Endpoints/AlunoAssistantEndpoints.cs` | `POST /aluno/assistant/chat` com pipeline completo de guard rails |
| `Endpoints/TreinadorAssistantEndpoints.cs` | `POST /treinador/assistant/chat` (pipeline) + `POST /treinador/assistant/apply-suggestion` (approval: valida ownership + TTL 10 min, remove draft) |
| `Extensions/DependencyInjectionExtensions.cs` | +`AddForzionAI()` · +`AddForzionOpenTelemetry()` · +rate limits `agent-aluno` (20/h) e `agent-treinador` (10/h) |
| `Extensions/RouteBuilderExtensions.cs` | +`MapAlunoAssistantEndpoints()` · +`MapTreinadorAssistantEndpoints()` |
| `.env.example` | +`AI__Internal__Model`, `AI__Internal__ApiKey` (placeholder), `AI__TokenBudget__*`, `OTEL_EXPORTER_OTLP_ENDPOINT` |

---

### Memória e documentação de segurança

| Arquivo | Conteúdo |
|---------|----------|
| `.agent-security-memory.md` | Agentes registrados · DC-001 (provider + LGPD) · DC-002 (sem agente externo) · achados F1–F9 por severidade · histórico de revisão MAF |
| `docs/ai-integration-status.md` | Este documento |

---

## Decisões arquiteturais

### DC-001 — Provider LLM
- **Decisão final:** Claude Haiku 4.5 (`claude-haiku-4-5-20251001`) via Anthropic API  
- **Estado atual:** OpenAI SDK como provider interim — `ChatClientFactory` usa `OpenAIClient.GetChatClient(model).AsIChatClient()`  
- **Troca de provider:** alterar apenas `AI__Internal__ApiKey`, `AI__Internal__Model`, `AI__Internal__Endpoint` — zero mudança de código  
- **Bloqueio LGPD:** dados físicos do aluno (peso, altura, carga) vão ao LLM externo → Art. 33 LGPD exige documentação de transferência internacional + verificação de opt-out Anthropic **antes de qualquer deploy em produção**. Não incluir CPF, nome completo, dados clínicos no contexto

### DC-002 — Sem agente externo
- **Decisão:** escopo atual é apenas agentes internos (dados da própria aplicação). Nenhum agente faz pesquisa web ou chama LLM de terceiros como tool  
- **Se o escopo mudar:** agente externo em controller separado, com zero tools internas, seguindo `architecture-patterns.md`

---

## O que foi concluído nesta sessão

| Task | Item | Status |
|------|------|--------|
| T19 | Conectar `apply-suggestion` ao `CriarTreinoHandler` | ✅ Implementado — `IDraftSuggestionService` + `IDraftRequestTracker`, `ParseObjetivo`/`ParseDificuldade`, handler real com propriedade/TTL |
| T15 | Threat model — Agente Aluno | ✅ `docs/threat-model-agente-aluno.md` — STRIDE T1-T8, mitigações por camada |
| T21 | Threat model — Agente Treinador | ✅ `docs/threat-model-agente-treinador.md` — STRIDE T1-T9, approval flow 10 passos, análise tier Write |
| DC-001 | Documentação LGPD Art. 33 | ✅ `docs/lgpd-transferencia-internacional.md` — tabela permitido/proibido, base legal, checklist pré-produção |
| T16 | Testes unitários — guard rails | ✅ 60 testes: `InputNormalizer`, `PromptInjectionPatterns`, `OutputScanner`, `OutputSanitizer`, `ToolResponseSanitizer`, `InMemoryDraftSuggestionService` |
| T16 | Testes unitários — tools | ✅ 7 testes: `TreinadorToolsTests` — cross-tenant, args inválidos, draft storage, clamp |
| T22b | Testes de integração — endpoints | ✅ 4 testes: `TreinadorAssistantEndpointsTests` — 401 sem auth, 404 draft ausente, 200 draft válido, mapeamento objetivo/dificuldade |
| Bug | Bug `InputNormalizer` — regex com invisible chars | ✅ Corrigido — substituído por `@"\uDB40[\uDC00-\uDC7F]"` literal (regex anterior stripava texto acentuado) |

---

## O que ainda falta

### 🔴 Crítico — bloqueia qualquer teste real

| Task | Item | Arquivo afetado | Detalhe |
|------|------|-----------------|---------|
| T10 | **Credenciais de API** | User Secrets / Key Vault | `AI__Internal__ApiKey` nunca commitado. Sem isso os endpoints lançam `InvalidOperationException` na inicialização. Configurar via `dotnet user-secrets set` em dev, variável de ambiente em prod |

### 🟡 Médio — Sprint 4

| Task | Item | Detalhe |
|------|------|---------|
| T22 | **ITokenBudget → PostgreSQL** | `InMemoryTokenBudget` não persiste entre restarts e não funciona em múltiplas instâncias. Migrar para tabela `ai_token_usage (user_id, agent_type, date, token_count)` |
| T23 | **Alertas e dashboards OTel** | Criar dashboards em Grafana/Azure Monitor para `gen_ai.client.token.usage`, latência p95, erros de tool calls, taxa de injection detectada |
| T24 | **Testes adversariais** | ✅ 38 casos: unicode tag/ZW/BOM, case variants, delimiter spoofing, base64 payload, role injection, DAN, prompt leak, tool exfiltração, PII em output, multi-vector pipeline — `forzion.tech.Tests/AI/GuardRails/AdversarialTests.cs`. Fix colateral: 2 gaps em `PromptInjectionPatterns.cs` (`DoAnythingNow` camelCase + "show me the instructions") |
| T25 | **Revisão de segurança pré-produção** | Re-execução do `/ultrareview` completo sobre o código final antes de merge para `main` (user-triggered) |

### 🟢 Opcional / Futuro

| Item | Detalhe |
|------|---------|
| **Topic boundary check** | Classificador que valida se a pergunta é sobre treinos antes de chamar o agente. Opções: embedding similarity local ou LLM pre-call curto |
| **Conversation history (multi-turn)** | Endpoints atuais são stateless (1 turno por request). Se multi-turn for adicionado: histórico com TTL, criptografia em repouso, expurgo por sessão |
| **PII redaction automática** | Se os dados enviados ao LLM expandirem, adicionar `PiiRedactor` antes de `GetResponseAsync`. Regex para CPF, CNPJ, email, cartão já implementada em `input-guardrails.md` |
| **Trocar para SDK nativo Anthropic** | Quando um adapter MEAI-compatible para Anthropic estiver disponível (e.g., `Microsoft.Extensions.AI.Anthropic`), trocar via config sem mudar código |

---

## Compatibilidade MEAI 10.x — breaking changes relevantes

MEAI 10.6.0 renomeou APIs vs. 9.x. O projeto já usa a nomenclatura nova:

| MEAI 9.x (antigo) | MEAI 10.x (atual no projeto) |
|-------------------|------------------------------|
| `ChatCompletion` | `ChatResponse` |
| `IChatClient.CompleteAsync()` | `IChatClient.GetResponseAsync()` |
| `completion.Message.Text` | `response.Text` |
| `TotalTokenCount` → `int?` | `TotalTokenCount` → `long?` (cast para `int` nos endpoints) |
| `CompleteStreamingAsync()` | `GetStreamingResponseAsync()` |

---

## Como configurar localmente

```bash
# 1. Credenciais via User Secrets — NUNCA commitar
dotnet user-secrets set "AI:Internal:ApiKey" "sk-sua-chave" --project forzion.tech.Api
dotnet user-secrets set "AI:Internal:Model"  "gpt-4o-mini"  --project forzion.tech.Api
# Quando usar Anthropic diretamente:
# dotnet user-secrets set "AI:Internal:Endpoint" "https://api.anthropic.com/v1/"
# dotnet user-secrets set "AI:Internal:Model"    "claude-haiku-4-5-20251001"

# 2. Build e run
dotnet build
dotnet run --project forzion.tech.Api
```

Token budgets e endpoint OTLP são opcionais — têm defaults em código se ausentes das configs.
