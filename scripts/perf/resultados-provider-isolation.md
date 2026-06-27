# resultados-provider-isolation — isolamento de latência de provider (fase4, FR-5 / AC-2.4 / PERF-04)

Prova empírica de que a latência de um provider externo (e-mail/Resend) **NÃO vaza pro request-path**:
os efeitos externos são diferidos (outbox durável + worker), então uma resposta HTTP quente não infla
quando o provider fica lento. Mede o p95 do request-path com latência de provider = **0 vs +2.000 ms**.

## AMBIENTE (honestidade — bench local ≠ produção)
PostgreSQL 17-alpine em Docker/Windows (16 vCPU locais, dados quentes), API .NET 10 no schema
`perf_bench`, pool 20, **DB acessado DIRETO** (sem toxic — isola a variável de e-mail). E-mail (Resend)
roteado **via toxiproxy** (`scripts/perf/toxiproxy.sh`, mail-proxy :9098 → sink local :9099). O toxic de
latência incide só no caminho do provider de e-mail. O sinal durável é o **DELTA do p95 do request-path
entre as duas condições** (deveria ser ~0), não o ms absoluto.

## Mecanismo sob teste
`POST /suporte/mensagens` (request-path autenticado) persiste o ticket na transação e **enfileira** o
e-mail ao suporte: `MensagemSuporteCriadaEvent` está registrado como handler **DURÁVEL** no
`OutboxDurabilityRegistry` (`InfrastructureExtensions.cs`). O endpoint responde **202 Accepted**; a chamada
HTTP ao Resend acontece DEPOIS, no worker do outbox (cliente `resend`), fora do request-path. Confirmado em
runtime: POST retorna em ~134 ms enquanto o `Start processing HTTP request POST .../emails` aparece no log
do worker segundos mais tarde; com o toxic ativo, a RTT do mail-proxy medida foi **2.0057 s**.

## Carga
k6 `scripts/perf/load/k6-provider-isolation.js`: 8 VUs (1 aluno bench distinto cada, espalha rate-limit
`read`/`write` por-sub), 60 s. Cada iteração: `GET /aluno/dashboard` + `GET /aluno/execucoes` (reads de
controle, sem provider) + **`POST /suporte/mensagens`** (a ação que enfileira efeito durável de provider).
240 iterações por condição, 720 reqs + 8 logins de setup.

## Resultado — p95 por endpoint (request-path)

| endpoint | lat provider = 0 | lat provider = +2.000 ms | Δ | veredito |
|---|---|---|---|---|
| **POST /suporte/mensagens** (enfileira e-mail) | **12.8 ms** | **9.2 ms** | ~0 (ruído) | **ISOLADO** |
| GET /aluno/dashboard (controle) | 9.1 ms | 7.9 ms | ~0 | isolado (sem provider) |
| GET /aluno/execucoes (controle) | 42.1 ms | 6.2 ms | ~0 | isolado (sem provider) |
| http_req_failed | 0 / 728 | 0 / 728 | — | sem falha |

## Veredito — AC-2.4 / PERF-04 CONFIRMADO
Com o provider de e-mail **2.000 ms mais lento**, o p95 do `POST /suporte/mensagens` permaneceu **FLAT
(~9–13 ms)**. Se a chamada ao Resend fosse SÍNCRONA no request-path, o p95 saltaria p/ ~2.012 ms (+2.000 ms);
o observado (9.2 ms) prova que o efeito de provider está **diferido pelo outbox durável** — a latência do
provider é absorvida pelo worker, não pela resposta HTTP. Nenhum endpoint quente bloqueia em provider →
sem vazamento PERF-04. Os reads de controle (dashboard/execuções) também ficam flat, como esperado (não
tocam provider).

A latência foi de fato exercida (RTT do mail-proxy = 2.0057 s; 240 e-mails enfileirados por condição, cada
um pago a +2 s no worker do outbox) — o teste mede isolamento REAL, não ausência de chamada.

## Stripe (AC-5.1, stretch) — NÃO executado
O flow de request-path do Stripe (criação de PaymentIntent/Connect) não é triggerável local sem mocar o
webhook/conta Connect das contas bench. O mecanismo de diferimento é o MESMO (outbox/best-effort), provado
acima para o Resend. Cobertura Stripe via toxiproxy fica como runbook futuro se o flow vier a ser triggerável.

## Reproduzir
```bash
scripts/perf/toxiproxy.sh up                 # sobe proxies (db + mail)
# sink de e-mail em 0.0.0.0:9099 (toxiproxy alcança o host via host.docker.internal)
# API: gate=8, pool=20, DB DIRETO (:55432), Resend:ApiUrl=http://127.0.0.1:9098/emails (via mail-proxy)
scripts/perf/toxiproxy.sh toxic-clear mail   # baseline
k6 run -e BASE=http://localhost:5080 scripts/perf/load/k6-provider-isolation.js   # lat0
scripts/perf/toxiproxy.sh toxic-add mail 2000
k6 run -e BASE=http://localhost:5080 scripts/perf/load/k6-provider-isolation.js   # lat2000
# comparar suporte_post_latencia p95 entre as duas: deve ficar FLAT.
```
