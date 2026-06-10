# specification-performance — performance backend (forzion.tech)

DOC PARA AGENTES. Armadilhas de PERFORMANCE no backend .NET/EF Core/PG que reviews pegam tarde (só sob carga). Perf de frontend (Web Vitals/Lighthouse/perf budgets) → [specification-observability]. Ler antes de query nova, endpoint de listagem, ou handler com I/O. Formato denso. ENFORCEMENT É FRACO hoje (poucos gates) — marcado por item; maioria é disciplina + revisão de diff.

## MANUTENÇÃO
- Atualizar quando review pegar classe nova de gargalo (incidente real). Vive em `specs/` (commitado). NÃO duplicar índices/schema de [specification-db] — referenciar.

## 1. EF CORE — ARMADILHAS
- **N+1** — `Include` (ou projeção `Select` p/ DTO) p/ carregar relação navegada em loop; NUNCA materializar e navegar por filho dentro de iteração. Caso óbvio do domínio: listar `treinos` + `treino_exercicios` + `treino_exercicio_series` (composição 3 níveis, CASCADE) — uma query com projeção, não N consultas por exercício. [disciplina; detectável por contar SQL em teste de integração — §5]
- **`AsNoTracking` em leitura read-only** — query que NÃO vai mutar+`CommitAsync` não precisa de change-tracker. Default tracking desperdiça memória/CPU em listagem (read model `assinantes`, catálogos `grupos_musculares`/`exercicios` globais). [disciplina]
- **Projeção > entidade inteira** — `Select` só as colunas usadas; não materializar agregado completo p/ devolver 3 campos.
- **Filtro no banco, não em memória** — `Where` antes de `ToList()`; NUNCA `ToList().Where(...)` (puxa tabela inteira e filtra no app).

## 2. PAGINAÇÃO
- **OBRIGATÓRIA + cap** em todo endpoint de coleção (`skip`/`take` com teto, ex. max 100). Sem cap = scan de tabela inteira sob crescimento (alunos por treinador, pagamentos por assinatura, execuções por aluno). [disciplina — propor gate: teste de contrato rejeita endpoint de lista sem paginação]
- Ordenação estável (coluna determinística, ex. `created_at`+`id`) p/ paginação consistente.

## 3. CONEXÃO / DBCONTEXT
- `DbContext` scoped (1 por request); NUNCA capturar em singleton/campo de hosted service sem escopo próprio. Worker de outbox / hosted services (`LimparTokensRevogadosService`, renovações) criam escopo por iteração.
- Pool de conexão PG (Supabase) respeita o limite do tier; vazamento = `DbContext`/conexão longo-vivo sem dispose. Host Supabase direto é IPv6-only ([specification-db] §DICAS) — não é questão de perf mas de conectividade.

## 4. ASYNC & CANCELAMENTO
- **Async all-the-way** — `async`/`await` ponta-a-ponta; NUNCA `.Result`/`.Wait()`/`.GetAwaiter().GetResult()` (deadlock / thread-pool starvation). [disciplina — propor analyzer]
- **`CancellationToken` propagado** — do endpoint Minimal API até a query EF (`ToListAsync(ct)`); request abortado pelo cliente não deve segurar conexão/CPU. [disciplina]
- **Timeout explícito em I/O externo** (Stripe/Resend/WhatsApp via `HttpClient`) — sem timeout = thread presa indefinida se o provider pendurar. Efeitos externos garantidos vão por outbox ([specification-concurrency] §2), que isola a latência do provider do request do usuário.

## 5. ÍNDICES
- Coluna de filtro/join de query quente tem índice. FKs e UQ já indexadas (ver [specification-db] por tabela: `idx(conta_id)`, `idx(resend_message_id)`, `idx(status,proxima_tentativa)` no outbox, etc.). Migration que introduz query de filtro nova AVALIA índice — e atualiza [specification-db]. [gate parcial: revisão de migration]

## 6. ENFORCEMENT (honesto — fraco hoje)
- Sem gate hard atual. Sinais a propor (até existirem, vale só revisão + este checklist): contar SQL emitido em teste de integração (detecta N+1/regressão de query count), teste de contrato p/ paginação obrigatória, analyzer p/ `.Result`/`.Wait()`. Perf budget de backend (latência p95 por endpoint) é ALVO — hoje só frontend tem budget ([specification-observability]).

## 7. REFERÊNCIAS
[specification-db] (índices/schema/conexão), [specification-backend] (handlers/EF/DI), [specification-concurrency] (lock↔perf, outbox isola latência), [specification-observability] (medição/health/budget frontend), [specification-tests §6] (Testcontainers p/ medir query count).
