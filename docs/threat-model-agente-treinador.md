# Threat Model — TreinadorAssistant

**Versão:** 1.0  
**Data:** 2026-05-16  
**Agente:** TreinadorAssistant  
**Endpoints:** `POST /treinador/assistant/chat` · `POST /treinador/assistant/apply-suggestion`  
**Auth:** `[Authorize("Treinador")]` — JWT claim `tipo_conta=Treinador`, `perfil_id=<treinadorId>`  
**Modelo:** `claude-haiku-4-5-20251001` / Temperature: 0.3 / MaxOutputTokens: 1200  

---

## 1. Escopo e Superfície de Ataque

O agente permite ao treinador autenticado consultar dados de alunos vinculados e gerar sugestões de fichas. A tool `sugerir_ficha_treino` é **Write-tier** mas não persiste diretamente — requer aprovação explícita via `apply-suggestion`.

```
Internet
  └─ HTTPS
      ├─ POST /treinador/assistant/chat      [JWT obrigatório]
      │    └─ Pipeline guard rails
      │        └─ TreinadorAssistant (LLM)
      │            └─ Tools (5): 4 read-only + 1 write-draft
      │                └─ IVinculoTreinadorAlunoRepository (validação cross-tenant)
      │                └─ Database (PostgreSQL)
      │
      └─ POST /treinador/assistant/apply-suggestion  [JWT obrigatório]
           └─ IDraftSuggestionService (validate ownership + TTL)
           └─ CriarTreinoHandler → Database
```

**Atores:**
| Ator | Acesso esperado | Nível de confiança |
|------|-----------------|--------------------|
| Treinador autenticado | Dados de seus alunos vinculados | Baixo (usuário externo) |
| Treinador A tentando ver aluno de B | Nenhum | Não confiável |
| Aluno tentando acessar endpoint | Bloqueado por auth policy | Não confiável |
| Atacante externo sem JWT | Nenhum | Não confiável |

---

## 2. Assets a Proteger

| Asset | Criticidade | Localização |
|-------|-------------|-------------|
| Dados de alunos (progresso, fichas, execuções) | Alta | PostgreSQL |
| Vínculo treinador↔aluno | Alta | `VinculoTreinadorAluno` entity |
| Draft de sugestão de ficha | Média | `InMemoryDraftSuggestionService` |
| Capacidade de criar fichas | Alta | `CriarTreinoHandler` via `apply-suggestion` |
| System prompt / instruções | Média | Memória da requisição |
| API key do LLM | Crítica | User Secrets / env var |

---

## 3. Threat Enumeration (STRIDE)

### T1 — Spoofing: acesso a dados de aluno de outro treinador (cross-tenant)
- **Vetor:** Treinador A injeta `alunoId` de aluno do Treinador B no prompt
- **Mitigação implementada:** **Todas** as 4 tools que aceitam `alunoId` chamam `_vinculo.ObterAtivoAsync(treinadorId, alunoGuid)` antes de retornar dados. `treinadorId` vem exclusivamente do JWT
- **Mitigação implementada:** `sugerir_ficha_treino` também valida vínculo antes de gerar draft
- **Residual:** Baixo — vínculo validado por código + DB, não só por auth

### T2 — Tampering: criação não autorizada de fichas
- **Vetor:** LLM cria ficha diretamente sem aprovação do treinador
- **Mitigação implementada:** `sugerir_ficha_treino` é write-draft — não chama `CriarTreinoHandler`. Persistência só ocorre via `apply-suggestion` com `draftId` explícito aprovado pelo treinador
- **Mitigação implementada:** `InMemoryDraftSuggestionService.GetDraft()` valida ownership (`TreinadorId == treinadorId from JWT`) + TTL (10 min) antes de permitir criação
- **Residual:** Baixo — aprovação humana obrigatória no path de escrita

### T3 — Tampering: aplicar draft de outro treinador
- **Vetor:** Treinador A obtém `draftId` gerado pelo Treinador B e chama `apply-suggestion`
- **Mitigação implementada:** `IDraftSuggestionService.GetDraft(draftId, treinadorId)` verifica `draft.TreinadorId == treinadorId` — retorna null se mismatch → 404
- **Residual:** Nenhum — ownership hardcoded no draft com JWT do gerador

### T4 — Repudiation: criação de ficha sem rastreio
- **Mitigação implementada:** `logger.LogInformation("ApplySuggestion TreinadorId={TId} DraftId={DId} TreinoId={TrId}")`. `CriarTreinoHandler` também loga criação do treino
- **Residual:** Médio — logs não imutáveis sem SIEM

### T5 — Information Disclosure: dados de alunos no output
- **Mitigação implementada:** `OutputScanner` (CPF, CNPJ, cartão, API key, pixel tracking). `OutputSanitizer.SanitizeMarkdown()`
- **Mitigação implementada:** Tool responses encapsuladas em `<external_data>` via `ToolResponseSanitizer`
- **Residual:** Médio — dados de treino (carga, séries) podem aparecer legitimamente; não são detectados como PII

### T6 — Denial of Service: esgotamento de quota
- **Vetor:** Rate 10 req/hora + token budget 100k/dia
- **Mitigação implementada:** Rate limit `agent-treinador` (10/h). `InMemoryTokenBudget` 100k/dia/treinador. Timeout 60s
- **Residual:** Médio — token budget in-memory (T22 pendente)

### T7 — Elevation of Privilege: prompt injection via dados
- **Vetor:** Nome de aluno ou observação de exercício contém instrução maliciosa
- **Mitigação implementada:** `ToolResponseSanitizer` encapsula respostas em `<external_data>`. System prompt instrui a tratar como dado
- **Residual:** Médio — mesma limitação do agente Aluno

### T8 — Draft TTL bypass
- **Vetor:** Draft expira mas atacante tenta aplicá-lo após 10 min
- **Mitigação implementada:** `InMemoryDraftSuggestionService.GetDraft()` verifica `draft.ExpiresAt < DateTime.UtcNow` → remove e retorna null → 404
- **Residual:** Baixo — TTL enforce é server-side

### T9 — Replay de draft
- **Vetor:** Treinador tenta aplicar o mesmo draft duas vezes (criar 2 fichas)
- **Mitigação implementada:** `draftService.RemoveDraft(req.DraftId)` imediatamente após criação bem-sucedida. Segunda chamada → 404
- **Residual:** Nenhum — one-shot by design

---

## 4. Análise da Tool Write: `sugerir_ficha_treino`

Esta é a única tool write do sistema. Análise completa do fluxo:

```
1. Treinador pede sugestão de ficha
2. LLM chama sugerir_ficha_treino(alunoId, objetivo, dificuldade, numeroDeTreinos)
3. Tool valida vínculo ativo (→ erro se inválido)
4. Tool cria SugestaoDraft e armazena em IDraftSuggestionService (TTL 10min)
5. Tool sinaliza IDraftRequestTracker.PendingDraftId (shared scope)
6. Tool retorna texto descritivo ao LLM (SEM JSON, SEM markers no output)
7. LLM gera response ao usuário explicando a sugestão
8. Endpoint detecta PendingDraftId via IDraftRequestTracker (não via parsing de texto)
9. Response ao cliente inclui {pendingApproval: true, draftId: <guid>}
10. Cliente exibe sugestão → Treinador clica "confirmar"
11. POST /treinador/assistant/apply-suggestion {draftId}
12. Endpoint valida ownership + TTL via IDraftSuggestionService
13. CriarTreinoHandler persiste ficha + vínculo TreinoAluno
14. Draft removido imediatamente
```

**Pontos críticos:**
- Passo 4: vínculo validado antes de qualquer draft ser criado
- Passo 8: detecção por shared scope (não parsing LLM) — elimina fragilidade anterior
- Passo 12: double-check de ownership no momento da persistência

---

## 5. Mitigações por Camada

| Camada | Controle | Implementação |
|--------|----------|---------------|
| **Network** | TLS obrigatório | HTTPS |
| **Auth** | JWT obrigatório | `[Authorize("Treinador")]` + `perfil_id` |
| **Auth** | Tenant isolation | Vínculo validado em cada tool call |
| **Rate limit** | 10 req/hora/user | `agent-treinador` FixedWindowLimiter |
| **Input** | Unicode normalize | `InputNormalizer.NormalizeUnicode()` |
| **Input** | Token budget | `budget.WouldExceedDailyAsync()` 100k/dia |
| **Input** | Injection detection | `PromptInjectionPatterns.Check()` (log) |
| **Tool** | Scope isolation | `treinadorId` closure = JWT |
| **Tool** | Cross-tenant block | `ObterAtivoAsync()` em todas as tools com alunoId |
| **Tool** | Write-draft only | `sugerir_ficha_treino` nunca persiste |
| **Tool** | Tool response wrap | `ToolResponseSanitizer` |
| **Draft** | Ownership check | `draft.TreinadorId == JWT treinadorId` |
| **Draft** | TTL enforce | 10 min, server-side |
| **Draft** | One-shot apply | Remove imediatamente após criação |
| **Output** | PII scan | `OutputScanner.Scan()` |
| **Output** | Markdown sanitize | `OutputSanitizer.SanitizeMarkdown()` |

---

## 6. Riscos Residuais Aceitos

| Risco | Justificativa |
|-------|---------------|
| Injection detection não bloqueia | Mesmo racional do agente Aluno — usuário autenticado |
| `InMemoryDraftSuggestionService` não persiste | Drafts perdidos em restart — aceitável; treinador pode pedir nova sugestão |
| Dados de treino no output não detectados como PII | Carga, séries são dados de negócio, não dados pessoais sensíveis per se |
| Dados físicos de alunos ao LLM externo | DC-001 / LGPD Art. 33 documentado em `docs/lgpd-transferencia-internacional.md` |

---

## 7. Pendências

| ID | Item | Prioridade |
|----|------|-----------|
| T22 | `InMemoryDraftSuggestionService` + `InMemoryTokenBudget` → PostgreSQL | Sprint 4 |
| T24 | Testes adversariais: cross-tenant attempt, write sem approval, draft replay | Sprint 4 |
| — | Distributed cache para drafts em deploy multi-instância | Futuro |
