# specification-infrastructure — infraestrutura & deploy (forzion.tech)

DOC PARA AGENTES. Fonte de verdade de hosting, containers, roteamento, SSL, CI/CD, deploy, ambientes e secrets. Formato denso, agent-oriented. Consultar antes de mexer em deploy, pipeline, nginx, SSL, env/secrets ou hosting. Cross-ref: [specification-db] (Supabase/schemas/connection/migrations), [specification-email] (Resend/webhook/Svix/nginx-webhook).

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
- Server `pact.homologacao.forzion.tech:443` → `pact-broker:9292` (usa cert do domínio principal até `certbot --expand`).

## SSL (Let's Encrypt / certbot)
- Bootstrap UMA VEZ: `scripts/init-ssl.sh <dominio> <email>` — sobe `nginx-init.conf` (HTTP-only p/ desafio ACME) → `certbot certonly --webroot` → `sed DOMAIN_PLACEHOLDER` no `nginx.conf` → `up -d` stack.
- Renovação: container `certbot` em loop (`certbot renew` a cada 12h). Volumes: `certbot/conf` (certs), `certbot/www` (webroot ACME).

## CI/CD (GitHub Actions)
- Trigger base: `push`/`pull_request` → `homolog` (salvo nota). **paths-ignore** (`**.md`,`docs/**`,`specs/**`) em `ci.yml`/`openapi-drift.yml`/`semgrep.yml` → PR/push só-docs NÃO dispara CI/CD.
- **`ci.yml`** (CI / CD — Homolog): commitlint(PR), backend unit (gates cobertura: Domain/Application 75% branch + 85% line/method; Api 85% line/70% method; `--filter Category!=Integration`), backend integração (Testcontainers/Docker; gates global 50% branch + Infrastructure 35% branch), frontend lint+typecheck+test, frontend build+storybook, security frontend (gitleaks/osv/npm-audit/license/sbom), security backend (vuln NuGet + SBOM CycloneDX), **Gate** (agrega needs), **Deploy→homolog** (só `push`).
- Outros workflows: `openapi-drift.yml` (regenera swagger offline vs `docs/api/swagger.v1.json`; drift=falha), `semgrep.yml` (SAST), `hygiene.yml` (madge/knip; paths frontend), `contract.yml`+`pact-provider.yml` (Pact; paths api/pact), `smoke.yml` (`workflow_run` pós CI/CD), `mutation.yml` (Stryker; schedule), `lighthouse.yml`/`zap.yml` (dispatch).
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
- **VM hmg** `/opt/forzion/.env` (NÃO versionado): `APP_ENV`, `DB_CONNECTION`, `DB_SCHEMA`, `JWT_SECRET`/`ISSUER`/`AUDIENCE`, `CORS_ORIGINS`, `STRIPE_SECRET_KEY`/`WEBHOOK_SECRET`/`URL_BASE`, `RESEND_API_KEY`/`WEBHOOK_SECRET`, `APP_FRONTEND_BASE_URL`, WhatsApp (opcional), `PACT_*`. Compose mapeia → `Resend__ApiKey`, `Stripe__SecretKey`, etc.
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

## DICAS / GOTCHAS
- nginx.conf alterado → exige `restart nginx` (bind-mount; `up` não recria por arquivo).
- Env var nova/alterada no container → `up -d <svc>` (recreate); `restart` simples não recarrega env.
- PR só-docs não roda CI/CD (paths-ignore) → docs chegam na VM no próximo deploy de código (`git pull`). Merge só-docs em homolog NÃO dispara deploy.
- Supabase host direto IPv6-only → containers podem não alcançar por hostname; ver [specification-db].
- `docker-compose.server.yml` (registry) sem CI de build/push hoje → não usar como ativo sem antes criar o pipeline de imagem.
- Migration destrutiva/backfill roda no startup (Dev/Homolog) contra o REMOTO — validar antes (ver [specification-db]).
