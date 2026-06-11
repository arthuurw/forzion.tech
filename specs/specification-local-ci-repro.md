# specification-local-ci-repro — reproduzir o CI localmente & lições (forzion.tech)

DOC PARA AGENTES. Fonte de verdade de COMO antecipar falhas do CI (gate homolog) rodando as validações localmente, e o catálogo de GOTCHAS de ambiente (Windows/Docker) descobertos. Formato denso, agent-oriented. Consultar antes de "rodar o CI local", debugar divergência local×CI, ou preparar um PR→homolog. Cross-ref: [specification-tests] (gates/thresholds/comandos canônicos), [specification-infrastructure] (CI/CD workflows, deploy), [specification-security] (semgrep/zap/gitleaks), [specification-frontend-ui] (a11y harness).

## MANUTENÇÃO DESTE ARQUIVO
Atualizar quando: mudar matriz de versões CI (node/dotnet), surgir novo gate/workflow, ou descobrir novo gotcha de reprodução local. NÃO duplicar os comandos canônicos de [specification-tests] §9 — aqui foca em REPRODUTIBILIDADE local e divergências.

## 0. MATRIZ DE AMBIENTE (local×CI) — origem de quase toda divergência
| Tool | CI (ubuntu) | Local típico (Windows) | Impacto |
|---|---|---|---|
| Node | **22** | 20 (default) | coverage v8 conta diferente → thresholds frontend divergem. Use `fnm install 22 && fnm use 22` (PowerShell: `fnm env \| iex; fnm use 22`). |
| .NET SDK | **8.0.x** | 10.x (default) | SDK 10 lê `.slnx`; **SDK 8 NÃO** (`dotnet build` sem arg → MSB1003). `dotnet test/build <projeto>` direto contorna. Coverage line/branch idêntico entre SDK 8/10 (NÃO é fonte de divergência de cobertura). |
| OS | Linux | Windows + Docker Desktop | CRLF, MSYS path-mangling, fsync de volume glacial. |

## 1. REPRODUÇÃO POR GATE (comando local · rodável? · caveat)
Lista de jobs do `gate` + thresholds: CANÔNICO em [specification-tests] §7/§8. Workflows extra em PR homolog: `semgrep`, `openapi-drift`, `hygiene`, `contract`.

| Gate/check | Comando local | Rodável Win? | Caveat |
|---|---|---|---|
| backend build+format | `dotnet build -c Release` ; `dotnet format forzion.tech.slnx --verify-no-changes` | ✅ | format exige CRLF (§2). `.slnx` precisa SDK ≥9/10. |
| backend unit (1843) | `dotnet test forzion.tech.Tests --filter "Category!=Integration"` | ✅ | — |
| **backend coverage gates** | ver §3 | ⚠️ | NÃO rodar 6× `--no-build` em sequência no Windows (merge/lock → números errados/rc=1). Rodar 1× sem Include e ler a tabela por módulo. |
| backend integração (Testcontainers) | `dotnet test forzion.tech.Tests --filter "Category=Integration"` | ✅ (Docker) | 96 testes; sobe Postgres efêmero. |
| backend vuln/SBOM | `dotnet list forzion.tech.slnx package --vulnerable --include-transitive` | ✅ | — |
| frontend lint/type | `npm run lint` (eslint sai 0 com só warnings) ; `npm run typecheck` | ✅ | — |
| **frontend test:coverage** | `npm run test:coverage` | ⚠️ | thresholds por-glob; números só fiéis em **node 22**. Máquina lenta → cold-cache estoura timeout (§2). |
| frontend build+storybook | `API_BASE_URL=x JWT_SECRET=x... npm run build` ; `npm run storybook:build` | ✅ | build prod exige `API_BASE_URL`+`JWT_SECRET` dummy. |
| security: npm audit/license/sbom | `npm run audit && npm run license && npm run sbom` | ✅ | — |
| security: gitleaks | `MSYS_NO_PATHCONV=1 docker run --rm -v "$PWD:/repo" zricethezav/gitleaks:latest detect --source=/repo --no-git --config=/repo/.gitleaks.toml --redact --no-banner` | ✅ (Docker) | escaneia árvore tracked; CI roda ANTES do `npm ci` (sem node_modules/.next). Local: excluir/remover `.next` p/ espelhar (§2). |
| security: osv | `docker run … ghcr.io/google/osv-scanner:latest …` | ⚠️ | `:latest` quebra `osv-scanner.toml` (schema novo). report-only no CI. |
| semgrep (SAST) | `MSYS_NO_PATHCONV=1 docker run --rm -v "$PWD:/src" -w /src semgrep/semgrep semgrep scan --config p/default --error --metrics=off` | ✅ (Docker) | bloqueante (`--error`). 0 findings esperado. |
| openapi-drift | `bash scripts/gen-swagger.sh docs/api/swagger.v1.json && git diff --exit-code docs/api/swagger.v1.json` | ✅ | gera offline (sem Kestrel). Drift = regenerar+commitar. |
| hygiene | `npm run deadcode` (madge, bloqueante) ; `npm run knip` (report-only) | ✅ | — |
| contract (Pact consumer) | `npm run test:contract` | ✅ | gera contratos local; publicar no broker precisa secrets. |
| **E2E Playwright (a11y/color-contrast hard-gate)** | ver §4 | ⚠️ parcial | precisa app no ar + browsers + creds. Público+admin OK local; aluno/treinador bloqueados (§4). |

## 2. GOTCHAS DE AMBIENTE (Windows/Docker/git-bash)
- **CRLF / SonarAnalyzer warnings**: ver [specification-git] §EDGE CASES e §PRE-COMMIT HOOK — documentação completa está lá (ENDOFLINE, S3267, sequência fix).
- **MSYS path-mangling (git-bash)**: `docker run -v "$PWD:/repo" … /repo` vira `C:/Program Files/Git/repo` dentro do container ("no such file"). Prefixar `MSYS_NO_PATHCONV=1`.
- **`/p:` no git-bash**: `dotnet test /p:X=Y` → MSBuild vê `p:X` como 2º projeto (MSB1008). Usar `-p:X=Y`.
- **coverlet `--no-build` repetido**: rodar várias sessões coverlet seguidas no Windows → merge/lock dos assemblies instrumentados → números poluídos / rc=1 espúrio. Rodar UMA vez (tabela por módulo) ou processos separados.
- **`.slnx` × SDK 8**: SDK 8 não resolve o formato `.slnx`; `dotnet build` sem arg falha (MSB1003). Targetar o projeto (`dotnet test forzion.tech.Tests`) ou usar SDK ≥9.
- **Postgres fsync no Docker Desktop/Windows**: volume lento → crash-recovery (`syncing data directory`) leva 90–150s; healthcheck (start_period 20s) marca `unhealthy` e backend (depends_on) não sobe. Remediação: aguardar `pg_isready` aceitar (~150s) e então `docker compose up -d` p/ subir backend/frontend. NÃO dar `down` com postgres mid-shutdown (piora a recovery).
- **Porta 3000 ocupada**: no **Next 16** `next dev` NÃO migra de porta sozinho — falha `EADDRINUSE` e morre (exit 1). Subir explícito em outra porta: `npm run dev -- -p 3001`. Confirmar a porta real e setar `E2E_BASE_URL`/`API_BASE_URL` de acordo — senão Playwright escaneia o app errado (falso "pass"). (Versões antigas do Next migravam automaticamente; não mais.)
- **local-run sem Docker (Development + develop)**: receita + gotchas (launch-profile força Homolog, `frontend/.env.local`, portas) em [specification-infrastructure] §LOCAL-RUN.
- **`npm ci` node 22 (cold)**: reinstala ~600 deps + recompila; lento na 1ª vez + regenera `frontend/public/mockServiceWorker.js` (msw) → reverter esse arquivo se não intencional. `node_modules` reinstalado sob node 22 funciona em node 20 (deps JS puras).
- **gitleaks ruído local**: sem `--no-git` ignorar .gitignore? não — gitleaks `--no-git` escaneia TUDO presente (inclui `.next`/`node_modules` que o CI não tem). Remover `.next` p/ espelhar o checkout do CI.
- **`forzion.tech.PactVerification` está na `.slnx`** (`dotnet build forzion.tech.slnx` o inclui): MANTER. Fora da solution, erros de compilação do provider (ex. refactor `Result<T>`) vazam SILENCIOSAMENTE só no push→homolog (drift latente).
- **Broker Pact (homolog) trava o `contract`/`pact-provider` (CANÔNICO)**: ambos dependem do broker self-hosted na VM homolog (`https://pact.homologacao.forzion.tech`, containers `pact-broker`+`pact-postgres`). A borda (nginx) responde `401` mesmo com o broker app/DB degradado; o step "Aguarda broker disponivel" faz `curl` heartbeat **autenticado SEM `--max-time`** → se o broker app/DB não responde, o curl **PENDURA** (não falha em ~3min; runs de 20min+). App NÃO é afetado (DB do app é separado do `pact-postgres`; `can-i-deploy` já é `continue-on-error`). Diagnóstico: app `homologacao.forzion.tech`=200 + broker=`401` (borda viva) mas heartbeat autenticado pendura → broker interno. Remediação (VM): `docker compose -f docker-compose.homolog.yml ps` / `restart pact-broker pact-postgres`. Hardening: add `--max-time` no `curl` do `contract.yml`/`pact-provider.yml` p/ falhar rápido em vez de pendurar.

## 3. COVERAGE BACKEND — números reais medidos (unit, `Category!=Integration`)
Medido 2026-06-06 (pós-billing treinador + raise de cobertura; SDK 8/10 idênticos → não é artefato de SDK). Gates (pisos) por módulo: CANÔNICO em [specification-tests] §8 — aqui só os reais:
| Módulo | Line | Branch | Method | Status vs gate (b75/l85/m85; Api l85/m70) |
|---|---|---|---|---|
| Domain | 95.22% | 92.67% | 94.85% | ✅ |
| Application | 94.82% | 84.55% | 96.14% | ✅ |
| Api | 86.36% | 56.72% | 77.34% | ✅ (line 86.36 ≥ 85 — margem fina; endpoints billing baixaram de 87.74) |
| Infrastructure | 6.2% | 67.15% | 34.87% | n/a unit (branch 35 só na suíte de integração) |
- **Gate Application usa `ThresholdType="line,method"` → exige AMBOS line E method ≥85**: passar line mas method <85 REPROVA (já ocorreu — handlers novos sem cobertura de método). Baseline conservador no `ci.yml` fica abaixo do real; piso canônico em [specification-tests] §8.
- Reproduzir 1 gate: `dotnet test forzion.tech.Tests --no-build -c Release --filter "Category!=Integration" -p:CollectCoverage=true -p:Include="[forzion.tech.Application]*" -p:Threshold=85 -p:ThresholdType="line,method" -p:ThresholdStat=Total` (rebuild antes; não encadear vários).

## 4. E2E A11Y LOCAL (color-contrast hard-gate)
- Stack: `docker compose -f docker-compose.yml up -d` (postgres local + backend Development + frontend :3001). Admin pré-verificado pelo seed; senha = `SEED_ADMIN_PASSWORD` do **`.env` da raiz** (NÃO o User Secrets do `dotnet run`).
- Browsers: `npx playwright install chromium`. `E2E_BASE_URL=http://localhost:3001`.
- Público (sem auth): `npx playwright test e2e/specs/a11y/all-pages-axe.spec.ts --project=chromium-desktop --no-deps -g "paginas publicas"`.
- Admin: `E2E_ADMIN_EMAIL/PASSWORD` setados → `--project=setup -g "role admin"` (escreve `e2e/.auth/admin.json`) → `… --no-deps -g "a11y: admin" --workers=1`.
- ⚠️ **BLOQUEIO aluno/treinador local**: login exige `EmailVerificado`; contas novas via cadastro não verificam (sem Resend → `NullEmailService`) → sem token → não logam → sem storage state. Seed só cria admin. Opções: seed de contas de teste pré-verificadas, ou cirurgia no DB. Hoje aluno/treinador a11y só validam no CI (que provê `E2E_*`).
- ⚠️ **Flakiness em máquina lenta**: o describe completo de admin pode falhar por timeout de render/scan (cold + parallel), mas cada página passa ISOLADA (`-g "axe em /admin/treinadores"`). Falha no batch ≠ violação real — confirmar isolado antes de tratar como a11y bug.
- Resultado 2026-05-30: público 4/4 ✅ (após fix contraste landing), admin 3/3 ✅ (isolado). Tokens de tema iguais nas demais áreas → CI cobre aluno/treinador.

## 5. STATUS CORRENTE (deferidos ao CI)
- frontend coverage thresholds: não medidos limpo local (node 22 / máquina lenta) → CI valida.
- aluno/treinador a11y E2E: bloqueio de verificação de email local (§4) → CI valida.
