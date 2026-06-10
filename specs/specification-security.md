# specification-security — segurança (forzion.tech)
DOC PARA AGENTES. Fonte de verdade da postura de segurança consolidada (threat model, AuthN/AuthZ, headers/CSP em 3 camadas, rate-limit/brute-force, segredos, SAST/DAST, supply-chain, webhook signing, gaps). Formato denso, agent-oriented. Cross-ref: [specification-backend] (mecânica de JWT/rate-limit/headers/internal — §4), [specification-infrastructure] (nginx edge, ENV/SECRETS na VM, docker-compose, TLS/certbot), [specification-lgpd] (consentimento → gate Sentry, anonimização), [specification-stripe] (Stripe-Signature webhook), [specification-email] (Svix webhook Resend), [specification-tests] (gates CI, thresholds, hooks).

## MANUTENÇÃO DESTE ARQUIVO
Atualizar quando mudar: política de auth (JWT/jti/blacklist/policies/refresh), HTTP security headers ou CSP (qualquer das 3 camadas: app/Next/nginx), rate-limit (políticas/caps/partição) ou brute-force handling, gestão de segredos (User Secrets / `.env` VM / GitHub secrets / comparação constant-time), SAST (semgrep), DAST (zap.yaml/zap.yml), webhook signing (Stripe/Svix/Meta), dependency/secret scanning (gitleaks/osv/npm-audit/license/SBOM/NuGet --vulnerable), rotação de segredos / cadência de upgrade de deps / enforcement de AuthZ negativo (§9). Vive em `specs/` versionado. NÃO duplicar a mecânica detalhada de `specification-backend.md` §4 — REFERENCIAR.

## 1. THREAT MODEL / SUPERFÍCIE DE ATAQUE
Trust boundaries: `internet → nginx (edge, TLS terminate) → {frontend:3000 | backend:8080}`. Tudo atrás de nginx em rede docker interna; backend NÃO é exposto direto exceto `/webhooks/` (ver §3 edge routing). Cross-ref [specification-infrastructure].

| Superfície pública | Entrada | AuthN | Trust boundary / notas |
|---|---|---|---|
| SPA / páginas Next | `nginx /` → `frontend:3000` | sessão JWT (cookie/header via proxy) | servidor Next também é proxy BFF |
| Proxy BFF backend | Next `/api/backend/[...path]` | repassa Bearer do usuário | frontend reescreve p/ `API_BASE_URL`; descarta headers crus (ex. svix-*) |
| Auth proxy | Next `/api/auth/*` | pré-auth (login/registro/reset) | rate-limit frontend in-memory (§4) + rate-limit backend `auth` |
| Webhooks | `nginx /webhooks/` → `backend:8080` DIRETO | assinatura (Stripe/Svix/Meta) — anônimo | bypassa o proxy SPA p/ preservar headers de assinatura crus (nginx.conf `location /webhooks/`) |
| Endpoints internos | `/internal/processar-renovacoes-treinador`, `/internal/processar-renovacoes`, `/internal/reconciliar-pagamentos` | header `X-Internal-Key` constant-time (§5) | server-to-server (billing-* GH Actions); EXPOSTO via HTTPS público sem WAF (gap §8) |
| Health | `/health` (liveness) + `/health/ready` | anônimo + rate `read` | contrato em [specification-observability] §2 |

Swagger NÃO é exposto publicamente: backend só serve Swagger em Development (não roteado pelo nginx em homolog/prod).

## 2. AUTHN / AUTHZ
Mecânica completa em [specification-backend] §4 (auth/authorization, rate-limiting, internal). Resumo de postura:

- **JWT Bearer** (`AuthenticationExtensions.AddJwtAuthentication`): HS256 simétrico (`Auth:JwtSecret`, mín. 32 bytes validado no boot, falha fechada se ausente). `ValidateIssuer`+`ValidateAudience`+`ValidateLifetime` on; **`ClockSkew = TimeSpan.Zero`** (expiração exata, sem tolerância); **`MapInboundClaims = false`** (claims crus, sem remapping SOAP; policies leem `tipo_conta`/`sub`/`jti` literais).
- **Blacklist por `jti`** (handler `OnTokenValidated`): todo token exige `jti` GUID válido (`ctx.Fail` se ausente); consulta `ITokenRevogadoRepository.EstaRevogadoAsync` → revogado ⇒ `ctx.Fail`. Logout/troca-de-senha gravam jti revogado; `LimparTokensRevogadosService` (hosted) faz GC. Repo ausente (DI Test) ⇒ checagem pulada.
- **Policies por `tipo_conta`** (`AddAuthorization`): `SystemAdmin` | `Treinador` | `Aluno` (`RequireClaim`). Ownership por recurso (`perfil_id`/`sub`) é enforçado nos handlers/filters, não nas policies — ver [specification-backend] §4 (filters `IEndpointFilter`).
- **Sessional-only, SEM refresh token**: não há endpoint de refresh nem token de longa duração. Rationale: superfície reduzida (sem rotação/armazenamento de refresh tokens, sem replay de refresh roubado); revogação imediata via blacklist jti. Implicação: expiração ⇒ re-login (sem renovação silenciosa); UX aceitável p/ o escopo. Mudança aqui (adicionar refresh) reabre toda a superfície de token rotation — atualizar esta spec + backend §4.
- **CORS** (`DependencyInjectionExtensions.AddCorsPolicy`): origins de `Cors:AllowedOrigins` (`;`-separado), rejeita `*` e URIs inválidas; métodos GET/POST/PUT/DELETE/PATCH/OPTIONS; headers `Content-Type/Authorization/Accept/X-Requested-With`; `AllowCredentials`. Lista vazia ⇒ deny-all + `LogWarning` no boot (`RouteBuilderExtensions.UseApiConfiguration`, CORS startup check).

## 3. HTTP SECURITY HEADERS / CSP (3 camadas)
Defesa em profundidade em 3 camadas independentes. Cabeçalhos emitidos em mais de uma camada ⇒ o navegador pode receber duplicatas.

Camada APP = backend `RouteBuilderExtensions.UseApiConfiguration` (`ctx.Response.Headers.Append`); Camada NEXT = `next.config.ts` headers/`buildCsp`; Camada EDGE = `nginx.conf` `add_header ... always`.

| Header | Camada APP (backend) | Camada NEXT | Camada EDGE (nginx) |
|---|---|---|---|
| X-Content-Type-Options | `nosniff` | `nosniff` | `nosniff` |
| X-Frame-Options | **`DENY`** | **`DENY`** | **`DENY`** |
| Referrer-Policy | `no-referrer` | `strict-origin-when-cross-origin` | — |
| Permissions-Policy | `camera=(),microphone=(),geolocation=()` | idem | — |
| Strict-Transport-Security | `max-age=31536000; includeSubDomains` (só Production) | idem | idem `always` |
| Content-Security-Policy | — | `buildCsp()` + Report-Only se `CSP_REPORT_ONLY=true` | — |
| X-DNS-Prefetch-Control | — | `on` | — |
| X-Robots-Tag | — | — | `noindex, nofollow` **só no server `homologacao.forzion.tech`** |

**X-Robots-Tag (homolog noindex, A1)**: o server block de `homologacao.forzion.tech` injeta `add_header X-Robots-Tag "noindex, nofollow" always;` — impede buscadores de indexar o staging público (não competir/expor vs produção). NÃO replicar no server block de produção. Defesa em profundidade com `robots.ts` env-gated (`NEXT_PUBLIC_INDEXABLE`, default noindex) — ver [specification-seo] §4.1.

**CSP** (`next.config.ts buildCsp`) — só na camada Next (apenas o frontend serve HTML ao browser): `default-src 'self'`; `script-src 'self' 'unsafe-inline'` (+`'unsafe-eval'` só em dev p/ HMR) + `https://js.stripe.com`; `style-src 'self' 'unsafe-inline'` (Emotion injeta inline); `font-src 'self'`; `img-src 'self' data: blob: https://*.stripe.com`; `connect-src 'self' https://api.stripe.com https://*.sentry.io`; `frame-src https://js.stripe.com`; `worker-src 'self' blob:` (Sentry Replay); `frame-ancestors 'none'`; `base-uri 'self'`; `form-action 'self'`. `'unsafe-inline'` em script-src é exigido (Next hidrata sem nonce) — false-positive aceito no ZAP (rule 10055, §6).

**X-Frame-Options alinhado em 3 camadas** (`DENY`): app, Next e edge nginx todos setam `DENY`. Para HTML do Next o browser pode receber o header duplicado (Next + nginx) com mesmo valor `DENY` ⇒ sem ambiguidade. `frame-ancestors 'none'` no CSP reforça (CSP prevalece sobre XFO em browsers modernos). Anteriormente o edge usava `SAMEORIGIN` (divergência cosmética, mitigada pelo "mais restritivo vence"); resolvido — manter os 3 alinhados ao mexer em qualquer camada.

## 4. RATE LIMIT / BRUTE-FORCE
Duas camadas (frontend pré-auth + backend autoritativo).

- **Backend** (`DependencyInjectionExtensions.AddRateLimiter`) — `FixedWindow`, rejeição **429**. Em `Test` todas viram NoLimiter. Caps (autoritativos aqui; mecânica em [specification-backend] §4): `auth` 10/min por IP, `write` 60/min, `read` 120/min (por `sub` autenticado, IP fallback — `KeyFromIpOrSub`), `internal` 5/min por IP, `webhook` 300/min por IP (= §7). Ordem do pipeline: `UseAuthentication` ANTES de `UseRateLimiter` (`RouteBuilderExtensions.UseApiConfiguration`) p/ a partição por `sub` funcionar. `JwtService` emite o claim `sub` (= `conta.Id`) — sem ele a partição caía sempre p/ IP. Rationale de `UseForwardedHeaders`/nginx (IP real do cliente): CANÔNICO em [specification-infrastructure] §NGINX — sem ele todos colapsariam no bucket do container nginx.
- **Frontend** (`frontend/src/lib/rateLimit.ts`) — guarda só o proxy `/api/auth/*`. `Map` in-memory per-process, **10 req / 60s**, bounded em `MAX_ENTRIES=10_000` com eviction (`pruneExpired`). `getClientIp`: `X-Real-IP` (nginx injeta) → primeiro hop de `X-Forwarded-For`; NUNCA último hop (spoofable). LIMITAÇÃO documentada: single-instance — com N réplicas o cap efetivo vira `10×N`; sem store compartilhada (Redis). Backend é a defesa autoritativa.
- **GAP**: brute-force **monitoring/alerting ausente** (.specs/codebase/CONCERNS.md High). Rate-limit bloqueia (429) mas não há detecção/alerta de tentativas repetidas; `/internal/*` exposto público sem WAF amplifica (§8).

## 5. GESTÃO DE SEGREDOS
Cross-ref [specification-infrastructure] (ENV/SECRETS, `.env` na VM). Por ambiente:

| Ambiente | Mecanismo | Segredos |
|---|---|---|
| Dev local | .NET User Secrets / env vars | `Auth:JwtSecret` (mín 32 bytes), `Internal:ApiKey`, Stripe/Resend/WhatsApp keys |
| VM homolog | `/opt/forzion/.env` (root-only, fora do repo) injetado via `--env-file` no docker-compose | idem prod-like |
| CI / GH Actions | GitHub Actions secrets (`secrets.HOMOLOG_HOST`, `HOMOLOG_SSH_KEY`, `SENTRY_AUTH_TOKEN`, etc.) + `vars.HOMOLOG_BASE_URL` | deploy/scan tokens |

- **`Auth:JwtSecret`**: validado no boot (≥32 bytes UTF-8), falha fechada se ausente (`AuthenticationExtensions.AddJwtAuthentication`). Next: `JWT_SECRET`+`API_BASE_URL` obrigatórios em produção (`next.config.ts`, guard lança no boot).
- **Comparação constant-time `/internal`** — validador único compartilhado `PagamentosEndpoints.ChaveInternaValida` (aplicado nos 3 endpoints internos): `X-Internal-Key` vs `Internal:ApiKey` via `CryptographicOperations.FixedTimeEquals`, precedido de checagem de comprimento (evita `ArgumentException` em spans desiguais e timing oracle). Mesma técnica no handshake WhatsApp `hub.verify_token` (`WebhookEndpoints.cs`, branch GET `hub.verify_token`). Chave ausente/divergente ⇒ 401/403.
- **Scanning de segredos versionados**: gitleaks no CI (§6, `.gitleaks.toml`).

## 6. SAST / DAST / SUPPLY-CHAIN
### SAST — Semgrep (`.github/workflows/semgrep.yml`)
`semgrep scan --config p/default --error --metrics=off`. **BLOQUEANTE** (`--error` falha o job em finding). Triggers: push/PR em `homolog` (paths-ignore docs/specs) + schedule `0 5 * * 1` (segunda 05:00). Escopo via `.semgrepignore` (nginx/infra/fixtures fora). Substitui CodeQL (que exigia GHAS em repo privado).

### DAST — OWASP ZAP (`zap.yaml` + `.github/workflows/zap.yml`) — MAIOR GAP
Automation Framework (`zap.yaml`):
- **Contexto** `forzion-homolog` → `https://homologacao.forzion.tech/`; includePaths `https://homologacao.forzion.tech/.*`.
- **excludePaths**: imagens/fonts (`.png/.jpg/.svg/.woff2?`) e **`.*/api/auth$`** (não bombardear rate-limit + auth real).
- **authentication: method `manual`** — ZAP NÃO autentica: cobre só superfície anônima.
- **6 jobs**: `passiveScan-config` (maxAlertsPerRule 10, scanOnlyInScope), `spider` (maxDuration 5, maxDepth 5), `passiveScan-wait` (maxDuration 10), `report` (traditional-html, risks high/medium/low), `alertFilter`. `failOnError: true` / `failOnWarning: false`.
- **false-positives** (`alertFilter`): rule **10020** (X-Frame-Options — já DENY via Next) e **10055** (CSP unsafe-inline — exigido por Next hydration + Emotion) marcados `false-positive`.
- Workflow (`zap.yml`): `workflow_dispatch` (input `mode`: baseline|full) + schedule **sexta 02:00 UTC** (`0 2 * * 5`). baseline = passivo rápido (`action-baseline@v0.14.0`); full = active rules SQLi/XSS/path-traversal/CMDi (`action-full-scan@v0.10.0`, ~30 min). Target via input ou `vars.HOMOLOG_BASE_URL`.
- ⚠️ **`allow_issue_writing: false`** (ambos os modos) ⇒ **REPORT-ONLY, NÃO BLOQUEIA** o merge/deploy nem abre issues. ZAP é manual/agendado, fora do gate de CI (§8).

### Dependency / secret scanning (`.github/workflows/ci.yml`)
| Ferramenta | Job | Bloqueante? | Notas |
|---|---|---|---|
| gitleaks | `security` | SIM | docker, `--no-git` working tree, `.gitleaks.toml`, `--redact` |
| OSV scanner | `security` | NÃO (`continue-on-error`) | report-only (lockfile inclui deps dev) |
| npm audit | `security` | SIM | `npm run audit` = prod, `>= high` (`--omit=dev`) |
| license-checker | `security` | SIM | `npm run license` |
| SBOM CycloneDX (frontend) | `security` | artifact | `sbom.cdx.json` |
| NuGet `--vulnerable` | `security-backend` | SIM | gate manual: grep do header (cmd sai 0); direto + transitivo |
| SBOM CycloneDX (.NET) | `security-backend` | artifact | tool pinada 6.2.0 |

`security` + `security-backend` são `needs` do job `gate` (required check) ⇒ todos os gates bloqueantes acima travam o PR. ZAP NÃO está em `gate`.

## 7. WEBHOOK SIGNING
Todos os webhooks: `AllowAnonymous` + rate `webhook` (300/min IP) + body cap **64 KB** (`LimitedStream` em `WebhookEndpoints.cs` — DoS guard, `InvalidDataException`→400). Roteados direto ao backend pelo nginx (`/webhooks/`) p/ preservar headers crus.

| Webhook | Header assinatura | Verificação | Cross-ref |
|---|---|---|---|
| Stripe `/webhooks/stripe` | `Stripe-Signature` | Stripe SDK (`EventUtility.ConstructEvent`) no handler | [specification-stripe] |
| Resend `/webhooks/resend` | `svix-id`/`svix-timestamp`/`svix-signature` | Svix lib c/ `Resend:WebhookSecret` | [specification-email] |
| WhatsApp `/webhooks/whatsapp` POST | `X-Hub-Signature-256` | HMAC c/ `WhatsApp:AppSecret` | [specification-whatsapp] |
| WhatsApp GET handshake | `hub.verify_token` (query) | constant-time vs `WhatsApp:WebhookVerifyToken` | — |

## 8. GAPS CONHECIDOS / NÃO-ENFORÇADO
- **ZAP report-only**: `allow_issue_writing:false`, fora do `gate` → não bloqueia merge/deploy. Findings DAST exigem triagem manual. MAIOR gap de enforcement.
- **DAST não testa endpoints autenticados**: `zap.yaml` auth `manual` + exclude `/api/auth` ⇒ toda a superfície pós-login fica fora da varredura ativa.
- **Brute-force monitoring ausente** (CONCERNS High): rate-limit bloqueia mas sem alerta/detecção; sem fail2ban/WAF no edge.
- **`/internal/*` público sem WAF/firewall** (CONCERNS High): exposto via HTTPS com X-Internal-Key. Constant-time OK, mas sem camada extra; brute-force da key não monitorado.
- **Sem Stripe-Idempotency-Key** nas Create requests (CONCERNS High): app protege via tx serializable, mas idempotency Stripe-side seria belt-and-suspenders.
- **Sentry wiring incompleto** (CONCERNS Medium): `@sentry/nextjs` instalado, gate por consentimento de cookie ([specification-lgpd]); erros prod podem cair em buracos.
- **Branch protection do repo**: push direto a `homolog`/`master` proibido por convenção (CONCERNS:37) mas enforcement de branch protection é responsabilidade de config do repo — REFERENCIAR [specification-infrastructure]; `--no-verify` NUNCA (hooks locais, não server-side).
- **OSV report-only** (deps dev não gateadas); gate de vuln frontend é só `npm audit --omit=dev >= high`.

## 9. ROTAÇÃO DE SEGREDOS, CADÊNCIA DE DEPS & ENFORCEMENT DE AUTHZ
Postura PROATIVA (complementa §2 AuthZ, §5 segredos, §6 scanning — que são reativos/estáticos). Várias entradas são [ALVO] (política a definir), marcadas.

### 9.1 Rotação de segredos
- [ALVO] Política de rotação POR TIPO de segredo (não rotacionado = exposição acumulada se vazar): `Auth:JwtSecret`, `Internal:ApiKey`, Stripe keys, Resend/WhatsApp tokens. Definir cadência + procedimento por tipo. §5 cobre ARMAZENAMENTO; isto cobre CICLO DE VIDA.
- **Rotação de `Auth:JwtSecret` sem invalidar sessões válidas**: HS256 simétrico (§2) ⇒ trocar a chave invalida TODOS os JWT em voo. Rotação graciosa exige aceitar chave nova+velha durante a janela de expiração do token (validar contra ambas) OU aceitar o re-login em massa (sessional-only, sem refresh — §2 — torna isso menos doloroso: janela curta). DECIDIR e documentar antes de rotacionar.
- `Internal:ApiKey`: rotação coordenada com os GH Actions de billing que a consomem (`vars`/`secrets`) — trocar key + secret no mesmo deploy, senão `/internal` 401 quebra renovações.
- Vazamento confirmado (gitleaks/incidente) ⇒ rotação IMEDIATA, não agendada.

### 9.2 Cadência de upgrade de dependências (proativo)
- §6 pega vuln REATIVO (gitleaks/OSV/npm audit/NuGet `--vulnerable` no `gate`). Falta o PROATIVO: [ALVO] automação de PRs de upgrade regulares (Renovate/Dependabot) p/ não acumular débito até virar vuln gateada.
- Upgrade MAJOR (.NET/Next/React/EF) = avaliação de breaking change + suíte completa ([specification-tests]) + atualizar specs de stack afetadas.
- Pinagem: ferramentas de CI já pinadas (CycloneDX 6.2.0, zap actions) — manter pin + bump consciente.

### 9.3 Enforcement de AuthZ (testar a NEGAÇÃO, não só o caminho feliz)
- §2 define policies (`SystemAdmin`/`Treinador`/`Aluno`) + ownership nos handlers/filters. Lacuna de enforcement: garantir TESTE NEGATIVO por papel.
- [ALVO/disciplina] Matriz de teste papel × recurso × ação: papel errado → **403**; ownership cross-tenant (treinador acessando aluno de OUTRO treinador; aluno lendo ficha de outro) → **403/404**, não vazamento. O gate é o teste de autorização negativo, não a policy em si (policy sem teste negativo é invariante não-verificada).
- Cross-ref defense-in-depth: validação de segurança/compliance no SERVIDOR mesmo com trava no frontend ([specification-coding §4]).
