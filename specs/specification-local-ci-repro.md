# specification-local-ci-repro — reproduzir o CI localmente & lições (forzion.tech)

DOC PARA AGENTES. Fonte de verdade de COMO antecipar falhas do CI (gate homolog) rodando as validações localmente, e o catálogo de GOTCHAS de ambiente (Windows/Docker) descobertos. Formato denso, agent-oriented. Consultar antes de "rodar o CI local", debugar divergência local×CI, ou preparar um PR→homolog. Cross-ref: [specification-tests] (gates/thresholds/comandos canônicos), [specification-infrastructure] (CI/CD workflows, deploy), [specification-security] (semgrep/zap/gitleaks), [specification-frontend-ui] (a11y harness).

## MANUTENÇÃO DESTE ARQUIVO
Atualizar quando: mudar matriz de versões CI (node/dotnet), surgir novo gate/workflow, ou descobrir novo gotcha de reprodução local. NÃO duplicar os comandos canônicos de [specification-tests] §9 — aqui foca em REPRODUTIBILIDADE local e divergências.

## 0. MATRIZ DE AMBIENTE (local×CI) — origem de quase toda divergência
| Tool | CI (ubuntu) | Local típico (Windows) | Impacto |
|---|---|---|---|
| Node | **22** | 20 (default) | coverage v8 conta diferente → thresholds frontend divergem. Use `fnm install 22 && fnm use 22` (PowerShell: `fnm env \| iex; fnm use 22`). |
| .NET SDK | **10.0.x** (pin `global.json` floor 10.0.100, rollForward latestFeature) | 10.x | CI e local alinhados em SDK 10 (TFM `net10.0`). `.slnx` exige SDK ≥9 (SDK 8 dava MSB1003 — histórico, não mais relevante). |
| OS | Linux | Windows + Docker Desktop | CRLF, MSYS path-mangling, fsync de volume glacial. |

## 1. REPRODUÇÃO POR GATE (comando local · rodável? · caveat)
Lista de jobs do `gate` + thresholds: CANÔNICO em [specification-tests] §7/§8. Workflows extra em PR homolog: `semgrep`, `openapi-drift`, `hygiene`, `contract`.

| Gate/check | Comando local | Rodável Win? | Caveat |
|---|---|---|---|
| backend build+format | `dotnet build -c Release` ; `dotnet format forzion.tech.slnx --verify-no-changes` | ✅ | format exige CRLF (§2). `.slnx` precisa SDK ≥9/10. |
| backend unit | `dotnet test forzion.tech.Tests --filter "Category!=Integration"` | ✅ | contagem atual: ver [specification-tests] §4 |
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
| openapi-drift | `bash scripts/gen-swagger.sh docs/api/swagger.v1.json && git diff --exit-code docs/api/swagger.v1.json` | ✅ | gera offline (sem Kestrel). Drift = regenerar+commitar. **Também roda no pre-commit local** (gate backend, após o build — `git diff --quiet`): drift de swagger é pego ANTES do push, não só no CI. Ver [specification-git] §PRE-COMMIT HOOK. |
| hygiene | `npm run deadcode` (madge, bloqueante) ; `npm run knip` (report-only) | ✅ | — |
| contract (Pact consumer) | `npm run test:contract` | ✅ | gera contratos local; publicar no broker precisa secrets. |
| **E2E Playwright (a11y/color-contrast hard-gate)** | ver §4 | ⚠️ parcial | precisa app no ar + browsers + creds. Público+admin OK local; aluno/treinador bloqueados (§4). |

## 2. GOTCHAS DE AMBIENTE (Windows/Docker/git-bash)
- **CRLF / SonarAnalyzer warnings**: ver [specification-git] §EDGE CASES e §PRE-COMMIT HOOK — documentação completa está lá (ENDOFLINE, S3267, sequência fix).
- **MSYS path-mangling (git-bash)**: `docker run -v "$PWD:/repo" … /repo` vira `C:/Program Files/Git/repo` dentro do container ("no such file"). Prefixar `MSYS_NO_PATHCONV=1`.
- **`/p:` no git-bash**: `dotnet test /p:X=Y` → MSBuild vê `p:X` como 2º projeto (MSB1008). Usar `-p:X=Y`.
- **coverlet `--no-build` repetido**: rodar várias sessões coverlet seguidas no Windows → merge/lock dos assemblies instrumentados → números poluídos / rc=1 espúrio. Rodar UMA vez (tabela por módulo) ou processos separados.
- **`.slnx` × SDK** (histórico — projeto pin SDK 10 via `global.json`): SDK 8 não resolvia `.slnx` (`dotnet build` sem arg → MSB1003). Resolvido pela migração net10; só relevante se alguém forçar um SDK <9.
- **Postgres fsync no Docker Desktop/Windows**: volume lento → crash-recovery (`syncing data directory`) leva 90–150s; healthcheck (start_period 20s) marca `unhealthy` e backend (depends_on) não sobe. Remediação: aguardar `pg_isready` aceitar (~150s) e então `docker compose up -d` p/ subir backend/frontend. NÃO dar `down` com postgres mid-shutdown (piora a recovery).
- **Porta 3000 ocupada**: no **Next 16** `next dev` NÃO migra de porta sozinho — falha `EADDRINUSE` e morre (exit 1). Subir explícito em outra porta: `npm run dev -- -p 3001`. Confirmar a porta real e setar `E2E_BASE_URL`/`API_BASE_URL` de acordo — senão Playwright escaneia o app errado (falso "pass"). (Versões antigas do Next migravam automaticamente; não mais.)
- **local-run sem Docker (Development + develop)**: receita + gotchas (launch-profile força Homolog, `frontend/.env.local`, portas) em [specification-infrastructure] §LOCAL-RUN.
- **`npm ci` node 22 (cold)**: reinstala ~600 deps + recompila; lento na 1ª vez + regenera `frontend/public/mockServiceWorker.js` (msw) → reverter esse arquivo se não intencional. `node_modules` reinstalado sob node 22 funciona em node 20 (deps JS puras).
- **gitleaks ruído local**: sem `--no-git` ignorar .gitignore? não — gitleaks `--no-git` escaneia TUDO presente (inclui `.next`/`node_modules` que o CI não tem). Remover `.next` p/ espelhar o checkout do CI.
- **`forzion.tech.PactVerification` está na `.slnx`** (`dotnet build forzion.tech.slnx` o inclui): MANTER. Fora da solution, erros de compilação do provider (ex. refactor `Result<T>`) vazam SILENCIOSAMENTE só no push→homolog (drift latente).
- **Broker Pact: runner alcança SÓ via tailnet (CANÔNICO / INVARIANTE — §Pact broker rota)**: o broker self-hosted (`https://pact.homologacao.forzion.tech`, containers `pact-broker`+`pact-postgres`) vive na MESMA VM do app (`docker-compose.homolog.yml`, atrás do nginx 443). O runner do GitHub NÃO atravessa a borda pública até o broker: a borda Hostinger DROPA o IP rotativo do runner (SYN dropado → `curl (28)` connect-timeout, não "refused") — MESMA patologia que força o `deploy-homolog` a falar com a VM via tailnet (IP privado `VM_TAILNET_IP`), não pela 22/443 pública. Sintoma: job Pact pendura em "Aguarda broker disponivel" 12×, exit 1, com o broker NO AR (heartbeat de fora da borda responde 401).
  - **INVARIANTE (não regredir — pedido explícito do usuário 2026-06-16)**: TODO job que fala com o broker DEVE, ANTES de qualquer acesso, (1) entrar no tailnet (`tailscale/github-action`, `tags: tag:ci`) e (2) mapear o host do broker → `VM_TAILNET_IP` (no runner via `/etc/hosts`; nos `docker run` do pact-cli via `--add-host "$BROKER_HOST:$VM_TAILNET_IP"`). Hostname preservado → TLS/SNI valida pelo cert. Rota efetiva: runner→tailnet→nginx:443→broker, SEM borda pública. Cobre `ci.yml` `pact-publish` + `pact-provider-verify` e `pact-provider.yml` `verify`. `contract.yml` é file-source PURO (não toca o broker) — não precisa. Nenhuma mudança de timing/deploy reintroduz o erro: a causa é ROTA, não tempo.
  - **PRÉ-REQUISITO DE INFRA — ACL Tailscale libera 443 (descoberto+validado 2026-06-16)**: tailnet+resolve no workflow é NECESSÁRIO mas INSUFICIENTE sozinho. O `tag:ci` é escopado restrito e por padrão só alcançava a VM na **22** (SSH do deploy). A **443 via tailnet era DROPADA pela ACL** → `curl (28)` connect-timeout IDÊNTICO ao da borda pública (por isso "rota pública" era só METADE do diagnóstico). PROVA: o step `Resolve broker` ecoa `*** pact.homologacao.forzion.tech` (`***`=IP tailnet mascarado → `/etc/hosts` VÁLIDO) e o curl ao IP privado:443 ainda dá timeout, enquanto a **22 do MESMO IP funciona** (deploy verde) → bloqueio é POR PORTA = ACL. FIX: no policy do Tailscale, o grant `tag:ci` → VM inclui `tcp:443` (além de `tcp:22`). ACL NÃO é versionada no repo (vive no console Tailscale) → este registro é o único home durável. Sem esse grant, QUALQUER re-arrumo de workflow volta a falhar igual. CONFIRMADO verde após o grant: run `27659177368` `pact-publish`+`pact-provider-verify` ambos `success` (broker ok na 1ª tentativa).
  - **DIAGNÓSTICO ANTIGO ERRADO (corrigido 2026-06-16)**: atribuía a falha a RACE com o deploy ("443 inalcançável por dezenas de min durante build/restart nginx"). Por isso os jobs foram movidos p/ pós-deploy (`needs:`) + heartbeat-wait — NÃO resolveu: a 1ª run com os jobs pós-deploy (merge PR#164→homolog) falhou IGUAL, pois a rota seguia pública. Deploy inteiro dura ~2min, não "dezenas de min". A ordem pós-deploy `deploy-homolog → pact-publish → pact-provider-verify` PERMANECE, mas só como ordem de PUBLICAÇÃO consumer→provider (não anti-race).
  - `needs` dos jobs usa `if: always() && needs.<x>.result == 'success'` — o `success()` IMPLÍCITO avalia o grafo TRANSITIVO; sem isso os jobs PR-only skipados em push fariam pular também (mesmo gotcha do `deploy-homolog`). `contract.yml` segue gate de PR puro (consumer+provider file-source, trigger `pull_request`/`dispatch`); `pact-provider.yml` segue `workflow_dispatch`+cron terça (re-verify standalone). `workflow_run` NÃO ordena cross-workflow (lê o workflow do branch default `main`, onde os arquivos não existem) → daí a consolidação no `ci.yml`.
  - **Rede de segurança**: "Aguarda broker disponivel" faz `curl` heartbeat `--connect-timeout 10 --max-time 20` (12×sleep 15) → falha rápido se o broker (CONTAINER) estiver reiniciando. Já via tailnet, não mascara mais a borda. HISTÓRICO: antes do hardening o curl sem timeout pendurava 136s/tentativa → run de 30min.
  - **Broker genuinamente DOWN** (não-rota): via tailnet o heartbeat autenticado pendura/timeout mas o app `homologacao.forzion.tech`=200 → broker interno degradado. App NÃO afetado (DB do app ≠ `pact-postgres`; `can-i-deploy` é `continue-on-error`). Remediação (VM): `docker compose -f docker-compose.homolog.yml ps` / `restart pact-broker pact-postgres`.

## 3. COVERAGE BACKEND — números reais medidos (unit, `Category!=Integration`)
Medido 2026-06-06 (pós-billing treinador + raise de cobertura; SDK 8/10 idênticos → não é artefato de SDK). Contagem unit atual: **2164** (reconferida 2026-06-13 — [specification-tests] §4 é canônico). Gates (pisos) por módulo: CANÔNICO em [specification-tests] §8 — aqui só os reais:
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
