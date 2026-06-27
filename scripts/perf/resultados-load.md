# resultados-load — contenção do request-path & exaustão de pool (fase3, T3)

Duas medições EXECUTÁVEIS, ambas sobre a base do seed (300k execuções):
1. **pgbench** — curva de contenção das queries quentes (mecanismo de saturação).
2. **k6 full-app (AC-2.3)** — request-path real do aluno concorrente ao lote de pré-aviso,
   `gate=8` (default atual) vs `unbounded` (toggle de config simulando o estado pré-gate).

## AMBIENTE (honestidade — bench local ≠ produção)
PostgreSQL 17-alpine em Docker Desktop/Windows, **16 vCPU locais**, dados QUENTES, sem RTT de rede.
**Supabase Free (alvo real) = 1–2 vCPU compartilhados + pool pequeno** → o joelho da curva chega em
concorrência MUITO menor e o cliff é mais abrupto. O sinal durável é o **delta** (forma da curva,
saturação do pool), não o milissegundo absoluto.

## 1) Curva de contenção (pgbench, request-path, índice single-col = estado homolog)
`scripts/perf/load/run-pgbench.sh` — 10s/passo, warm:

| clients | tps | lat média (ms) |
|---|---|---|
| 1 | 1.436 | 0.70 |
| 4 | 5.082 | 0.79 |
| 8 | 8.084 | 0.99 |
| 16 | 10.088 | 1.59 |
| 24 | 11.982 | 2.00 |
| 32 | 12.201 | 2.62 |
| 48 | 12.072 | 3.98 |
| 64 | 12.869 | 4.97 |
| 96 | 12.456 | **7.71** |

**Leitura:** tps escala quase linear até ~núcleos (16) e satura em ~12k. Além da saturação,
**adicionar concorrência NÃO aumenta throughput — só infla latência (0.70 → 7.71 ms, ~11×)**:
as transações extras enfileiram atrás da CPU/pool saturados. É o mecanismo exato pelo qual um
fan-out concorrente não-limitado degrada o request-path.

## 2) Full-app k6 — gate=8 vs unbounded (AC-2.3, MEDIDO)
Setup: API no schema `perf_bench`, **pool Npgsql cap=20** (simula tier pequeno), 8 alunos logáveis
(8 VUs request-path: `/aluno/dashboard` + `/aluno/execucoes`, ~16 req/s) CONCORRENTES a 4 disparos
de `/internal/processar-pre-avisos`, cada um com **4.600 assinaturas na janela** → 4.600 domain
events `CobrancaProximaAlunoEvent` em fan-out best-effort (cada handler faz reads no DB + POST de
e-mail num sink local). Único diferencial entre as duas colunas: `DomainEvents:MaxConcorrenciaBestEffort`.

| métrica | **gate=8** (default) | **unbounded** (toggle=100000) |
|---|---|---|
| pool — amostras no cap (20/20 conns) | **0 / 88** | **80 / 88 (91% do run)** |
| pool — max conexões totais | 16 | **20 (cap atingido)** |
| dashboard latência avg | 11.7 ms | **25.1 ms (2.1×)** |
| dashboard latência max (cauda) | 233 ms | **877 ms (3.8×)** |
| request-path falhas HTTP | 0 / 808 | 0 / 808 |

**Veredito — CONFIRMADO:** com o fan-out **unbounded** o pool fica **fixado no teto (20/20) em 91%
do run** e a cauda do request-path infla ~3.8×; com o **gate=8** o mesmo lote de 4.600 eventos nunca
satura o pool (teto 16, 0 amostras no cap) e a latência fica estável. O gate **bound 8** é o que
mantém o request-path fora do joelho da curva — exatamente o achado ALTO da auditoria.

**Por que 0 falhas HTTP local (e por que Supabase Free seria PIOR):** os reads do fan-out são
sub-ms (cache quente, sem RTT) → mesmo enfileirados no pool de 20, drenam rápido e ninguém estoura o
`Timeout` de aquisição. No alvo real (cache frio + RTT + pool menor), cada conexão do fan-out fica
retida 10–50× mais → o pool fixado no teto vira **timeout de aquisição** no request-path (HTTP 500),
não só cauda inflada. O número LOCAL **subestima** a severidade; o sinal durável é a **fixação do
pool no teto sob unbounded vs folga sob gate=8**.

## 3) AC-2.3 ENDURECIDO — forçando o HTTP 500 que o cache quente escondeu (fase4, FR-6)
A fase3 provou a fixação do pool (20/20, 91% do run) mas **0 falha HTTP**: os reads sub-ms (cache
quente, sem RTT) drenavam rápido demais p/ estourar o `Timeout` de aquisição. Para materializar o
**cliff duro** do tier alvo (cache frio + RTT), repetimos o cenário sob latência de DB injetada e pool
reduzido — emulando Supabase Free.

Setup: API → Postgres **via toxiproxy** com toxic de latência **+50 ms/conn** (`scripts/perf/toxiproxy.sh`);
`Maximum Pool Size=10`; `Timeout=10`. Mesmo cenário AC-2.3: 8 alunos request-path (logins do `setup()`
concluídos ANTES do storm via `START_DELAY` no batch) ∥ 4 disparos de pré-aviso, **4.500 eventos por
disparo** (18.000 no total). Único diferencial entre colunas: `DomainEvents:MaxConcorrenciaBestEffort`.

| métrica | **gate=8** (default) | **unbounded** (toggle=100000) |
|---|---|---|
| **request-path falhas HTTP** | **0 / 594** | **8 / 120 (6.66%)** |
| dashboard p95 | 568 ms | **60 s (timeout)** |
| execuções p95 | 414 ms | 38.6 s |
| throughput request-path | 5.56 req/s | **0.99 req/s (colapso 5.6×)** |
| pool — pico de conexões | 10/10 (dreno rápido, 0 falha) | 10/10 (fixado → timeout de aquisição) |

**Veredito — AC-6.3 CONFIRMADO:** sob latência+pool pequeno, o **unbounded materializa o HTTP 500/timeout
de aquisição** que o local quente da fase3 escondeu — `http_req_failed = 6.66%`, p95 do dashboard estoura
p/ 60 s e o throughput desaba 5.6×. Com **gate=8** o MESMO lote de 18.000 eventos passa com **0 falha** e
p95 ~568 ms. O gate é o que mantém o request-path do lado certo do joelho da curva quando cada conexão do
fan-out é retida 10–50× mais (o regime do tier alvo). Confirma que o número da fase3 **subestimava** a
severidade: o cliff não é só cauda inflada, é **falha dura do request-path**.

Ambiente: toxiproxy emula cold-cache/RTT mas NÃO é o tier real (1–2 vCPU compart.) — a aproximação
declara o MECANISMO (pool fixado → timeout de aquisição sob fan-out unbounded), não o ms absoluto de prod.
Reproduzir: `scripts/perf/toxiproxy.sh up && toxiproxy.sh toxic-add db 50`; API com pool=10 apontando à
porta do db-proxy (:5434); rodar o cenário 1× gate=8 e 1× unbounded (logins antes do batch).

## Ligação com o BestEffortConcurrencyGate
- O gate JÁ está no código (`InfrastructureExtensions.cs` registra `BestEffortConcurrencyGate`
  com default **8** via `DomainEvents:MaxConcorrenciaBestEffort`); `DomainEventDispatcher`
  adquire o slot antes de cada handler best-effort. A coluna "unbounded" acima é o estado
  **pré-gate** reproduzido só por config (toggle alto), sem alterar código.
- Mecanismo: cada disparo do lote enfileira N `Task.Run` DB-bound; sem o bound, N≈4.600 competem
  pelo pool simultâneo → soma-se ao request-path → empurra a concorrência total além do joelho.

## Reproduzir
```bash
# 0) base + janela de pré-aviso + contas logáveis (ver § Setup full-app abaixo)
scripts/perf/00-setup-db.sh
# patch: bcrypt real em aluno1..8 + data_proxima_cobranca de assinaturas Ativa → (current_date+3)+12h

# 1) curva pgbench (executável direto):
scripts/perf/load/run-pgbench.sh

# 2) full-app: API no ar (ASPNETCORE_ENVIRONMENT custom p/ pular UserSecrets; Search Path=perf_bench;
#    Maximum Pool Size=20; Resend:ApiUrl → sink local 200; Internal:ApiKey set), depois por gate:
#    Terminal A: CONTAINER=forzion-perfbench scripts/perf/load/sample-pg-activity.sh pg.csv
#    Terminal B: k6 run -e BASE=http://localhost:5080 -e VUS=8 scripts/perf/load/k6-request-path.js
#    Terminal C: k6 run -e BASE=http://localhost:5080 -e INTERNAL_KEY=.. scripts/perf/load/k6-batch-trigger.js
#    Rodar 1× com DomainEvents__MaxConcorrenciaBestEffort=8 e 1× =100000; comparar pool + p95.
```

### Setup full-app (gotchas, p/ outro agente reproduzir)
- **UserSecrets sobrescreve o ConnectionString** em `Development`/`Homolog` (Program.cs adiciona
  UserSecrets DEPOIS do env). Rodar num env custom (ex.: `Bench`) p/ o `ConnectionStrings__AppConnection`
  do ambiente vencer — mantém infra completa (≠ `Test`, que PULA `AddInfrastructure`).
- **Secrets exigidos no boot** (StartupValidator em env não-Dev): `Auth__JwtSecret`,
  `DataProtection__EncryptionKey` (base64 32 bytes), `Mfa__EncryptionKey`, `Stripe__SecretKey`,
  `Stripe__WebhookSecret`, `Internal__ApiKey`.
- **Conta logável:** o seed grava `password_hash` placeholder (35 chars, NÃO é bcrypt válido).
  Para o login funcionar, patchar com hash real: `crypt('Bench@123456', gen_salt('bf',11))` (pgcrypto).
- **Janela de pré-aviso:** `DespacharPreAvisos*` filtra `status='Ativa'` E `DataProximaCobranca ∈
  [hoje+3d, hoje+4d)`. O seed põe +20d → fora da janela; mover p/ +3d12h p/ o lote ter fan-out.
- **E-mail habilita o DB-work do fan-out:** `CobrancaProximaEmailAlunoHandler` retorna cedo se
  `IEmailService.Habilitado=false` (NullEmailService local). Apontar `Resend:ApiKey` + `Resend:ApiUrl`
  a um sink local 200 → habilita o handler → ele faz os reads que pressionam o pool, sem e-mail real.
- **Rate-limit:** `auth`=10/min/IP (≤8 logins ok), `read`=120/min/sub, `internal`=5/min/IP
  (lote a ≤1/30s). Em env `Test` os limiters são no-op, mas `Test` não registra DB — inutilizável.
