# specification-security — segurança (forzion.tech)
DOC PARA AGENTES. Fonte de verdade da postura de segurança consolidada (threat model, AuthN/AuthZ, headers/CSP em 3 camadas, rate-limit/brute-force, segredos, SAST/DAST, supply-chain, webhook signing, gaps). Formato denso, agent-oriented. Cross-ref: [specification-backend] (mecânica de JWT/rate-limit/headers/internal — §4), [specification-infrastructure] (nginx edge, ENV/SECRETS na VM, docker-compose, TLS/certbot), [specification-lgpd] (consentimento → gate Sentry, anonimização), [specification-stripe] (Stripe-Signature webhook), [specification-email] (Svix webhook Resend), [specification-tests] (gates CI, thresholds, hooks).

## MANUTENÇÃO DESTE ARQUIVO
Atualizar quando mudar: política de auth (JWT/jti/blacklist/policies/refresh), HTTP security headers ou CSP (qualquer das 3 camadas: app/Next/nginx), rate-limit (políticas/caps/partição) ou brute-force handling, gestão de segredos (User Secrets / `.env` VM / GitHub secrets / comparação constant-time), SAST (semgrep), DAST (zap.yaml/zap.yml), webhook signing (Stripe/Svix/Meta), dependency/secret scanning (gitleaks/osv/npm-audit/license/SBOM/NuGet --vulnerable), rotação de segredos / cadência de upgrade de deps / enforcement de AuthZ negativo (§9). NÃO duplicar a mecânica detalhada de `specification-backend.md` §4 — REFERENCIAR.

## 1. THREAT MODEL / SUPERFÍCIE DE ATAQUE
Trust boundaries: `internet → nginx (edge, TLS terminate) → {frontend:3000 | backend:8080}`. Tudo atrás de nginx em rede docker interna; backend NÃO é exposto direto exceto `/webhooks/` (ver §3 edge routing). Cross-ref [specification-infrastructure].

| Superfície pública | Entrada | AuthN | Trust boundary / notas |
|---|---|---|---|
| SPA / páginas Next | `nginx /` → `frontend:3000` | sessão JWT (cookie/header via proxy) | servidor Next também é proxy BFF |
| Proxy BFF backend | Next `/api/backend/[...path]` | repassa Bearer do usuário | frontend reescreve p/ `API_BASE_URL`; descarta headers crus (ex. svix-*) |
| Auth proxy | Next `/api/auth/*` | pré-auth (login/registro/reset) | rate-limit frontend in-memory (§4) + rate-limit backend `auth` |
| Webhooks | `nginx /webhooks/` → `backend:8080` DIRETO | assinatura (Stripe/Svix/Meta) — anônimo | bypassa o proxy SPA p/ preservar headers de assinatura crus (nginx.conf `location /webhooks/`) |
| Endpoints internos | `/internal/*` — billing (renovações aluno/treinador, reconciliar) + LGPD-purge (`lgpd/contas-elegiveis`, `lgpd/contas/{id}`) + pré-avisos (aluno/treinador); inventário completo em [specification-backend] §endpoints internos | header `X-Internal-Key` constant-time (§5) | server-to-server via SSH+`docker exec` na VM (GH Actions); borda fechada `/internal/` 404 (F3, §8) |
| Health | `/health` (liveness) + `/health/ready` | anônimo + rate `read` | contrato em [specification-observability] §2 |

Swagger NÃO é exposto publicamente: backend só serve Swagger em Development (não roteado pelo nginx em homolog/prod).

## 2. AUTHN / AUTHZ
Mecânica completa em [specification-backend] §4 (auth/authorization, rate-limiting, internal). Resumo de postura:

- **JWT Bearer** (`AuthenticationExtensions.AddJwtAuthentication`): HS256 simétrico (`Auth:JwtSecret`, mín. 32 bytes validado no boot, falha fechada se ausente). `ValidateIssuer`+`ValidateAudience`+`ValidateLifetime` on; **`ClockSkew = TimeSpan.Zero`** (expiração exata, sem tolerância); **`MapInboundClaims = false`** (claims crus, sem remapping SOAP; policies leem `tipo_conta`/`sub`/`jti` literais).
- **Blacklist por `jti`** (handler `OnTokenValidated`): todo token exige `jti` GUID válido (`ctx.Fail` se ausente); consulta `ITokenRevogadoRepository.EstaRevogadoAsync` → revogado ⇒ `ctx.Fail`. Logout/troca-de-senha gravam jti revogado; `LimparTokensRevogadosService` (hosted) faz GC. Repo ausente (DI Test) ⇒ checagem pulada.
- **Policies por `tipo_conta`** (`AddAuthorization`): `SystemAdmin` | `Treinador` | `Aluno` (`RequireClaim`). Ownership por recurso (`perfil_id`/`sub`) é enforçado nos handlers/filters, não nas policies — ver [specification-backend] §4 (filters `IEndpointFilter`).
- **Access curto + refresh com rotação single-use + reuse detection + família revogável** (substituiu o modelo sessional-only): access JWT curto por papel (Aluno/Treinador 15min, SystemAdmin 10min — `Auth:Sessao:<papel>:AccessMinutes`, fallback `Auth:JwtExpirationMinutes`) mantendo o `jti` (blacklist/rate-limit intactos). Refresh é o estado de longa duração: `refresh` raw só no cookie httpOnly; DB guarda **SHA-256** (`refresh_tokens.token_hash`, único). `POST /auth/refresh` (anon, cookie) rotaciona: marca o token usado→sucessor, emite par novo, estende o idle (sliding). **Reuse detection** (token já `UsadoEm` reapresentado) ⇒ `RevogarFamilia(ReuseDetectado)` + `LogWarning` + 401 (defesa contra refresh roubado — §4). Idle = exp do refresh (Aluno/Treinador 7d, Admin 2h); **absolute cap** server-side por família (`AbsolutoExpiraEm`: Aluno 90d / Treinador 30d / Admin 8h). Revogação unificada: logout (`RevogarFamilia(Logout)`), troca/reset de senha e anonimização (`RevogarTodasPorConta`/purga) derrubam todas as sessões + blacklist jti. Lifetimes em `Auth:Sessao` (NR-7). Detalhe completo: [specification-backend] §4 + design `.specs/features/sessao-refresh`.
- **CORS** (`DependencyInjectionExtensions.AddCorsPolicy`): origins de `Cors:AllowedOrigins` (`;`-separado), rejeita `*` e URIs inválidas; métodos GET/POST/PUT/DELETE/PATCH/OPTIONS; headers `Content-Type/Authorization/Accept/X-Requested-With`; `AllowCredentials`. Lista vazia ⇒ deny-all + `LogWarning` no boot (`RouteBuilderExtensions.UseApiConfiguration`, CORS startup check).
- **Idempotency-Key (header, `POST /aluno/execucoes`)**: cliente envia GUID p/ dedup de reenvio offline/double-tap; endpoint valida `Guid.TryParse` (malformado ⇒ `Results.Problem` 400) e normaliza (`parsed.ToString()`) antes de propagar ao command — não confia no formato cru. Valor é **opaco e não-PII** (GUID gerado client-side, sem dado pessoal) → persistido em `execucoes_treino.idempotency_key` apenas como guarda de unicidade ([specification-concurrency §4]); FORA de export LGPD e de anonimização (`RegistrarExecucao` é dado de treino, não identifica; coluna não entra no pacote de export nem é mascarada — [specification-lgpd]). NÃO logado.
- **Tokens JWT de escopo restrito (MFA)**: além do access normal, `JwtService.GerarTokenEscopo(conta, escopo, validade)` emite tokens com claim `scope` (`MfaScopes.ClaimType`) e **SEM** `tipo_conta`/`perfil_id` — dois escopos: `mfa_pending` (login parcial após senha, aguardando 2º fator; 5 min) e `step_up` (reautenticação recente p/ ação sensível; 5 min). A **default policy** (`AddAuthorizationBuilder().SetDefaultPolicy`) agora exige autenticado **E** ausência da claim `scope` ⇒ token scoped NÃO acessa rota de negócio (as policies de papel já o excluiriam por exigir `tipo_conta`; a default fecha as rotas `.RequireAuthorization()` sem papel). Policies dedicadas `MfaPendente`/`MfaStepUp` (`RequireClaim scope`) gateiam só `/auth/mfa/*` e `/auth/step-up/verificar`.

## 2.1 MFA / SEGUNDO FATOR (2FA)
Opt-in por conta; TOTP primário, e-mail OTP fallback, recovery codes p/ perda de dispositivo. Detalhe de modelo/handlers em [specification-backend]/[specification-model]; tabelas em [specification-db].
- **Fatores** (`MfaFator`): **TOTP** (RFC 6238, `Otp.NET`; anti-replay via `conta_mfa.ultimo_time_step` — time-step ≤ último rejeitado), **recovery code** (10 por lote, hex 16-char = 64 bits, SHA-256 em repouso, **single-use** via `usado_em`), **e-mail OTP** (6 dígitos, SHA-256, válido 10 min, cap de tentativas).
- **Segredo TOTP em repouso CIFRADO** (`MfaSecretProtector`, AES-256-GCM): nonce 12B + tag + ciphertext; chave `Mfa:EncryptionKey` (base64 → 32 bytes) validada no boot (`AddMfaProtection`) — ausente/inválida/≠32B ⇒ **falha fechada** (boot lança) em todo ambiente exceto `Test`. NUNCA persistir segredo em claro.
- **Enroll**: `POST /conta/mfa/totp/iniciar` (retorna segredo + URI `otpauth://`) → `confirmar` com TOTP válido habilita o MFA e devolve os 10 recovery codes UMA vez (raw nunca re-exibido).
- **Login com MFA** (`LoginHandler`): senha OK + MFA habilitado ⇒ NÃO emite par de sessão; devolve token `mfa_pending`. Cliente conclui em `POST /auth/mfa/verificar` (TOTP|recovery|e-mail) — só aí sai o par access+refresh. `POST /auth/mfa/email/enviar` dispara o OTP por e-mail. "Lembrar dispositivo" grava `trusted_devices` (hash SHA-256 do token em cookie httpOnly, 30 dias) → pula o 2º fator enquanto válido/não-revogado.
- **Step-up** (reautenticação p/ ação sensível): `POST /auth/step-up/iniciar` (TOTP se habilitado, senão e-mail OTP) → `verificar` emite token `step_up` (5 min). `RequerStepUpFilter` (header `X-Step-Up-Token`, falha `403 step_up_requerido`) protege: desabilitar/regenerar MFA, troca de senha, troca de e-mail, onboarding/payout Stripe e ações admin sensíveis (treinador aprovar/reprovar/inativar). Conta SEM TOTP cai no fator e-mail OTP — gate de **posse-de-inbox**, mais fraco que TOTP (aceito: hash + cap 5 tentativas; trade-off registrado).

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

**CSP** (`next.config.ts buildCsp`) — só na camada Next (apenas o frontend serve HTML ao browser): `default-src 'self'`; `script-src 'self' 'unsafe-inline'` (+`'unsafe-eval'` só em dev p/ HMR) + `https://js.stripe.com`; `style-src 'self' 'unsafe-inline'` (Emotion injeta inline); `font-src 'self'`; `img-src 'self' data: blob: https://*.stripe.com https://i.ytimg.com`; `connect-src 'self' https://api.stripe.com https://*.sentry.io`; `frame-src https://js.stripe.com https://www.youtube-nocookie.com`; `worker-src 'self' blob:` (Sentry Replay); `frame-ancestors 'none'`; `base-uri 'self'`; `form-action 'self'`. `'unsafe-inline'` em script-src é exigido (Next hidrata sem nonce) — false-positive aceito no ZAP (rule 10055, §6). `i.ytimg.com` (img) + `www.youtube-nocookie.com` (frame) habilitam a facade de vídeo de execução do exercício (thumbnail estática → iframe nocookie só após clique; player não carrega nem seta cookies de tracking até o opt-in do aluno — [specification-frontend-ui]).

**X-Frame-Options alinhado em 3 camadas** (`DENY`): app, Next e edge nginx todos setam `DENY`. Para HTML do Next o browser pode receber o header duplicado (Next + nginx) com mesmo valor `DENY` ⇒ sem ambiguidade. `frame-ancestors 'none'` no CSP reforça (CSP prevalece sobre XFO em browsers modernos). Anteriormente o edge usava `SAMEORIGIN` (divergência cosmética, mitigada pelo "mais restritivo vence"); resolvido — manter os 3 alinhados ao mexer em qualquer camada.

## 4. RATE LIMIT / BRUTE-FORCE
Duas camadas (frontend pré-auth + backend autoritativo).

- **Backend** (`DependencyInjectionExtensions.AddRateLimiter`) — `FixedWindow`, rejeição **429**. Em `Test` todas viram NoLimiter. Caps (autoritativos aqui; mecânica em [specification-backend] §4): `auth` 10/min por IP, `write` 60/min, `read` 120/min (por `sub` autenticado, IP fallback — `KeyFromIpOrSub`), `internal` 5/min por IP, `webhook` 300/min por IP (= §7). Ordem do pipeline: `UseAuthentication` ANTES de `UseRateLimiter` (`RouteBuilderExtensions.UseApiConfiguration`) p/ a partição por `sub` funcionar. `JwtService` emite o claim `sub` (= `conta.Id`) — sem ele a partição caía sempre p/ IP. Rationale de `UseForwardedHeaders`/nginx (IP real do cliente): CANÔNICO em [specification-infrastructure] §NGINX — sem ele todos colapsariam no bucket do container nginx.
- **Frontend** (`frontend/src/lib/rateLimit.ts`) — guarda só o proxy `/api/auth/*`. `Map` in-memory per-process, **10 req / 60s**, bounded em `MAX_ENTRIES=10_000` com eviction (`pruneExpired`). `getClientIp`: `X-Real-IP` (nginx injeta) → primeiro hop de `X-Forwarded-For`; NUNCA último hop (spoofable). LIMITAÇÃO documentada: single-instance — com N réplicas o cap efetivo vira `10×N`; sem store compartilhada (Redis). Backend é a defesa autoritativa.
- **Reuse de refresh ⇒ sinal de alerta**: `/auth/refresh` reapresentado com um refresh já usado (token roubado/replay) ⇒ `RevogarFamilia(ReuseDetectado)` + `LogWarning` (`RefreshTokenService`). É o ÚNICO sinal de alerta de credencial ativo hoje — fecha parcialmente o gap abaixo p/ o vetor de refresh roubado (falha do refresh é sempre 401 genérico p/ não vazar a causa — §2).
- **MFA / step-up** — verificação do 2º fator (`/auth/mfa/*`, `/auth/step-up/*`) usa `.RequireRateLimiting("mfa")` = **5/min por CONTA** (sub-keyed, `KeyFromIpOrSub`): o token pendente/step-up já carrega `sub`, então o cap é por-conta e **imune a rotação de IP** (fecha brute-force do TOTP de 6 dígitos — IP-only deixava o atacante girar IP por bucket novo). `/conta/mfa/*` segue `"auth"` (10/min IP — inclui `status` GET de polling; enroll-confirm é da própria conta, disable/regenerar já têm step-up). **Lockout** por defesa em profundidade no domínio: e-mail OTP com cap de **5 tentativas** (`MfaChallenge.MaximoTentativas`, `Bloqueado`), recovery code **single-use** (`usado_em`), TOTP anti-replay (`ultimo_time_step`). Desafios/dispositivos expirados são purgados de hora em hora (`LimparTokensRevogadosService`, GC — `ExecuteDelete`).
- **Sinal estruturado de rejeição** (`OnRejected` do RateLimiter, políticas `auth`/`mfa`): `LogWarning` estruturado com IP/política/rota em 429 — SEM PII (sem e-mail/senha). Consumível por alerta (Sentry/sink). Base para detecção de brute-force no app.
- **fail2ban no edge** (`infra/fail2ban/`, instalado por `setup-vm.sh`): jail `forzion-nginx-auth` casa 401/429 em `/api/auth*` e 429 em `/api/backend/*` no `access_log` do nginx (formato combined; nginx é a borda ⇒ IP real) e bane via iptables. Limiar **conservador** (maxretry 12 / findtime 10m / bantime 1h) por causa de NAT corporativo (1 IP, vários usuários legítimos). nginx loga em ARQUIVO (`access_log /var/log/nginx/access.log`, bind-mount `/var/log/forzion-nginx`) — stdout do container não serve pro fail2ban tailar. **Ban não testado em runtime** (validar na VM).

## 5. GESTÃO DE SEGREDOS
Cross-ref [specification-infrastructure] (ENV/SECRETS, `.env` na VM). Por ambiente:

| Ambiente | Mecanismo | Segredos |
|---|---|---|
| Dev local | .NET User Secrets / env vars | `Auth:JwtSecret` (mín 32 bytes), `Mfa:EncryptionKey` (base64, 32 bytes — AES-256-GCM do segredo TOTP), `DataProtection:EncryptionKey` (base64, 32 bytes — AES-256-GCM do keyring DataProtection), `Internal:ApiKey`, Stripe/Resend/WhatsApp keys |
| VM homolog | `/opt/forzion/.env` (root-only, fora do repo) injetado via `--env-file` no docker-compose | idem prod-like |
| CI / GH Actions | GitHub Actions secrets (`secrets.VM_TAILNET_IP`, `HOMOLOG_SSH_KEY`, `SENTRY_AUTH_TOKEN`, etc.) + `vars.APP_HOST` | deploy/scan tokens |

- **`Auth:JwtSecret`**: validado no boot (≥32 bytes UTF-8), falha fechada se ausente (`AuthenticationExtensions.AddJwtAuthentication`). Next: `JWT_SECRET`+`API_BASE_URL` obrigatórios em produção (`next.config.ts`, guard lança no boot).
- **`Mfa:EncryptionKey`**: chave AES-256-GCM do segredo TOTP em repouso (§2.1). base64 → exatamente 32 bytes, validada no boot (`AddMfaProtection`); ausente/inválida/tamanho errado ⇒ **falha fechada** (lança) em todo ambiente exceto `Test` (que não registra `AddMfaProtection`). Gerar via `openssl rand -base64 32`. Prod exige chave DISTINTA da homolog (reuso = mesmo keystream cross-ambiente).
- **`DataProtection:EncryptionKey`**: chave AES-256-GCM que cifra em repouso o keyring de ASP.NET Core DataProtection (issue #179). Antes: `AddDataProtection` sem `PersistKeysTo*` → repositório efêmero in-memory; chaves regeneravam a cada restart do container (sem user profile/registry) ⇒ antiforgery e payloads `IDataProtector` invalidavam a cada deploy, e cada réplica teria keyring próprio. Agora: `AddDataProtectionPersistence` (`Api/Configuration`) faz `PersistKeysToDbContext<AppDbContext>` (tabela `data_protection_keys`, [specification-db]) + `SetApplicationName("forzion.tech")` (keyring compartilhado entre réplicas) + `KeyManagementOptions.XmlEncryptor = AesGcmXmlEncryptor` (cifra cada chave em repouso — nonce+tag+ct base64, mesmo envelope do `MfaSecretProtector`; decifra via `AesGcmXmlDecryptor`). GOTCHA: o activator interno do DataProtection instancia o `IXmlDecryptor` SÓ por ctor parameterless OU ctor `(IServiceProvider)` — NÃO faz DI por ctor arbitrário; logo `AesGcmXmlDecryptor` recebe `IServiceProvider` e resolve `DataProtectionAesGcmKey` dele (ctor `(DataProtectionAesGcmKey)` compila mas explode em runtime no 2º boot ao ler key persistida — teste direto não pega; cobertura: round-trip `Protect`→novo provider→`Unprotect`). base64 → exatamente 32 bytes, validada no boot; ausente/inválida/tamanho errado ⇒ **falha fechada** (lança) em todo ambiente exceto `Test` (não registra). Gerar via `openssl rand -base64 32`. DISTINTA por ambiente E da `Mfa:EncryptionKey` (não reusar keystream). ESTÁVEL: trocar invalida o keyring persistido (chaves cifradas ficam indecifráveis). 3 eixos: persistência em Postgres (vs volume host) escolhida por sobreviver a restart + ser shared multi-instância; AES-GCM em repouso fecha o warn `XmlKeyManager[35]` (No XML encryptor) — DB já é trust boundary mas a cifra adiciona defesa em profundidade. Provisionar `DATA_PROTECTION_KEY` no `/opt/forzion/.env` da VM antes do deploy (senão `app migrate`/boot falham fechado).
- **Comparação constant-time `/internal`** — validador único compartilhado `InternalApiKeyValidator.ChaveInternaValida` (`Api/Extensions`; `PagamentosEndpoints` delega via wrapper fino), aplicado em TODOS os endpoints `/internal/*`: `X-Internal-Key` vs `Internal:ApiKey` via `CryptographicOperations.FixedTimeEquals`, precedido de checagem de comprimento (evita `ArgumentException` em spans desiguais e timing oracle). Mesma técnica no handshake WhatsApp `hub.verify_token` (`WebhookEndpoints.cs`, branch GET `hub.verify_token`). Chave ausente/divergente ⇒ 401/403.
- **Certificado A1 NFS-e (`Nfse:CertificadoPath`/`Nfse:CertificadoSenha`)**: .pfx e-CNPJ usado como cert cliente mTLS (`HttpClientHandler.ClientCertificates`) E p/ assinar o DPS XML (mesmo A1). `CertificadoSenha` é SEGREDO — **NUNCA logada** (gate explícito no serviço). `ValidateOnStart` fail-closed: `Nfse:Habilitado=true` SEM cert/senha/config → boot falha; `Habilitado=false` (dev/hmg) → boot OK sem segredo. Chave privada carregada efêmera (`X509KeyStorageFlags.EphemeralKeySet`, não persiste em disco) — só funciona em runtime Linux (deploy Debian); Windows local não emite. Canônico em [specification-fiscal].
- **Scanning de segredos versionados**: gitleaks no CI (§6, `.gitleaks.toml`).

## 6. SAST / DAST / SUPPLY-CHAIN
### SAST — Semgrep (`.github/workflows/semgrep.yml`)
`semgrep scan --config p/default --error --metrics=off`. **BLOQUEANTE** (`--error` falha o job em finding). Triggers: PR→`homolog` + `workflow_dispatch` (sem schedule — removido; não dispara fora da branch default). Escopo via `.semgrepignore` (nginx/infra/fixtures fora). Substitui CodeQL (que exigia GHAS em repo privado).

### DAST — OWASP ZAP (duas camadas: baseline efêmero bloqueante + full autenticado agendado)
**(1) Baseline efêmero — BLOQUEANTE no PR (`ci.yml` job `zap-baseline`, em `needs` do `gate`)**
- Sobe SÓ a imagem `frontend` standalone (sem backend/DB) e roda `zap-baseline.py` (passivo) contra `http://localhost:3001`. Passivo inspeciona headers/cookies/CSP da resposta — não precisa de backend vivo.
- Os security headers vêm do `next.config` (`headers()` em `/(.*)`): X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy, **HSTS** e CSP. Remover um num PR gera alerta novo ⇒ **reprova o gate**.
- **FPs conhecidos** em `.zap/baseline.conf` (IGNORE): **10020** (X-Frame-Options — já DENY) e **10055** (CSP unsafe-inline — exigido por Next hydration + Emotion). Sem `-I`: qualquer alerta novo fora desses reprova.
- Cobertura: headers de APP (next.config). Headers de **borda** (nginx, ex. HSTS no edge) NÃO entram aqui (não há nginx no stack efêmero) — ficam pro full autenticado contra homolog.
- PR-only, path-filter `frontend`/`ci`.

**(2) Full autenticado — agendado/manual (`zap.yaml` AF + `.github/workflows/zap.yml`)**
- **Contexto** `forzion-homolog` → `https://homologacao.forzion.tech/`.
- **authentication: method `json`** — login `POST /api/auth` `{email,senha}`; verificação `poll` em `/api/auth/me` (responde 200 SEMPRE, corpo `null` deslogado → regex `"contaId"` vs `^null$`); `sessionManagement: cookie` (httpOnly transparente no plano HTTP). Credenciais via env `${ZAP_AUTH_USER}`/`${ZAP_AUTH_PASSWORD}` (secrets) — **conta DEDICADA de teste**, nunca real.
- **excludePaths**: imagens/fonts + rotas que **mutam/encerram estado** (logout, reset/forgot/verify/resend, register/*, mfa/*, exclusao/lgpd/pagamento/checkout/stripe no `/api/backend`) — active scan injeta payload; não corromper conta nem disparar e-mail/cobrança.
- **jobs**: `passiveScan-config`, `spider` (user zap-test), `passiveScan-wait`, **`activeScan`** (user zap-test, maxScanDurationInMins 30, delayInMs 100, handleAntiCSRFTokens) — SQLi/XSS/path-traversal/CMDi pós-login, `report`, `alertFilter` (10020/10055 FP).
- Workflow `zap.yml`: `mode=baseline` (passivo anônimo, `action-baseline@v0.14.0`) ou `mode=full` (roda o AF plan `zap.yaml` via `docker run ghcr.io/zaproxy/zaproxy:stable zap.sh -autorun`, exige secrets de auth). Schedule **sexta 02:00 UTC**. Relatório como artifact.

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

`security` + `security-backend` + `zap-baseline` são `needs` do job `gate` (required check) ⇒ travam o PR. O ZAP **baseline** está no `gate`; o ZAP **full autenticado** é agendado/manual (fora do gate, pelo custo ~30 min).

## 7. WEBHOOK SIGNING
Todos os webhooks: `AllowAnonymous` + rate `webhook` (300/min IP) + body cap **64 KB** (`LimitedStream` em `WebhookEndpoints.cs` — DoS guard, `InvalidDataException`→400). Roteados direto ao backend pelo nginx (`/webhooks/`) p/ preservar headers crus.
- **Resposta de falha não vaza detalhe** (info-leak guard): handler que rejeita responde `400` com `detail` FIXO `"Webhook inválido."` — nunca o `Error.Message` interno (que poderia revelar estado/segredo de verificação ao chamador anônimo). O `Error.Message` real é registrado via `LogWarning` server-side (logger `Webhooks.Resend`/`Webhooks.WhatsApp`) antes da resposta. Mecânica em [specification-backend] §Webhooks.

| Webhook | Header assinatura | Verificação | Cross-ref |
|---|---|---|---|
| Stripe `/webhooks/stripe` | `Stripe-Signature` | Stripe SDK (`EventUtility.ConstructEvent`) no handler | [specification-stripe] |
| Resend `/webhooks/resend` | `svix-id`/`svix-timestamp`/`svix-signature` | Svix lib c/ `Resend:WebhookSecret` | [specification-email] |
| WhatsApp `/webhooks/whatsapp` POST | `X-Hub-Signature-256` | HMAC c/ `WhatsApp:AppSecret` | [specification-whatsapp] |
| WhatsApp GET handshake | `hub.verify_token` (query) | constant-time vs `WhatsApp:WebhookVerifyToken` | — |

## 8. GAPS CONHECIDOS / NÃO-ENFORÇADO
- **ZAP full autenticado é agendado/manual** (não no `gate`, pelo custo ~30 min): findings do active scan exigem triagem manual. O baseline passivo JÁ bloqueia o PR (§6). Resíduo: regressão pós-login só pega no schedule semanal, não no PR.
- **fail2ban: ban não testado em runtime** (entrega = config versionada + setup-vm). Validar ban→unban na VM antes de confiar (§4).
- **`/internal/*` na borda [RESOLVIDO F3]**: `location /internal/ { return 404; }` no nginx (ANTES de `location /`) — nenhuma request externa alcança os endpoints internos. Os crons de billing/fiscal/LGPD acessam por DENTRO da VM via SSH (`appleboy/ssh-action`) + `docker compose exec backend curl localhost:8080/internal/...` (key lida de `/opt/forzion/.env`). Defesa em profundidade: X-Internal-Key constant-time (§5) PERMANECE exigida no backend. Pendente DoD#4: smoke pós-deploy (`curl` externo → 404; crons `workflow_dispatch` verdes). Ver [specification-infrastructure §NGINX / §Workflows billing/cron].
- **Sentry wiring incompleto** (CONCERNS Medium): `@sentry/nextjs` instalado, gate por consentimento de cookie ([specification-lgpd]); erros prod podem cair em buracos.
- **Branch protection do repo**: push direto a `homolog`/`master` proibido por convenção (CONCERNS:37) mas enforcement de branch protection é responsabilidade de config do repo — REFERENCIAR [specification-infrastructure]; `--no-verify` NUNCA (hooks locais, não server-side).
- **OSV report-only** (deps dev não gateadas); gate de vuln frontend é só `npm audit --omit=dev >= high`.
- **Resíduo de enumeração: e-mail-não-verificado [ACEITO]** (F9, decisão D2). `LoginHandler` lança `EmailNaoVerificadoException` (403 distinto) APÓS o `Verify` de senha passar (`LoginHandler.cs` ordem: conta nula → dummy verify + `CredenciaisInvalidasException`; senha errada → `CredenciaisInvalidasException`; senha CERTA + e-mail não verificado → `EmailNaoVerificadoException`). O 403 distinto só é alcançável por quem JÁ tem a senha correta ⇒ NÃO vaza existência/estado de conta a atacante sem credencial (o timing oracle de existência foi fechado por F2 — §2). Resíduo aceito: um atacante que já possui a senha válida distingue conta verificada de não-verificada. **Rationale:** UX — usuário legítimo com e-mail pendente precisa da mensagem acionável "verifique seu e-mail" (reenviar verificação), não de um "credenciais inválidas" genérico que vira beco sem saída. Mover o check de `EmailVerificado` para ANTES do `Verify` reintroduziria timing oracle de existência (descartado no design). Trade-off (eixo segurança×usabilidade): o ganho de usabilidade supera um resíduo que exige a senha correta como pré-condição.

## 9. ROTAÇÃO DE SEGREDOS, CADÊNCIA DE DEPS & ENFORCEMENT DE AUTHZ
Postura PROATIVA (complementa §2 AuthZ, §5 segredos, §6 scanning — que são reativos/estáticos). Várias entradas são [ALVO] (política a definir), marcadas.

### 9.1 Rotação de segredos
Política de rotação POR TIPO (não rotacionado = exposição acumulada se vazar). §5 cobre ARMAZENAMENTO; isto cobre CICLO DE VIDA. Cadência = rotação AGENDADA proativa; qualquer vazamento confirmado dispara o runbook abaixo (IMEDIATO, ignora cadência). Por ambiente (§10.2 — nunca reusar entre prd/hmg).

| Segredo | Cadência agendada | Procedimento |
|---|---|---|
| `Auth:JwtSecret` | 90d | Trocar 1 valor; sessões absorvem via `/auth/refresh` (detalhe abaixo). Sem janela de manutenção. |
| `Internal:ApiKey` | 90d | Trocar `INTERNAL_API_KEY` em `/opt/forzion/.env` + redeploy backend. Crons leem a key do MESMO `.env` via SSH (F3) → não há secret de CI a sincronizar; janela de inconsistência eliminada. |
| Stripe (secret + webhook signing) | 180d ou on-suspect | Stripe roll com expiração graciosa (key velha+nova válidas na janela); trocar `Stripe:SecretKey` no app, depois revogar a velha. Webhook secret: re-criar endpoint signing secret + atualizar config antes de revogar. |
| Resend / WhatsApp tokens | 180d ou on-suspect | Emitir token novo no provedor, trocar no env, revogar o velho após deploy verde. |
| `Mfa:EncryptionKey`, `DeliveryLog:RecipientHashKey` | NÃO rotacionar casualmente | Chave de criptografia/hash de dados EM REPOUSO: trocar quebra decrypt do TOTP secret / desliga a correlação de hash de destinatário. Só sob vazamento confirmado, COM plano de re-encrypt/migração (re-enroll de MFA ou re-cifrar `mfa.secret_cifrado`). |
| Cert A1 NFS-e | Na expiração do cert (efêmero, runtime Linux — [specification-fiscal]) | Substituir PFX + `CertificadoSenha`; senha nunca logada. |
- **Rotação de `Auth:JwtSecret` sem invalidar sessões válidas**: HS256 simétrico (§2) ⇒ trocar a chave invalida TODOS os access JWT em voo. Com access curto (15min/10min — §2), a janela de dor é pequena: após a troca, cada cliente cai em 401 → `/auth/refresh` (cookie httpOnly, NÃO assinado com a chave) reemite um access com a chave nova. Ou seja, a renovação silenciosa absorve a rotação sem re-login manual — desde que o refresh (não-JWT) siga válido. Rotação graciosa (aceitar chave nova+velha) deixa de ser necessária para evitar logout em massa. DECIDIR e documentar mesmo assim antes de rotacionar.
- `Internal:ApiKey`: desde F3 os crons leem a key direto de `/opt/forzion/.env` (SSH+`docker exec`), não de secret de CI ⇒ rotação = trocar `INTERNAL_API_KEY` no `.env` + redeploy backend, sem coordenar secret de GH Actions.
- Vazamento confirmado (gitleaks/incidente) ⇒ rotação IMEDIATA, não agendada.

**Runbook de vazamento** (segredo exposto em log/commit/incidente): 1. CONTER — rotacionar o segredo afetado JÁ (procedimento da tabela), sem esperar cadência. 2. REVOGAR o valor velho no provedor (Stripe/Resend/WhatsApp/Supabase) — rotacionar sem revogar deixa a cópia vazada viva. 3. DERRUBAR sessões se o segredo toca auth: `Auth:JwtSecret` rotacionado já invalida access em voo; suspeita de comprometimento de conta ⇒ `RevogarTodasPorConta` (§2). 4. PURGAR o segredo do histórico se vazou em git (rewrite + force-push coordenado; gitleaks no `gate` evita recorrência). 5. REGISTRAR escopo/janela de exposição e o que foi rotacionado. `service_role` Supabase (ignora RLS — §10.1) ou `Mfa:EncryptionKey` vazados = incidente de dados, não só de segredo: avaliar acesso indevido + obrigação LGPD ([specification-lgpd]).

### 9.2 Cadência de upgrade de dependências (proativo)
- §6 pega vuln REATIVO (gitleaks/OSV/npm audit/NuGet `--vulnerable` no `gate`). O PROATIVO é o **Dependabot** (`.github/dependabot.yml`): ecossistemas `nuget` (raiz), `npm` (`/frontend`) e `github-actions`, schedule weekly, `open-pull-requests-limit` baixo, grouped minor/patch p/ reduzir ruído. Respeita pins de CI já fixados (CycloneDX, zap actions — §9.2 abaixo): bump consciente via review do PR, não auto-merge.
- Upgrade MAJOR (.NET/Next/React/EF) = avaliação de breaking change + suíte completa ([specification-tests]) + atualizar specs de stack afetadas.
- Pinagem: ferramentas de CI já pinadas (CycloneDX 6.2.0, zap actions) — manter pin + bump consciente.

### 9.3 Enforcement de AuthZ (testar a NEGAÇÃO, não só o caminho feliz)
- §2 define policies (`SystemAdmin`/`Treinador`/`Aluno`) + ownership nos handlers/filters. Lacuna de enforcement: garantir TESTE NEGATIVO por papel.
- Matriz de teste papel × recurso × ação (papel errado → **403**) implementada em `forzion.tech.Tests/Api/Endpoints/AutorizacaoNegativaMatrixTests.cs`: rotas só-Treinador / só-Aluno / só-Admin acessadas pelo papel errado → 403, e sem token → 401, via `WebApplicationFactory` + scheme de teste (a autorização curto-circuita antes do handler, então o teste falha se a policy for removida — não-tautológico). Ownership cross-tenant (treinador acessando aluno de OUTRO treinador; aluno lendo ficha de outro) → **403/404** fica nas suítes por-endpoint (ex.: `TreinoEndpointsTests`). O gate é o teste de autorização negativo, não a policy em si (policy sem teste negativo é invariante não-verificada).
- Cross-ref defense-in-depth: validação de segurança/compliance no SERVIDOR mesmo com trava no frontend ([specification-coding §4]).

## 10. ISOLAMENTO MULTI-AMBIENTE (PRD + HMG) [ALVO]
Hoje SÓ homolog existe (staging canônico). Produção é ALVO. Esta seção é o checklist de pré-condições de segurança PARA QUANDO prd subir — co-locado ou não. Mecânica de host/VM/compose: [specification-infrastructure §ISOLAMENTO-PRD-HMG]. Verdade-âncora: mesmo kernel + mesmo Docker daemon + mesmo Supabase project = blast radius compartilhado que NENHUMA config remove 100% — pra SaaS de pagamento (Stripe LIVE) + PII (LGPD) a postura defensável é project Supabase separado (obrigatório p/ os dados) e, de preferência, VM separada. Co-locar = ponte de custo curtíssima, tratando hmg como SEMI-confiável-em-direção-a-prd, nunca confiável.

### 10.1 Supabase / DB (elo crítico)
- ESTADO REAL hoje: prd=`public`, hmg=`homolog`, **MESMA DB, MESMO project** (1 dump cobre os 2 — [specification-db]/db-backup). Pra prd com PII+pagamento isto é anti-pattern.
- [ALVO/obrigatório] **Project Supabase SEPARADO por ambiente.** Razão não-contornável por schema: num project são COMPARTILHADOS (a) `anon` key e **`service_role` key** (service_role **IGNORA RLS** → vazar em hmg = ler/escrever TODA a base de prd), (b) mesma instância Postgres (hmg satura/derruba → prd cai), (c) mesmo pooler/backups. Separar project mata os 3 de uma vez.
- [ALVO] Se insistir em 1 project + schemas (INFERIOR, não fecha por causa do service_role compartilhado): roles distintos por ambiente least-privilege (`GRANT` só no schema próprio, `REVOKE` total no schema do outro); nenhum role de app com superuser/`postgres` na connection string; `search_path` fixado por role; **RLS ligado em TODAS as tabelas** (default-deny, não confiar em separação de schema); pooler users/connection strings separados + rotação independente; migration de hmg NUNCA toca `public` (guard no target do migrate).

### 10.2 Segredos — DISTINTOS por ambiente (nunca reusar)
- **Stripe**: prd = chaves **LIVE**, hmg = **test**. Misturar = cobrança real disparada por teste. Isolamento absoluto.
- `Auth:JwtSecret` (+ `JwtIssuer`/`Audience` por ambiente → token de hmg NÃO autentica em prd), `Mfa:EncryptionKey` (§5: reuso = mesmo keystream cross-ambiente), `Internal:ApiKey`, `DeliveryLog:RecipientHashKey`, Resend/WhatsApp tokens, cert A1 NFS-e — TODOS por ambiente. CORS de prd = só domínio de prd. Rotação independente (§9.1).

### 10.3 Cross-ref host/rede
Compose project separado, `mem_limit`/`cpus`/`pids_limit`, NÃO buildar-na-VM em prd (registry images), sem `docker.sock` em container de app, firewall 80/443-only + `/internal/*` não-público, `.env` 600 root-only sem valores compartilhados, idealmente rootless Docker/userns ou users Linux distintos — detalhado em [specification-infrastructure §ISOLAMENTO-PRD-HMG].

### 10.4 Mínimo inegociável p/ prd valer
1. Project Supabase próprio p/ prd (separa service_role/instância/backups). 2. Stripe LIVE isolado. 3. `mem_limit`/`cpus` + sem build-on-VM em prd. 4. `.env`/segredos 100% separados.
