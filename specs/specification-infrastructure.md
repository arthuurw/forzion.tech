# specification-infrastructure — infraestrutura & deploy (forzion.tech)

DOC PARA AGENTES. Fonte de verdade de hosting, containers, roteamento, SSL, CI/CD, deploy, ambientes e secrets. Formato denso, agent-oriented. Consultar antes de mexer em deploy, pipeline, nginx, SSL, env/secrets ou hosting. Cross-ref: [specification-db] (Supabase/schemas/connection/migrations), [specification-email] (Resend/webhook/Svix/nginx-webhook), [specification-security] (semgrep/zap/secrets/edge headers nginx), [specification-observability] (healthcheck/Sentry/lighthouse/smoke).

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA de mudança relevante em: provedor de hosting, compose, nginx, SSL, workflows CI/CD, fluxo de deploy, ambientes, mapeamento de env/secret.
- Vive em `specs/` (versionado; commitar). Não duplicar DB/e-mail — referenciar os specs próprios.

## TOPOLOGIA (hosting)
- **VPS Hostinger** (Ubuntu + Docker + docker-compose-plugin). Provisionada UMA VEZ via `scripts/setup-vm.sh` (instala Docker, cria `/opt/forzion/{app,nginx,certbot/conf,certbot/www}`, gera `.env` template). Produção será o MESMO modelo (VPS Hostinger).
- **Supabase**: PostgreSQL 17 gerenciado (`db.<ref>.supabase.co`). Schemas `homolog`/`develop`/`public` idênticos. Host direto IPv6-only → ops ad-hoc por host, não por container. Ver [specification-db].
- **DNS**: Hostinger (hPanel). E-mail: Resend usa `send.forzion.tech` (SPF+MX SES) + `resend._domainkey` (DKIM). Ver [specification-email].
- **Domínios**: `homologacao.forzion.tech` (hmg ATIVO), `pact.homologacao.forzion.tech` (Pact broker), `forzion.tech`/`www`/`app.forzion.tech` (prod PREPARADO/não-ativo).
- **Fluxo de request**: internet → nginx:443 → `location /` → frontend:3000 · `location /webhooks/` → backend:8080 · subdomínio pact → pact-broker:9292.

## STACKS COMPOSE
- **`docker-compose.homolog.yml`** — ATIVO (hmg). Build-on-VM. Serviços: `backend`(build Api/Dockerfile, `ASPNETCORE_ENVIRONMENT=Homolog`, env via `${...}`, healthcheck `GET /health:8080`), `frontend`(build, `depends_on backend healthy`), `nginx`(80/443, bind-mount `nginx.conf`+`certbot` ro), `certbot`(loop renew 12h), `pact-postgres`+`pact-broker`(:9292). Env ← `/opt/forzion/.env`.
- **`docker-compose.yml`** — LOCAL dev. `postgres`(postgres:16, schema `homolog` via Search Path) + `backend`(build, `Development`, :8080) + `frontend`(:3001→3000). Alternativa SEM Docker: `dotnet run` → User Secrets → **Supabase REMOTO** (não local; ⚠️ migra/seeda remoto — ver [specification-db]).
- **`docker-compose.server.yml`** — PREPARADO/NÃO-ATIVO. Deploy por imagem de registry (`${REGISTRY}/forzion/{backend,frontend}:${TAG}`). Hoje NÃO há CI que builde/pushe imagem; caminho previsto p/ prod.

## NGINX / ROTEAMENTO
- `nginx/nginx.conf` (bind-mount ro). `:80` → 301 https (+ `/.well-known/acme-challenge/` p/ ACME). `:443` TLS (Let's Encrypt).
- `location /webhooks/` → `http://backend:8080` (ANTES de `location /`). Necessário p/ headers `svix-*` crus (webhooks **Stripe** `/webhooks/stripe` E **Resend** `/webhooks/resend`). NÃO usar proxy SPA `/api/backend` p/ webhook (descarta headers). Ver [specification-email].
- `location /` → `http://frontend:3000` (proxy + ws upgrade). Backend NÃO tem outra rota pública (só via Next `/api/backend/[...path]` e `/api/auth/*`).
- **X-Forwarded-* / ForwardedHeaders**: nginx envia `X-Forwarded-For`/`-Proto`; o backend confia via `app.UseForwardedHeaders` (Homolog/Production, `RouteBuilderExtensions.cs`, antes de Auth/RateLimiter). `KnownNetworks`/`KnownProxies` limpos + `ForwardLimit=1` — confia no único hop porque o backend só é alcançável via nginx (rede docker isolada, sem porta publicada). Premissa de segurança: se o backend passar a ter porta exposta, isto vira vetor de spoofing de IP — restringir a `KnownNetworks` então. Afeta rate-limit (IP real do cliente) e scheme em reconstrução de URL. Ver [specification-security] §4.
- Server `pact.homologacao.forzion.tech:443` → `pact-broker:9292` (usa cert do domínio principal até `certbot --expand`).
- **X-Robots-Tag (homolog)**: server block `homologacao.forzion.tech:443` injeta `add_header X-Robots-Tag "noindex, nofollow" always;` — homolog é host público; evita indexação por buscadores. NÃO replicar no server de produção (forzion.tech, futuro). Defesa em profundidade com `robots.ts` env-gated no frontend. Ver [specification-security] §3 / [specification-seo] §4.1.

## SSL (Let's Encrypt / certbot)
- Bootstrap UMA VEZ: `scripts/init-ssl.sh <dominio> <email>` — sobe `nginx-init.conf` (HTTP-only p/ desafio ACME) → `certbot certonly --webroot` → `sed DOMAIN_PLACEHOLDER` no `nginx.conf` → `up -d` stack.
- Renovação: container `certbot` em loop (`certbot renew` a cada 12h). Volumes: `certbot/conf` (certs), `certbot/www` (webroot ACME).

## CI/CD (GitHub Actions)
- Trigger base: `push`/`pull_request` → `homolog` (salvo nota). **paths-ignore** (`**.md`,`docs/**`,`specs/**`) em `ci.yml`/`openapi-drift.yml`/`semgrep.yml` → PR/push só-docs NÃO dispara CI/CD.
- **`ci.yml`** (CI / CD — Homolog): commitlint(PR), backend unit (gates cobertura: Domain/Application 75% branch + 85% line/method; Api 85% line/70% method; `--filter Category!=Integration`), backend integração (Testcontainers/Docker; gates global 50% branch + Infrastructure 35% branch), frontend lint+typecheck+test, frontend build+storybook, security frontend (gitleaks/osv/npm-audit/license/sbom), security backend (vuln NuGet + SBOM CycloneDX), **Gate** (agrega needs), **Deploy→homolog** (só `push`).
- Outros workflows: `openapi-drift.yml` (regenera swagger offline vs `docs/api/swagger.v1.json`; drift=falha), `semgrep.yml` (SAST), `hygiene.yml` (madge/knip; paths frontend), `contract.yml`+`pact-provider.yml` (Pact; paths api/pact), `smoke.yml` (`workflow_run` pós CI/CD), `mutation.yml` (Stryker; schedule), `lighthouse.yml`/`zap.yml` (dispatch), `billing-renewal.yml` (cron `0 8 * * *` + `workflow_dispatch`; `curl -f POST https://${HOMOLOG_HOST}/internal/processar-renovacoes` com header `X-Internal-Key: ${INTERNAL_API_KEY}` → gera cobrança mensal das assinaturas com `DataProximaCobranca` vencida), `billing-reconciliation.yml` (cron `0 4 * * 1` — seg 04h UTC — + `workflow_dispatch` com input opcional `desde_utc`; `curl -f POST https://${HOMOLOG_HOST}/internal/reconciliar-pagamentos` com header `X-Internal-Key: ${INTERNAL_API_KEY}` e body `{}`/`{"desdeUtc":...}` → reconcilia eventos Stripe; `failure()` abre issue `ops`/`billing-reconciliation-failed`).
- Repo SEM branch protection (free tier privado) → checks NÃO-enforçados; `Gate` é agregador lógico (merge não trava por check ausente).
- **DEPLOY** (`Deploy→homolog`, `if: push`): SSH (`HOMOLOG_HOST`/`HOMOLOG_SSH_KEY`) → `cd /opt/forzion/app` → `git pull origin homolog` → `docker compose -f docker-compose.homolog.yml --env-file /opt/forzion/.env build && up -d --remove-orphans` → `restart nginx` (config é bind-mount; up não recria por mudança só de arquivo) → `docker image prune -f`.

## AMBIENTES
| Ambiente | ASPNETCORE_ENVIRONMENT | Schema | Host | Estado |
|----------|------------------------|--------|------|--------|
| local-docker | Development | homolog (PG local) | localhost | dev |
| local-run (`dotnet run`) | Development OU Homolog (profiles http/https forçam Homolog) | develop (Dev) / homolog (Homolog) — **Supabase REMOTO** | localhost | dev (⚠️ migra/seeda remoto) |
| homolog | Homolog | homolog | homologacao.forzion.tech (VPS Hostinger) | **ATIVO** |
| produção | Production | public | forzion.tech/www/app | **PREPARADO/NÃO-ATIVO** |
- Migrate + Seed no startup em Development/Homolog (`Program.cs`). Prod: `appsettings.Production.json` (AllowedHosts forzion.tech/www/app, schema `public`, CORS prod) existe, mas SEM deploy automatizado e sem CI de imagem.

## ENV / SECRETS
- **VM hmg** `/opt/forzion/.env` (NÃO versionado): `APP_ENV`, `DB_CONNECTION`, `DB_SCHEMA`, `JWT_SECRET`/`ISSUER`/`AUDIENCE`, `CORS_ORIGINS`, `STRIPE_SECRET_KEY`/`WEBHOOK_SECRET`/`URL_BASE`, `RESEND_API_KEY`/`WEBHOOK_SECRET`, `APP_FRONTEND_BASE_URL`, `INTERNAL_API_KEY` (chave dos endpoints internos `/internal/processar-renovacoes` e `/internal/reconciliar-pagamentos`; vazio → 401), WhatsApp (opcional), `PACT_*`. Compose mapeia → `Resend__ApiKey`, `Stripe__SecretKey`, `Internal__ApiKey`, etc.
- **GitHub Actions secrets**: `HOMOLOG_HOST`/`HOMOLOG_SSH_KEY` (deploy), `INTERNAL_API_KEY` (billing-renewal + billing-reconciliation; MESMO valor do `.env` da VM — comparação constant-time no endpoint).
- **Local**: User Secrets `forzion-prod` (`dotnet user-secrets`) — `ConnectionStrings:AppConnection` (Supabase remoto), `Auth:JwtSecret`, `Stripe:*`, `Resend:*`, `Seed:*`, `AI:Internal:*`. OU `.env` (ver `.env.example`) p/ `docker-compose.yml`.
- `appsettings.{Env}.json`: só defaults não-secret (AllowedHosts, Cors, Database:Schema, App:FrontendBaseUrl, Resend:ApiUrl). Secrets vazios no repo.
- ⚠️ `Program.cs` adiciona User Secrets DEPOIS do `CreateBuilder` → secrets sobrescrevem env em RUNTIME. Ver [specification-db].

## OBSERVABILITY
- Healthcheck backend `GET /health:8080` (compose + frontend `depends_on healthy`).
- Sentry (frontend, `withSentryConfig` em `next.config.ts`): source maps só com `SENTRY_AUTH_TOKEN`; RUM/replay via DSN (no-op sem DSN); CSP `connect-src https://*.sentry.io`.
- `smoke.yml`: smoke tests pós-deploy (`workflow_run` após CI/CD).

## INTEGRAÇÕES EXTERNAS
- Resend (e-mail + webhook entrega Svix) → [specification-email].
- Stripe (Connect + PaymentIntents/Pix). Webhook `/webhooks/stripe` (rota nginx `/webhooks/`).
- WhatsApp Meta Cloud API (opcional; `NullWhatsAppNotifier` se não configurado).
- Supabase (PostgreSQL gerenciado) → [specification-db].
- Pact Broker (contract testing self-hosted na VM, subdomínio `pact.`).

## ACESSO EXTERNO / WARP
- **Sintoma**: `connection refused` ao abrir `homologacao.forzion.tech` **só via Cloudflare WARP**; acesso direto (sem WARP) OK.
- **Path WARP**: client → CF edge (WireGuard/MASQUE) → egress CF (IP **compartilhado**, faixa Cloudflare) → origem. DNS resolvido via 1.1.1.1.
- **JÁ DESCARTADO (não reinvestigar)**: IPv6/AAAA (domínio **não tem** AAAA → SOA); nginx (IPv4-only, irrelevante sem AAAA); DNS (A=`2.24.104.224`, PTR `srv1678885.hstgr.cloud`, **AS47583 Hostinger** `2.24.64.0/18` LT — consistente em 1.1.1.1/8.8.8.8/local); origem (TCP 443/80 abertos fora WARP, TLS 1.3 cert LE `CN=homologacao.forzion.tech`); ufw do VM (sem firewall-as-code no repo). Falha isolada ao **egress WARP→AS47583**, não ao código.
- **Causas (ranqueadas)**: A) client WARP local (registro/MASQUE stale) → RST. B) proteção de rede **Hostinger upstream** (FORA do VM) dá RST nos egress WARP compartilhados → VM/log nginx limpos. C) roteamento/blackhole CF→prefixo Hostinger a partir do colo. D) MTU/MASQUE — **só se for timeout, não "refused"** (SYN é pequeno).
- **Decision-tree (WARP ligado)**: (1) `curl -v https://homologacao.forzion.tech` + `nc -vz 2.24.104.224 443` → fork **refused/RST** (A/B) vs **timed out** (C/D). (2) WARP modo **DNS-only**: funciona → problema é túnel/egress (DNS descartado). (3) egress em `https://www.cloudflare.com/cdn-cgi/trace/` (campo `ip`) → `docker compose logs nginx | grep <egress>`; **sem entrada = bloqueio upstream (B/C), fora do VM**. (4) 2º device no WARP funciona → causa A (local).
- **Remediação**: A → WARP *Reset registration* / toggle off-on / reinstalar. B → ticket Hostinger liberar faixas Cloudflare **ou** fronting CF. C → trocar colo (reconnect) / reportar à Cloudflare. D → baixar MTU do WARP.
- **Fix durável (NÃO feito — gate)**: fronting Cloudflare (orange cloud) — migra NS `forzion.tech`, trava firewall do VM às faixas CF, nginx `real_ip`/`CF-Connecting-IP`. Não adotado: homolog=staging, causa não confirmada, blast radius da **zona inteira** (Resend SPF/DKIM/MX, pact, prod-prep). Avaliar só se WARP-access virar requisito amplo/recorrente. ⚠️ NÃO adicionar `real_ip`/ufw-allowlist CF **antes** da migração (inerte ou quebra acesso direto; `CF-Connecting-IP` confiado sem CF na frente = spoof de IP).

## DICAS / GOTCHAS
- nginx.conf alterado → exige `restart nginx` (bind-mount; `up` não recria por arquivo).
- Env var nova/alterada no container → `up -d <svc>` (recreate); `restart` simples não recarrega env.
- PR só-docs não roda CI/CD (paths-ignore) → docs chegam na VM no próximo deploy de código (`git pull`). Merge só-docs em homolog NÃO dispara deploy.
- Supabase host direto IPv6-only → containers podem não alcançar por hostname; ver [specification-db].
- `docker-compose.server.yml` (registry) sem CI de build/push hoje → não usar como ativo sem antes criar o pipeline de imagem.
- Migration destrutiva/backfill roda no startup (Dev/Homolog) contra o REMOTO — validar antes (ver [specification-db]).
