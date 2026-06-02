# specification-security — segurança (forzion.tech)
DOC PARA AGENTES. Fonte de verdade da postura de segurança consolidada (threat model, AuthN/AuthZ, headers/CSP em 3 camadas, rate-limit/brute-force, segredos, SAST/DAST, supply-chain, webhook signing, gaps). Formato denso, agent-oriented. Cross-ref: [specification-backend] (mecânica de JWT/rate-limit/headers/internal — §4), [specification-infrastructure] (nginx edge, ENV/SECRETS na VM, docker-compose, TLS/certbot), [specification-lgpd] (consentimento → gate Sentry, anonimização), [specification-stripe] (Stripe-Signature webhook), [specification-email] (Svix webhook Resend), [specification-tests] (gates CI, thresholds, hooks).

## MANUTENÇÃO DESTE ARQUIVO
Atualizar quando mudar: política de auth (JWT/jti/blacklist/policies/refresh), HTTP security headers ou CSP (qualquer das 3 camadas: app/Next/nginx), rate-limit (políticas/caps/partição) ou brute-force handling, gestão de segredos (User Secrets / `.env` VM / GitHub secrets / comparação constant-time), SAST (semgrep), DAST (zap.yaml/zap.yml), webhook signing (Stripe/Svix/Meta), dependency/secret scanning (gitleaks/osv/npm-audit/license/SBOM/NuGet --vulnerable). Vive em `specs/` versionado. NÃO duplicar a mecânica detalhada de `specification-backend.md` §4 — REFERENCIAR.

## 1. THREAT MODEL / SUPERFÍCIE DE ATAQUE
Trust boundaries: `internet → nginx (edge, TLS terminate) → {frontend:3000 | backend:8080}`. Tudo atrás de nginx em rede docker interna; backend NÃO é exposto direto exceto `/webhooks/` (ver §3 edge routing). Cross-ref [specification-infrastructure].

| Superfície pública | Entrada | AuthN | Trust boundary / notas |
|---|---|---|---|
| SPA / páginas Next | `nginx /` → `frontend:3000` | sessão JWT (cookie/header via proxy) | servidor Next também é proxy BFF |
| Proxy BFF backend | Next `/api/backend/[...path]` | repassa Bearer do usuário | frontend reescreve p/ `API_BASE_URL`; descarta headers crus (ex. svix-*) |
| Auth proxy | Next `/api/auth/*` | pré-auth (login/registro/reset) | rate-limit frontend in-memory (§4) + rate-limit backend `auth` |
| Webhooks | `nginx /webhooks/` → `backend:8080` DIRETO | assinatura (Stripe/Svix/Meta) — anônimo | bypassa o proxy SPA p/ preservar headers de assinatura crus (nginx.conf:59-66) |
| Endpoints internos | `/internal/processar-renovacoes`, `/internal/reconciliar-pagamentos` | header `X-Internal-Key` constant-time (§5) | server-to-server (billing-renewal GH Action); EXPOSTO via HTTPS público sem WAF (gap §8) |
| Health | `/health` | anônimo + rate `read` | — |

Swagger NÃO é exposto publicamente: backend só serve Swagger em Development (nginx.conf:51-53).

## 2. AUTHN / AUTHZ
Mecânica completa em [specification-backend] §4 (auth/authorization, rate-limiting, internal). Resumo de postura:

- **JWT Bearer** (`AuthenticationExtensions.cs`): HS256 simétrico (`Auth:JwtSecret`, mín. 32 bytes validado no boot — `cs:25-26`, falha fechada se ausente `cs:22-23`). `ValidateIssuer`+`ValidateAudience`+`ValidateLifetime` on; **`ClockSkew = TimeSpan.Zero`** (`cs:43` — expiração exata, sem tolerância); **`MapInboundClaims = false`** (`cs:33` — claims crus, sem remapping SOAP; policies leem `tipo_conta`/`sub`/`jti` literais).
- **Blacklist por `jti`** (`OnTokenValidated`, `cs:61-76`): todo token exige `jti` GUID válido (`ctx.Fail` se ausente `cs:64-67`); consulta `ITokenRevogadoRepository.EstaRevogadoAsync` → revogado ⇒ `ctx.Fail`. Logout/troca-de-senha gravam jti revogado; `LimparTokensRevogadosService` (hosted) faz GC. Repo ausente (DI Test) ⇒ checagem pulada (`cs:71`).
- **Policies por `tipo_conta`** (`cs:49-52`): `SystemAdmin` | `Treinador` | `Aluno` (`RequireClaim`). Ownership por recurso (`perfil_id`/`sub`) é enforçado nos handlers/filters, não nas policies — ver [specification-backend] §4 (filters `IEndpointFilter`).
- **Sessional-only, SEM refresh token**: não há endpoint de refresh nem token de longa duração. Rationale: superfície reduzida (sem rotação/armazenamento de refresh tokens, sem replay de refresh roubado); revogação imediata via blacklist jti. Implicação: expiração ⇒ re-login (sem renovação silenciosa); UX aceitável p/ o escopo. Mudança aqui (adicionar refresh) reabre toda a superfície de token rotation — atualizar esta spec + backend §4.
- **CORS** (`DependencyInjectionExtensions.cs:315-337`): origins de `Cors:AllowedOrigins` (`;`-separado), rejeita `*` e URIs inválidas (`cs:317-323`); métodos GET/POST/PUT/DELETE/PATCH/OPTIONS; headers `Content-Type/Authorization/Accept/X-Requested-With`; `AllowCredentials`. Lista vazia ⇒ deny-all + `LogWarning` no boot (`RouteBuilderExtensions.cs:38-55`).

## 3. HTTP SECURITY HEADERS / CSP (3 camadas)
Defesa em profundidade em 3 camadas independentes. Cabeçalhos emitidos em mais de uma camada ⇒ o navegador pode receber duplicatas.

| Header | Camada APP (backend `RouteBuilderExtensions.cs:64-72`) | Camada NEXT (`next.config.ts`) | Camada EDGE (`nginx.conf`) |
|---|---|---|---|
| X-Content-Type-Options | `nosniff` (cs:66) | `nosniff` (ts:42) | `nosniff` (nginx:45) |
| X-Frame-Options | **`DENY`** (cs:67) | **`DENY`** (ts:41) | **`DENY`** (nginx:44) |
| Referrer-Policy | `no-referrer` (cs:68) | `strict-origin-when-cross-origin` (ts:43) | — |
| Permissions-Policy | `camera=(),microphone=(),geolocation=()` (cs:69) | idem (ts:44) | — |
| Strict-Transport-Security | `max-age=31536000; includeSubDomains` (só Production, cs:70-71) | idem (ts:45) | idem `always` (nginx:43) |
| Content-Security-Policy | — | `buildCsp()` (ts:46) + Report-Only se `CSP_REPORT_ONLY=true` (ts:48) | — |
| X-DNS-Prefetch-Control | — | `on` (ts:40) | — |
| X-Robots-Tag | — | — | `noindex, nofollow` **só no host homolog** (nginx.conf, server `homologacao.forzion.tech`) |

**X-Robots-Tag (homolog noindex, A1)**: o server block de `homologacao.forzion.tech` injeta `add_header X-Robots-Tag "noindex, nofollow" always;` — impede buscadores de indexar o staging público (não competir/expor vs produção). NÃO replicar no server block de produção. Defesa em profundidade com `robots.ts` env-gated (`NEXT_PUBLIC_INDEXABLE`, default noindex) — ver [specification-seo] §4.1.

**CSP** (`next.config.ts:20-37`) — só na camada Next (apenas o frontend serve HTML ao browser): `default-src 'self'`; `script-src 'self' 'unsafe-inline'` (+`'unsafe-eval'` só em dev p/ HMR) + `https://js.stripe.com`; `style-src 'self' 'unsafe-inline'` (Emotion injeta inline); `img-src 'self' data: blob: https://*.stripe.com`; `connect-src 'self' https://api.stripe.com https://*.sentry.io`; `frame-src https://js.stripe.com`; `worker-src 'self' blob:` (Sentry Replay); `frame-ancestors 'none'`; `base-uri 'self'`; `form-action 'self'`. `'unsafe-inline'` em script-src é exigido (Next hidrata sem nonce) — false-positive aceito no ZAP (rule 10055, §6).

**X-Frame-Options alinhado em 3 camadas** (`DENY`): app (cs:67), Next (ts:41) e edge nginx (nginx.conf:44) todos setam `DENY`. Para HTML do Next o browser pode receber o header duplicado (Next + nginx) com mesmo valor `DENY` ⇒ sem ambiguidade. `frame-ancestors 'none'` no CSP reforça (CSP prevalece sobre XFO em browsers modernos). Anteriormente o edge usava `SAMEORIGIN` (divergência cosmética, mitigada pelo "mais restritivo vence"); resolvido — manter os 3 alinhados ao mexer em qualquer camada.

## 4. RATE LIMIT / BRUTE-FORCE
Duas camadas (frontend pré-auth + backend autoritativo).

- **Backend** (`DependencyInjectionExtensions.cs:107-169`) — `FixedWindow`, rejeição **429** (`cs:123`). Em `Test` todas viram NoLimiter (`cs:107-114`). Tabela completa em [specification-backend] §4: `auth` 10/min por IP, `write` 60/min, `read` 120/min (por `sub` autenticado, IP fallback — `KeyFromIpOrSub` cs:125-132), `internal` 5/min por IP, `webhook` 300/min por IP. Ordem do pipeline: `UseAuthentication` ANTES de `UseRateLimiter` (`RouteBuilderExtensions.cs`) p/ a partição por `sub` funcionar. `JwtService` emite o claim `sub` (= `conta.Id`) — sem ele a partição caía sempre p/ IP. Atrás do nginx, `UseForwardedHeaders` (Homolog/Production, `RouteBuilderExtensions`) reescreve `RemoteIpAddress` a partir do `X-Forwarded-For` (único hop confiável; backend só acessível via nginx) — sem isso o IP visto seria o do container nginx e TODOS os clientes colapsariam num bucket.
- **Frontend** (`frontend/src/lib/rateLimit.ts`) — guarda só o proxy `/api/auth/*`. `Map` in-memory per-process, **10 req / 60s** (`ts:14-15`), bounded em `MAX_ENTRIES=10_000` com eviction (`pruneExpired`). `getClientIp` (`ts:59-68`): `X-Real-IP` (nginx injeta) → primeiro hop de `X-Forwarded-For`; NUNCA último hop (spoofable). LIMITAÇÃO documentada (`ts:1-12`): single-instance — com N réplicas o cap efetivo vira `10×N`; sem store compartilhada (Redis). Backend é a defesa autoritativa.
- **GAP**: brute-force **monitoring/alerting ausente** (.specs/codebase/CONCERNS.md High). Rate-limit bloqueia (429) mas não há detecção/alerta de tentativas repetidas; `/internal/*` exposto público sem WAF amplifica (§8).

## 5. GESTÃO DE SEGREDOS
Cross-ref [specification-infrastructure] (ENV/SECRETS, `.env` na VM). Por ambiente:

| Ambiente | Mecanismo | Segredos |
|---|---|---|
| Dev local | .NET User Secrets / env vars | `Auth:JwtSecret` (mín 32 bytes), `Internal:ApiKey`, Stripe/Resend/WhatsApp keys |
| VM homolog | `/opt/forzion/.env` (root-only, fora do repo) injetado via `--env-file` no docker-compose | idem prod-like |
| CI / GH Actions | GitHub Actions secrets (`secrets.HOMOLOG_HOST`, `HOMOLOG_SSH_KEY`, `SENTRY_AUTH_TOKEN`, etc.) + `vars.HOMOLOG_BASE_URL` | deploy/scan tokens |

- **`Auth:JwtSecret`**: validado no boot (≥32 bytes UTF-8), falha fechada se ausente (`AuthenticationExtensions.cs:22-26`). Next: `JWT_SECRET`+`API_BASE_URL` obrigatórios em produção (`next.config.ts:11-16`, guard lança no boot).
- **Comparação constant-time `/internal`** (`PagamentosEndpoints.cs:87-97,132-141`): `X-Internal-Key` vs `Internal:ApiKey` via `CryptographicOperations.FixedTimeEquals`, precedido de checagem de comprimento (evita `ArgumentException` em spans desiguais e timing oracle). Mesma técnica no handshake WhatsApp `hub.verify_token` (`WebhookEndpoints.cs:99-103`). Chave ausente/divergente ⇒ 401/403.
- **Scanning de segredos versionados**: gitleaks no CI (§6, `.gitleaks.toml`).

## 6. SAST / DAST / SUPPLY-CHAIN
### SAST — Semgrep (`.github/workflows/semgrep.yml`)
`semgrep scan --config p/default --error --metrics=off`. **BLOQUEANTE** (`--error` falha o job em finding). Triggers: push/PR em `homolog` (paths-ignore docs/specs) + schedule `0 5 * * 1` (segunda 05:00). Escopo via `.semgrepignore` (nginx/infra/fixtures fora). Substitui CodeQL (que exigia GHAS em repo privado).

### DAST — OWASP ZAP (`zap.yaml` + `.github/workflows/zap.yml`) — MAIOR GAP
Automation Framework (`zap.yaml`):
- **Contexto** `forzion-homolog` → `https://homologacao.forzion.tech/`; includePaths `https://homologacao.forzion.tech/.*` (`zap.yaml:6-11`).
- **excludePaths** (`zap.yaml:12-17`): imagens/fonts (`.png/.jpg/.svg/.woff2?`) e **`.*/api/auth$`** (não bombardear rate-limit + auth real).
- **authentication: method `manual`** (`zap.yaml:18-19`) — ZAP NÃO autentica: cobre só superfície anônima.
- **6 jobs** (`zap.yaml:25-58`): `passiveScan-config` (maxAlertsPerRule 10, scanOnlyInScope), `spider` (maxDuration 5, maxDepth 5), `passiveScan-wait` (maxDuration 10), `report` (traditional-html, risks high/medium/low), `alertFilter`. `failOnError: true` / `failOnWarning: false` (`zap.yaml:21-23`).
- **false-positives** (`alertFilter`, `zap.yaml:51-58`): rule **10020** (X-Frame-Options — já DENY via Next) e **10055** (CSP unsafe-inline — exigido por Next hydration + Emotion) marcados `false-positive`.
- Workflow (`zap.yml`): `workflow_dispatch` (input `mode`: baseline|full) + schedule **sexta 02:00 UTC** (`0 2 * * 5`). baseline = passivo rápido (`action-baseline@v0.14.0`); full = active rules SQLi/XSS/path-traversal/CMDi (`action-full-scan@v0.10.0`, ~30 min). Target via input ou `vars.HOMOLOG_BASE_URL`.
- ⚠️ **`allow_issue_writing: false`** (`zap.yml:60,71`) em ambos os modos ⇒ **REPORT-ONLY, NÃO BLOQUEIA** o merge/deploy nem abre issues. ZAP é manual/agendado, fora do gate de CI (§8).

### Dependency / secret scanning (`.github/workflows/ci.yml`)
| Ferramenta | Job | Bloqueante? | Notas |
|---|---|---|---|
| gitleaks | `security` (ci.yml:360-365) | SIM | docker, `--no-git` working tree, `.gitleaks.toml`, `--redact` |
| OSV scanner | `security` (ci.yml:367-375) | NÃO (`continue-on-error`) | report-only (lockfile inclui deps dev) |
| npm audit | `security` (ci.yml:386) | SIM | `npm run audit` = prod, `>= high` (`--omit=dev`) |
| license-checker | `security` (ci.yml:389) | SIM | `npm run license` |
| SBOM CycloneDX (frontend) | `security` (ci.yml:392-400) | artifact | `sbom.cdx.json` |
| NuGet `--vulnerable` | `security-backend` (ci.yml:320-329) | SIM | gate manual: grep do header (cmd sai 0); direto + transitivo |
| SBOM CycloneDX (.NET) | `security-backend` (ci.yml:333-346) | artifact | tool pinada 6.2.0 |

`security` + `security-backend` são `needs` do job `gate` (required check) (ci.yml:405) ⇒ todos os gates bloqueantes acima travam o PR. ZAP NÃO está em `gate`.

## 7. WEBHOOK SIGNING
Todos os webhooks: `AllowAnonymous` + rate `webhook` (300/min IP) + body cap **64 KB** (`LimitedStream`, `WebhookEndpoints.cs:13,161-200` — DoS guard, `InvalidDataException`→400). Roteados direto ao backend pelo nginx (`/webhooks/`) p/ preservar headers crus.

| Webhook | Header assinatura | Verificação | Cross-ref |
|---|---|---|---|
| Stripe `/webhooks/stripe` | `Stripe-Signature` (WebhookEndpoints.cs:36) | Stripe SDK (`EventUtility.ConstructEvent`) no handler | [specification-stripe] |
| Resend `/webhooks/resend` | `svix-id`/`svix-timestamp`/`svix-signature` (cs:69-71) | Svix lib c/ `Resend:WebhookSecret` (cs:72) | [specification-email] |
| WhatsApp `/webhooks/whatsapp` POST | `X-Hub-Signature-256` (cs:137) | HMAC c/ `WhatsApp:AppSecret` (cs:138) | [specification-whatsapp] |
| WhatsApp GET handshake | `hub.verify_token` (query) | constant-time vs `WhatsApp:WebhookVerifyToken` (cs:99-108) | — |

## 8. GAPS CONHECIDOS / NÃO-ENFORÇADO
- **ZAP report-only**: `allow_issue_writing:false`, fora do `gate` → não bloqueia merge/deploy. Findings DAST exigem triagem manual. MAIOR gap de enforcement.
- **DAST não testa endpoints autenticados**: `zap.yaml` auth `manual` + exclude `/api/auth` ⇒ toda a superfície pós-login fica fora da varredura ativa.
- **Brute-force monitoring ausente** (CONCERNS High): rate-limit bloqueia mas sem alerta/detecção; sem fail2ban/WAF no edge.
- **`/internal/*` público sem WAF/firewall** (CONCERNS High): exposto via HTTPS com X-Internal-Key. Constant-time OK, mas sem camada extra; brute-force da key não monitorado.
- **Sem Stripe-Idempotency-Key** nas Create requests (CONCERNS High): app protege via tx serializable, mas idempotency Stripe-side seria belt-and-suspenders.
- **Sentry wiring incompleto** (CONCERNS Medium): `@sentry/nextjs` instalado, gate por consentimento de cookie ([specification-lgpd]); erros prod podem cair em buracos.
- **Branch protection do repo**: push direto a `homolog`/`master` proibido por convenção (CONCERNS:37) mas enforcement de branch protection é responsabilidade de config do repo — REFERENCIAR [specification-infrastructure]; `--no-verify` NUNCA (hooks locais, não server-side).
- **OSV report-only** (deps dev não gateadas); gate de vuln frontend é só `npm audit --omit=dev >= high`.
