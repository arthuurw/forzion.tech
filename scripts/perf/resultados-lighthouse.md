# resultados-lighthouse — Web Vitals + bundle (perf-measurement-harness-fase3, T4)

Medição empírica de Web Vitals (lab) e tamanho de bundle do `frontend/` (Next 16),
para fundamentar BUDGETS report-only (AC-3.3) e o RUNBOOK de reprodução (AC-3.4).
Fase de MEDIÇÃO — NÃO altera código de app (não-objetivo). Números REAIS medidos;
rotas autenticadas BLOQUEADAS por falta de backend local (documentado, não fabricado).

## AMBIENTE (honestidade — lab ≠ field, local ≠ prod CDN)
- **Node v24.18.0** local (CI usa node 22; cobertura difere, mas Web Vitals/bundle são fiéis em 24).
- **Next.js 16.2.9**, build de produção **Turbopack** (default do Next 16), `output: standalone`.
- Servidor: `next start -p 3100` (porta 3000 evitada; Next 16 NÃO auto-migra porta). **gzip ATIVO**
  no `next start` (`Content-Encoding: gzip` confirmado por `curl`). `next start` emite warning com
  `output: standalone` ("use node .next/standalone/server.js") mas serve normal (HTTP 200).
- Lighthouse via `@lhci/cli` 0.15.1 + lighthouse, `preset=desktop`, headless Chrome `--no-sandbox`,
  `--only-categories=performance`, **3 execuções/rota → mediana**.
- Máquina Windows 11, disco rápido, sem throttle de rede além do preset desktop.
- **Caveats duráveis:** (1) Lighthouse é **lab** — reporta **TBT**, não o INP de campo; TBT é proxy.
  (2) **localhost ≠ CDN de produção** (sem RTT real, sem edge, cache quente). (3) Preset **desktop**
  — mobile/3G de campo será PIOR. Os números absolutos são **best-case**; o sinal durável é o
  **delta relativo** e a **composição do bundle**.

## ACHADO DE FERRAMENTAL (gap na fiação do harness)
- `npm run analyze` (= `ANALYZE=true next build`, @next/bundle-analyzer) é **NO-OP sob Turbopack**:
  o build imprime *"The Next Bundle Analyzer is not compatible with Turbopack builds, no report will
  be generated"* — e a tabela de rotas do Turbopack **NÃO** traz a coluna *First Load JS* (só
  Revalidate/Expire). O harness, como fiado, **não gera relatório de bundle**.
- Caminhos que FUNCIONAM (usados aqui):
  - `next experimental-analyze` (analyzer **nativo do Turbopack**; `-o` escreve em `.next/diagnostics/analyze`). ✅ usado.
  - `ANALYZE=true next build --webpack` (gera `.next/analyze/{client,nodejs,edge}.html`). ⚠️ **compila mas
    FALHA no typecheck** — discrepância de geração de tipos webpack×turbopack numa rota
    (`(aluno)/aluno/assinatura/page.ts`: const exportada `CANCELAR_ASSINATURA_DESCRICAO` rejeitada pela
    checagem de tipos do webpack-build). HTML do analyzer é gerado ANTES da falha; numérico do route-table não.
  - Medição de tamanho aqui = **chunks reais em `.next/static/chunks` (raw + gzip)** + transfer de rede
    medido pelo Lighthouse (fonte da verdade do que o browser baixa).
- **Recomendação (fora deste escopo de medição):** trocar `analyze` para `next experimental-analyze`
  no `package.json`, ou manter o webpack-path e corrigir a divergência de tipo da rota assinatura.

## WEB VITALS — rotas PÚBLICAS (mediana n=3, desktop, lab)
| Rota | Perf | LCP | FCP | TBT | CLS | Speed Index |
|---|---|---|---|---|---|---|
| `/`                   | **100** | 0.77s | 0.26s | 0 ms | 0.000 | 0.53s |
| `/login`              | **97**  | 0.90s | 0.21s | 0 ms | **0.098** | 0.49s |
| `/cadastro/aluno`     | **100** | 0.76s | 0.21s | 0 ms | 0.000 | 0.46s |
| `/cadastro/treinador` | **99**  | 0.58s | 0.21s | 0 ms | 0.000 | 0.93s |

- Todas as 4 rotas são **estáticas** (`○` prerender). LCP/TBT/Perf excelentes em desktop-lab.
- **`/login` CLS = 0.098** — encostado no limite "good" (0.1). É o único achado de risco: provável
  shift de layout do form/logo na hidratação. Vale investigar (fora deste escopo de medição).
- TBT = 0 em todas: o JS pesado é **assíncrono/diferido** (ver bundle abaixo) → não bloqueia a thread.

## BUNDLE — transfer por rota (Lighthouse network trace) + composição
**First-load script** medido pelo Lighthouse (transfer = **gzip na rede**; decoded = parse). Inclui o
**prefetch de rotas do App Router** que dispara no trace (links de nav pré-buscados em idle) — por isso
o número é alto apesar do TBT=0; esses bytes são não-bloqueantes.

| Rota | Script transfer (gz, wire) | Script decoded | Total transfer | Heavy-chunks no first-load |
|---|---|---|---|---|
| `/`                   | 894 kB | 3094 kB | 1011 kB | zod, stripe+sentry |
| `/login`              | 873 kB | 3047 kB | 978 kB  | zod, stripe+sentry |
| `/cadastro/aluno`     | 568 kB | 1795 kB | 666 kB  | zod, stripe+sentry |
| `/cadastro/treinador` | 824 kB | 2873 kB | 921 kB  | zod, stripe+sentry |

### Chunks pesados (raw / gzip on-disk) e roteamento
Total: **7.2 MB** em 110 arquivos `.js` (todas as 55 rotas); CSS principal ~8 kB. Maiores chunks:
| Chunk | raw | gzip | Conteúdo (grep) | Entra no first-load público? |
|---|---|---|---|---|
| `22w9seqybvcu8.js` | 909 kB | **256 kB** | **exceljs** | ❌ route-split (só export) |
| `0i40nx5072nra.js` | 401 kB | 128 kB | zod | ❌ |
| `04c9-gppnkpc0.js` | 353 kB | 103 kB | **recharts** | ❌ route-split (dashboards) |
| `2vwv7uf9xj8ku.js` | 310 kB | 75 kB | **zod** (schemas) | ✅ TODAS as rotas públicas |
| `40m6n-hapf471.js` | 270 kB | 87 kB | **stripe + @sentry** | ✅ TODAS as rotas públicas |
| 5× `~362KB` | 362 kB | ~103 kB ea | shared route-group (MUI) | parcial |

- **exceljs (256 kB gz)** é o maior ofensor isolado — corretamente fora das rotas públicas, mas deve ser
  **`import()` dinâmico no clique de export** (não eager no chunk da dashboard). Verificar na medição authed.
- **recharts (103 kB gz)** route-split (só dashboards) — OK, manter assim.
- **zod (75 kB gz) + stripe+sentry (87 kB gz)** carregam em TODA rota pública. Sentry global é esperado;
  Stripe.js poderia ser deferido em rotas sem checkout; zod-schemas no shared é o baseline de validação.

## ROTAS AUTENTICADAS — NÃO MEDIDAS (bloqueadas por backend)
`lighthouserc.auth.cjs` mede a11y/perf de `/aluno`, `/treinador`, `/admin` (1 por role) via cookies do
storage-state Playwright (`e2e/.auth/<role>.json`). **Bloqueio:**
- **Sem backend .NET local** (build/start usou `API_BASE_URL=http://x` dummy) → páginas authed não buscam
  dados reais; data-fetches vão para host falso e falham → render não representativo.
- Storage-states existem (`aluno/treinador/admin.json`, **Jun 24**) mas os JWT provavelmente **expiraram**
  (3 dias) e exigem o backend para revalidar/refresh.
- Subir backend (.NET API + Postgres/Supabase + seed + login real + seed-consent) = esforço fora do
  razoável para esta task de medição. **Honestidade > cobertura:** documentado, não fabricado. Runbook abaixo.

## BUDGETS PROPOSTOS (AC-3.3 — ALVO / report-only, NÃO gate hard)
Base: web.dev "good" (LCP<2.5s, CLS<0.1, INP<200ms≈TBT<200ms) + folga sobre o medido. Enforcement = **warn**.
| Métrica | Atual (público, desktop-lab) | Budget ALVO | Racional |
|---|---|---|---|
| **LCP** | 0.58–0.90s | **≤ 2.5s** | web.dev good; folga enorme em desktop, protege contra regressão mobile/field |
| **CLS** | 0.000–**0.098** | **≤ 0.1** | login JÁ em 0.098 → near-miss; investigar shift do login antes de apertar |
| **TBT** | 0 ms | **≤ 200ms** | proxy de INP<200ms; preserva a thread principal livre |
| **Perf score** | 97–100 | **≥ 0.90** (warn) | acima do 0.85 atual do `lighthouserc.json`; margem real existe |
| **First Load JS** (script transfer gz, público) | 568–894 kB | **≤ 600 kB** (warn) | `cad-aluno` (568) é o baseline; números > são inflados por prefetch de nav (não-bloqueante) |
| **Guardrail de chunk** | exceljs 256 / recharts 103 gz | heavy libs (exceljs, recharts) **NUNCA** no shared/first-load; exceljs via `import()` dinâmico | impede regressão de árvore: o custo real está em libs grandes vazarem pro baseline |

Atual × alvo: **Web Vitals já dentro de todos os alvos** (CLS do login é o único no fio). O budget de
bundle é preventivo — o sistema passa hoje; o risco é exceljs/recharts migrarem para o shead em refactor.

## RUNBOOK (AC-3.4)
Env dummy obrigatório no build/start de produção (`next.config.ts` exige `API_BASE_URL`+`JWT_SECRET`):
`export API_BASE_URL=http://x JWT_SECRET=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx NEXT_PUBLIC_API_BASE_URL=http://x`

### Bundle
```bash
cd frontend
# build prod (Turbopack) — route table NÃO traz First Load JS no Turbopack:
npm run build
# analyzer NATIVO Turbopack (UI interativa em :4000; -o escreve em .next/diagnostics/analyze):
npx next experimental-analyze            # ou: npx next experimental-analyze -o
# analyzer clássico HTML (webpack) — ATUALMENTE falha no typecheck (ver achado de ferramental):
ANALYZE=true npx next build --webpack    # gera .next/analyze/client.html antes da falha
# tamanho real dos chunks (raw + gzip):
ls -S .next/static/chunks/*.js | head | while read f; do \
  echo "$(stat -c%s "$f") raw $(gzip -c "$f" | wc -c) gz  $(basename "$f")"; done
```

### Lighthouse — rotas PÚBLICAS
```bash
cd frontend
npx next start -p 3100 &     # 3000 ocupada? Next 16 não migra; aponte o LH na porta escolhida
# lhci completo (gates do lighthouserc.json):
npx lhci autorun --config=lighthouserc.json
#  ^ GOTCHA Windows: chrome-launcher dá EPERM ao limpar o tmpdir do Chrome (race no rmSync) e
#    ABORTA o batch no meio. Workaround: rodar por-URL com `lighthouse` direto (o JSON é escrito
#    ANTES do cleanup, então sobrevive ao EPERM):
for u in / /login /cadastro/aluno /cadastro/treinador; do \
  npx lighthouse "http://localhost:3100$u" --quiet --preset=desktop \
    --only-categories=performance --output=json --output-path="lh$(echo $u|tr / -).json" \
    --chrome-flags="--headless=new --no-sandbox --disable-dev-shm-usage"; done
```

### Lighthouse — rotas AUTENTICADAS (precisa backend)
```bash
# 1. backend up: docker compose up postgres + dotnet run --project forzion.tech.Api  → API_BASE_URL REAL
# 2. frontend: build + start apontando NEXT_PUBLIC_API_BASE_URL/API_BASE_URL ao backend real
# 3. sessões Playwright: npx playwright test auth.setup  (escreve e2e/.auth/<role>.json) + seed-consent
# 4. por role (cookie via extraHeaders no lighthouserc.auth.cjs):
LHCI_ROLE=aluno     npx lhci autorun --config=lighthouserc.auth.cjs
LHCI_ROLE=treinador npx lhci autorun --config=lighthouserc.auth.cjs
LHCI_ROLE=admin     npx lhci autorun --config=lighthouserc.auth.cjs
# atalho que itera as 3 roles: npm run lhci:auth
```

### PROPOSTA — job de CI report-only (NÃO gate hard; decisão futura do usuário)
- Job GH Actions **`lhci-report`** independente (não bloqueia merge; `continue-on-error` ou job não-required):
  node 22 → build com env dummy de CI → `next start` → `lhci autorun --config=lighthouserc.json`.
- **Assert em nível WARN** (variante do `lighthouserc.json` com `error`→`warn`, ou override no CI) →
  `upload.target=temporary-public-storage` → postar a URL do relatório LH como comentário no PR.
- Rotas authed exigem **service container do backend** (Postgres + API) — mais pesado; **deferir** para
  uma 2ª iteração do job.
- **Tornar GATE HARD (bloquear merge por regressão de budget) = decisão FUTURA do usuário** (muda o
  processo de CI + risco de flake por variância lab em runner compartilhado). Recomendação: rodar
  report-only por ~5–10 builds, observar a variância real, e SÓ então definir thresholds de bloqueio.
  Atenção: `lighthouserc.json` hoje tem assert em **error** (CLS 0.1) e `/login` mede 0.098 → near-miss
  que flakearia se promovido a gate hard sem corrigir o shift do login antes.

## Reproduzir (resumo)
```bash
cd frontend && export API_BASE_URL=http://x JWT_SECRET=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx NEXT_PUBLIC_API_BASE_URL=http://x
npm run build                                   # build prod turbopack
npx next experimental-analyze -o                # composição do bundle
npx next start -p 3100 &                         # servidor (gzip on)
# LH público por-URL (sobrevive ao EPERM do Windows) — ver bloco acima
```
