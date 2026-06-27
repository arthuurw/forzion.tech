# specification-load-testing — harness de medição de performance (forzion.tech)

DOC PARA AGENTES. Como REPRODUZIR a medição empírica de performance que fecha a lacuna do
[specification-performance §6] (enforcement fraco; perf budget backend = ALVO, sem load gate).
A fase 2 da auditoria INFERIU ganhos por leitura de código; esta fase PROVA/refuta com EXPLAIN,
load e Web Vitals reais. Scripts vivem em `scripts/perf/` (infra, fora do código de app).
Formato denso. Cross-ref: [specification-performance], [specification-db], [specification-observability],
[specification-concurrency], [specification-local-ci-repro].

## MANUTENÇÃO
Atualizar quando: mudar o seed/queries-alvo, surgir nova medição, ou um relatório for re-rodado em
outro tier (ex.: Supabase de teste). NÃO duplicar índices/schema de [specification-db] — referenciar.

## 0. PRINCÍPIOS (invariantes)
- **Schema ISOLADO, dados SINTÉTICOS.** Tudo num Postgres efêmero (Docker) no schema dedicado
  `perf_bench` (via `search_path`), populado por `seed-bench.sql` (sem PII real). NUNCA apontar p/
  homolog/develop/public nem produção. Segurança = isolamento; [specification-security] (PII).
- **Honestidade do número.** Bench LOCAL (disco rápido, dados quentes, sem RTT) ≠ Supabase Free
  (vCPU compartilhado, cache frio, RTT). O sinal DURÁVEL é o **delta** (plano de query, buffers,
  forma da curva), não o ms absoluto. Cada `resultados-*.md` declara seu ambiente.
- **NÃO é gate hard.** Esta fase é report-only. Tornar qualquer medição um gate bloqueante de CI =
  decisão FUTURA do usuário (muda processo de CI — exige aprovação, [specification-db §APLICAÇÃO]).

## 1. SEED SINTÉTICO (`scripts/perf/00-setup-db.sh` + `seed-bench.sql`)
- `00-setup-db.sh`: sobe `postgres:17-alpine` efêmero → `CREATE SCHEMA perf_bench` → `dotnet ef
  database update` (migrations EF) → `seed-bench.sql`. Idempotente (reusa container; seed é
  `ON CONFLICT DO NOTHING`). Escala: `N_TREINADORES`/`N_ALUNOS`/`EXEC_PER_ALUNO`.
- **Migrations aplicam em `perf_bench` por search_path** — `AppDbContext` é schema-agnostic (sem
  `HasDefaultSchema`; os `Up()` das migrations criam tabelas SEM qualificador → caem no search_path;
  a 1ª migration usa `current_schema()` no bloco de limpeza, daí o schema precisa existir ANTES do
  migrate). `ConnectionStrings__AppConnection` com `Search Path=perf_bench` (lido pelo
  `AppDbContextFactory` design-time). [specification-db §schema-agnostic].
- Volume default (~300k execuções): 200 treinadores, 5.000 alunos, 5.000 vínculos ativos, 5.000
  assinaturas (10% Inadimplente), 15.000 pagamentos, 15.000 treinos, 300k `execucoes_treino` +
  300k `execucoes_exercicio`. IDs determinísticos `md5(prefixo||i)::uuid` → reexecutável sem random.
- **GOTCHA modelado:** `treino` é FICHA owned por 1 aluno (`ix_treino_alunos_treino_id` UNIQUE entre
  `Ativo`) → treinos são per-aluno, não compartilhados. Outras UQ respeitadas: `assinaturas_aluno`
  1/vínculo; `pagamentos` 1 `Pendente`/assinatura; `contas.email` único.
- DROP/reset: `99-teardown.sh` (remove container + dados).

## 2. EXPLAIN ANALYZE (`run-explain.sh` + `explain-targets.sql` → `resultados-explain.md`)
- 5 queries-alvo reais dos repos: histórico (`ListarComNomePorAlunoAsync`), dashboard/dia
  (`ContarSessoesPorDiaAsync`), progressão (`ProjetarProgressaoAsync`), vínculo+`TemVinculoAtivoPrevio`
  (`ListarComDetalhesAsync`), admin ILIKE (`ListarTodosAsync`).
- **Toggle do índice composto** `(aluno_id, data_execucao DESC)`: ANTES = single-col
  `ix_execucoes_treino_aluno_id` (estado homolog), DEPOIS = composto (quick-win da
  `perf/auditoria-performance`, ainda não em homolog). Restaura single-col ao final (idempotente).
- **Veredito medido (resultados-explain.md):** índice composto JUSTIFICADO — elimina top-N
  heapsort no histórico (~8× tempo, 3× buffers) e habilita **Index-Only Scan** no dashboard
  (16× menos buffers; achado mais forte). Progressão: ganho marginal (cadeia de joins domina).
  Q4 (Seq Scan da subquery `DISTINCT`) e Q5 (`ILIKE '%..%'` sem trigram) RE-CONFIRMados como achados
  REAIS de OUTRO lote — o índice composto não os toca.

## 3. LOAD / EXAUSTÃO DE POOL (`scripts/perf/load/` → `resultados-load.md`)
- **MEDIDO (pgbench, executável):** `run-pgbench.sh` roda as queries quentes em concorrência
  crescente. Curva mostra throughput saturar no nº de núcleos e, além disso, **latência inflar ~11×
  (0.70→7.71 ms) sem ganho de tps** — o mecanismo de enfileiramento que o cliff de pool amplifica.
- **MEDIDO (full-app k6, AC-2.3):** `k6-request-path.js` (8 alunos: login + dashboard + execuções)
  concorrente a `k6-batch-trigger.js` (`/internal/processar-pre-avisos`, 4.600 assinaturas na janela
  → 4.600 events em fan-out), com `sample-pg-activity.sh`. Único diferencial: `DomainEvents:Max
  ConcorrenciaBestEffort` (8 vs 100000). **Resultado (pool cap=20):** unbounded **fixa o pool no teto
  20/20 em 91% do run** + cauda do dashboard 3.8× (233→877 ms); gate=8 nunca satura (teto 16) e fica
  estável. CONFIRMA o achado ALTO. 0 falhas HTTP LOCAL (cache quente drena rápido) → caveat: Supabase
  Free (cache frio+RTT+pool menor) converte a fixação do pool em timeout de aquisição (HTTP 500).
- **Ligação ALTO (gate JÁ mergeado):** `DomainEventDispatcher` despacha eventos best-effort em
  `Task.Run`; o `BestEffortConcurrencyGate` (registrado em `InfrastructureExtensions`, default **8**
  via `DomainEvents:MaxConcorrenciaBestEffort`) bound-a o fan-out. A coluna "unbounded" da medição é o
  estado PRÉ-gate reproduzido só por config (toggle alto), sem alterar código. Em Supabase Free (pool
  20, 1–2 vCPU) o efeito é dramático. Cross-ref [specification-concurrency] (outbox isola latência de provider).

## 4. LIGHTHOUSE + BUNDLE (frontend → `resultados-lighthouse.md`)
- Harness JÁ existe: `npm run lhci` (`@lhci/cli`; `lighthouserc.json` rotas públicas,
  `lighthouserc.auth.cjs` autenticadas via `e2e/scripts/lhci-auth.mjs`). Esta fase RODA o harness e
  versiona o relatório + budgets ALVO.
- **GOTCHA de ferramental (Next 16):** `npm run analyze` (= `ANALYZE=true next build`,
  `@next/bundle-analyzer`) é **NO-OP sob Turbopack** (default do Next 16): "not compatible with
  Turbopack builds, no report will be generated", e a route-table do Turbopack NÃO traz *First Load
  JS*. Usar o analyzer NATIVO: `next experimental-analyze` (`-o` → `.next/diagnostics/analyze`). O
  caminho `next build --webpack` gera o HTML clássico mas hoje FALHA no typecheck (divergência
  webpack×turbopack na rota `(aluno)/aluno/assinatura/page.ts`). Medição de tamanho fiel = chunks
  reais em `.next/static/chunks` (raw+gzip) + network-trace do Lighthouse. Fix do `package.json`
  (trocar `analyze` p/ `experimental-analyze`) = fora desta fase de medição (não toca app).
- Build prod exige env dummy (`API_BASE_URL=x JWT_SECRET=x …` — [specification-local-ci-repro]).
- **Medido (público, desktop-lab, mediana n=3):** Web Vitals verdes — LCP 0.58–0.90s, TBT 0ms, Perf
  97–100; CLS 0.000 EXCETO `/login`=**0.098** (no fio do limite 0.1 — único achado de risco; shift na
  hidratação). First-load script 568–894 kB gz (inflado por prefetch de nav do App Router, não-bloqueante
  → TBT=0). Heavy chunks: exceljs 256 kB gz e recharts 103 kB gz CORRETAMENTE route-split (fora do
  público); zod 75 kB + stripe+sentry 87 kB carregam em TODA rota pública. Lighthouse é LAB (TBT, não
  INP de campo); localhost ≠ CDN. Rotas autenticadas NÃO medidas (exigem backend+login — runbook no relatório).
- Budgets propostos (ALVO report-only): LCP≤2.5s, CLS≤0.1, TBT≤200ms, First Load JS≤600 kB +
  guardrail "exceljs/recharts NUNCA no shared/first-load". Números em `scripts/perf/resultados-lighthouse.md`.

## 5. GOTCHAS Win/Docker (além de [specification-local-ci-repro §2])
- **MSYS path-mangling**: `pgbench -f /tmp/x.sql` no git-bash vira `C:/…/Temp/x.sql`. Prefixar
  `MSYS_NO_PATHCONV=1` SÓ no `docker exec` do pgbench (não global — quebra o path-source de `docker cp`).
- **search_path no psql/pgbench**: passar via `-e PGOPTIONS='--search_path=perf_bench'` no `docker exec`.
- **`dotnet-ef` 9 × runtime EF 10**: aplica migrations OK (warning de versão tolerável); atualizar
  com `dotnet tool update --global dotnet-ef` se necessário.
- **fsync lento no Docker Desktop/Windows**: container FRESCO sobe em segundos; só a recuperação de
  crash de volume reusado é lenta ([specification-local-ci-repro §2]) — o bench usa container efêmero.
- **lhci EPERM no Windows**: `lhci autorun` dá EPERM intermitente ao limpar o tmpdir do Chrome
  (race no `rmSync` do chrome-launcher) e ABORTA o batch no meio. Workaround: rodar `lighthouse`
  por-URL (o JSON é escrito ANTES do cleanup → sobrevive). `next start` com `output:standalone` emite
  warning mas serve 200; porta 3000 não auto-migra no Next 16 → usar `-p 3100`.

## 6. PROPOSTA — CI report-only (NÃO-implementação)
Quando/se o usuário aprovar (muda processo de CI):
- Job `perf-report` (manual/cron, NÃO em todo PR — custo): sobe PG efêmero, roda seed + EXPLAIN +
  pgbench, publica os `resultados-*.md` como artifact. `lhci autorun` com `assert` em nível WARN
  (não-bloqueante) + upload `temporary-public-storage`.
- **Gate HARD (bloquear PR por regressão de p95/bundle/Web Vital) = decisão FUTURA do usuário.**
  Hoje: report-only. Os budgets de `resultados-lighthouse.md` são ALVO, não pisos de CI.

## 7. REFERÊNCIAS
`scripts/perf/README.md` (runbook operacional), [specification-performance §6] (enforcement fraco que
isto preenche), [specification-db] (schema-agnostic/índices/pool), [specification-observability]
(budget frontend/Web Vitals), [specification-concurrency] (gate/outbox), [specification-local-ci-repro]
(gotchas Win/Docker).
