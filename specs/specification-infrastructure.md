# specification-infrastructure — infraestrutura & deploy (forzion.tech)

DOC PARA AGENTES. Fonte de verdade de hosting, containers, roteamento, SSL, CI/CD, deploy, ambientes e secrets. Formato denso, agent-oriented. Consultar antes de mexer em deploy, pipeline, nginx, SSL, env/secrets ou hosting. Cross-ref: [specification-db] (Supabase/schemas/connection/migrations), [specification-email] (Resend/webhook/Svix/nginx-webhook), [specification-security] (semgrep/zap/secrets/edge headers nginx), [specification-observability] (healthcheck/Sentry/lighthouse/smoke).

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA de mudança relevante em: provedor de hosting, compose, nginx, SSL, workflows CI/CD, fluxo de deploy, ambientes, mapeamento de env/secret.
- Não duplicar DB/e-mail — referenciar os specs próprios.

## TOPOLOGIA (hosting)
- **VPS Hostinger** (Ubuntu + Docker + docker-compose-plugin). Provisionada UMA VEZ via `scripts/setup-vm.sh` (instala Docker, cria `/opt/forzion/{app,nginx,certbot/conf,certbot/www}`, gera `.env` template). Produção será o MESMO modelo (VPS Hostinger).
- **Supabase**: PostgreSQL 17 gerenciado (`db.<ref>.supabase.co`). Schemas `homolog`/`develop`/`public` idênticos. App conecta via **Session pooler (:5432, IPv4)** (DR-01); host direto IPv6-only = ops ad-hoc/fallback (não alcançável de container). Ver [specification-db].
- **DNS**: Hostinger (hPanel). E-mail: Resend usa `send.forzion.tech` (SPF+MX SES) + `resend._domainkey` (DKIM). Ver [specification-email].
- **Domínios**: `homologacao.forzion.tech` (hmg ATIVO), `pact.homologacao.forzion.tech` (Pact broker), `forzion.tech`/`www`/`app.forzion.tech` (prod PREPARADO/não-ativo).
- **Fluxo de request**: internet → nginx:443 → `location /` → frontend:3000 · `location /webhooks/` → backend:8080 · subdomínio pact → pact-broker:9292.

## STACKS COMPOSE
- **`docker-compose.homolog.yml`** — ATIVO (hmg). Build-on-VM. Serviços: `backend`(build Api/Dockerfile, `ASPNETCORE_ENVIRONMENT=Homolog`, env via `${...}`, healthcheck `curl -f http://localhost:8080/health`), `frontend`(build, `depends_on backend healthy`), `nginx`(80/443, bind-mount `nginx.conf`+`certbot` ro), `certbot`(loop renew 12h), `pact-postgres`+`pact-broker`(:9292). Env ← `/opt/forzion/.env`.
- **`docker-compose.yml`** — LOCAL dev. `postgres`(postgres:16, schema `develop` via Search Path; criado no init por `scripts/init-develop-schema.sql`) + `backend`(build, `Development`, :8080) + `frontend`(:3001→3000). Alternativa SEM Docker: `dotnet run` → User Secrets → **Supabase REMOTO** (não local; ⚠️ migra/seeda remoto — ver [specification-db]). Receita comando-a-comando: §LOCAL-RUN.
- **`docker-compose.server.yml`** — PREPARADO/NÃO-ATIVO. Deploy por imagem de registry (`${REGISTRY}/forzion/{backend,frontend}:${TAG}`). Hoje NÃO há CI que builde/pushe imagem; caminho previsto p/ prod.

## NGINX / ROTEAMENTO
- `nginx/nginx.conf` (bind-mount ro). `:80` → 301 https (+ `/.well-known/acme-challenge/` p/ ACME). `:443` TLS (Let's Encrypt).
- `location /webhooks/` → `http://backend:8080` (ANTES de `location /`). Necessário p/ headers `svix-*` crus (webhooks **Stripe** `/webhooks/stripe` E **Resend** `/webhooks/resend`). NÃO usar proxy SPA `/api/backend` p/ webhook (descarta headers). Ver [specification-email].
- `location /` → `http://frontend:3000` (proxy + ws upgrade). Backend NÃO tem outra rota pública (só via Next `/api/backend/[...path]` e `/api/auth/*`).
- **X-Forwarded-* / ForwardedHeaders**: nginx envia `X-Forwarded-For`/`-Proto`; o backend confia via `app.UseForwardedHeaders` (Homolog/Production, `RouteBuilderExtensions.UseApiConfiguration`, antes de Auth/RateLimiter). `KnownNetworks`/`KnownProxies` limpos + `ForwardLimit=1` — confia no único hop porque o backend só é alcançável via nginx (rede docker isolada, sem porta publicada). Premissa de segurança: se o backend passar a ter porta exposta, isto vira vetor de spoofing de IP — restringir a `KnownNetworks` então. Afeta rate-limit (IP real do cliente) e scheme em reconstrução de URL. Ver [specification-security] §4.
- Server `pact.homologacao.forzion.tech:443` → `pact-broker:9292` (usa cert do domínio principal até `certbot --expand`).
- **X-Robots-Tag (homolog)**: server block `homologacao.forzion.tech:443` injeta `add_header X-Robots-Tag "noindex, nofollow" always;` — homolog é host público; evita indexação por buscadores. NÃO replicar no server de produção (forzion.tech, futuro). Defesa em profundidade com `robots.ts` env-gated no frontend. Ver [specification-security] §3 / [specification-seo] §4.1.

## SSL (Let's Encrypt / certbot)
- Bootstrap UMA VEZ: `scripts/init-ssl.sh <dominio> <email>` — sobe `nginx-init.conf` (HTTP-only p/ desafio ACME) → `certbot certonly --webroot` → `sed DOMAIN_PLACEHOLDER` no `nginx.conf` → `up -d` stack.
- Renovação: container `certbot` em loop (`certbot renew` a cada 12h). Volumes: `certbot/conf` (certs), `certbot/www` (webroot ACME).

## CI/CD (GitHub Actions)
- Trigger base: `push`/`pull_request` → `homolog` (salvo nota). **paths-ignore** (`**.md`,`docs/**`,`specs/**`) em `ci.yml`/`openapi-drift.yml`/`semgrep.yml` → PR/push só-docs NÃO dispara CI/CD.
- **`ci.yml`** (CI / CD — Homolog): jobs `changes` (path-filter, ver abaixo), commitlint(PR), backend unit, backend integração (Testcontainers/Docker), frontend lint+typecheck+test, frontend build+storybook, security frontend (gitleaks/osv/npm-audit/license/sbom), security backend (vuln NuGet + SBOM CycloneDX), **Gate** (agrega needs), **Deploy→homolog** (só `push`). Lista de jobs do `gate` + thresholds de cobertura: CANÔNICO em [specification-tests] §7/§8 (não duplicar aqui).
- **Cobertura consolidada** (unit e integração): cada job de teste backend roda `dotnet test` **1×** coletando cobertura (cobertura.xml), gera `ReportGenerator -reporttypes:JsonSummary` e avalia TODOS os thresholds do relatório único via `scripts/check-coverage.sh` (bash+jq; `<Assembly>:line=,branch=,method=` ou `__overall__:branch=`). Antes cada threshold re-rodava `dotnet test` (unit 6×, integração 2×). Thresholds inalterados.
- **Path-filter** (`changes` job, `dorny/paths-filter@v3`): outputs `backend`/`frontend`/`ci`. Jobs backend (unit/integração/security-backend) rodam só se `backend||ci`; jobs frontend (test/build) só se `frontend||ci`; `security` (inclui gitleaks na árvore toda) roda se `frontend||backend||ci`. Mudança em `.github/workflows/**` ou `scripts/check-coverage.sh` (`ci`) força tudo. `changes` casca a maioria dos jobs em PR de área única.
- **Gate** com `if: always()` + agregação por `needs.*.result`: job PULADO por path-filter NÃO reprova; só `failure`/`cancelled` reprovam. `deploy-homolog` continua `needs:[gate]` + `if: push`.
- Outros workflows: `openapi-drift.yml` (regenera swagger offline vs `docs/api/swagger.v1.json`; drift=falha), `semgrep.yml` (SAST), `hygiene.yml` (madge/knip; paths frontend), `contract.yml`+`pact-provider.yml` (Pact; paths api/pact), `smoke.yml` (`workflow_run` pós CI/CD), `mutation.yml` (Stryker; schedule), `lighthouse.yml`/`zap.yml` (dispatch).
- **Workflows de billing** (3 endpoints internos, cron + `workflow_dispatch`; `curl -f POST https://${HOMOLOG_HOST}/<rota>` com header `X-Internal-Key: ${INTERNAL_API_KEY}`; `failure()` abre issue):
  - `billing-renewal-treinador.yml` (cron `0 8 * * *`) → `/internal/processar-renovacoes-treinador` (renovação mensal de planos de treinadores); issue labels `ops`/`billing-renewal-treinador-failed`.
  - `billing-renewal.yml` (cron `0 8 * * *`) → `/internal/processar-renovacoes` (cobrança mensal de assinaturas com `DataProximaCobranca` vencida).
  - `billing-reconciliation.yml` (cron `0 4 * * 1` — seg 04h UTC — input opcional `desde_utc`; body `{}`/`{"desdeUtc":...}`) → `/internal/reconciliar-pagamentos` (reconcilia eventos Stripe); issue labels `ops`/`billing-reconciliation-failed`.
- **GOTCHA CRÍTICO — `schedule`/`workflow_dispatch` SÓ disparam da branch DEFAULT** (regra GitHub). Default real = `main` (confirmado `gh repo view`), que hoje só tem `ci.yml`+CodeQL; TODOS os workflows de cron vivem em `homolog` (não-default) → **não disparam de fato** (`lgpd-purge`, `mutation`, os 3 `billing-*`; e `db-backup`, na branch de feature). Prova: `gh run list --workflow=lgpd-purge.yml` → `HTTP 404: workflow ... not found on the default branch`. Workflows de `push`/`pull_request` (ci, semgrep, contract, hygiene, openapi-drift, pact) rodam normal (o evento traz o `.yml` da própria branch homolog) — por isso aparecem "active". **IMPLICAÇÃO REAL**: renovação mensal de assinatura, purga LGPD e backup NÃO rodam agendados hoje. Fix = pôr os crons na branch default. `db-backup` é **DB-level** (prod=`public`/staging=`homolog` na mesma DB → 1 dump cobre os 2, independe da branch); `billing-*`/`lgpd-purge` são **env-level** (`curl` host específico via `HOMOLOG_HOST`) → na default precisam apontar p/ o host do ambiente certo (no cutover de prod = `main`, host de produção). Decisão de roteamento (default→homolog agora vs promover crons p/ `main`) — ver [STATE].
- Repo SEM branch protection → checks NÃO-enforçados; `Gate` é agregador lógico (merge não trava por check ausente).
- **DEPLOY** (`Deploy→homolog`, `if: push`): SSH (`HOMOLOG_HOST`/`HOMOLOG_SSH_KEY`) → `cd /opt/forzion/app` → `git pull origin homolog` → guarda imagens atuais (rollback) → `build` → **dry-run migrate** (`scripts/migrate-dryrun.sh` + `docker-compose.dryrun.yml`: cópia do schema homolog real) → **migrate real one-shot** (`compose run --rm --no-deps backend migrate` — ENTRYPOINT já é `dotnet forzion.tech.Api.dll`) ANTES do `up -d` → `up -d --remove-orphans` → **health-gate** (`/health`+`/health/ready` por dentro do container; reprovou → rollback re-tag `:previous` + `up -d --no-build`, exit 1) → `restart nginx` (bind-mount; up não recria por mudança só de arquivo) → `docker image prune -f`. Gates A–D (deploy-safety): [specification-dr] §4.
- **DEPLOY MANUAL / GOTCHAS** (quando o CI falha ou ao aplicar mudança de `.env` na mão):
  - `docker compose restart` **NÃO** relê o `.env` (reinicia o processo com env antiga). Pra carregar var nova: `up -d --force-recreate <serviço>`. Sintoma: `docker compose exec <svc> printenv VAR` vazio mesmo com a var no `/opt/forzion/.env`.
  - `git pull` na VM precisa ser **root** — remote é `git@github.com` (SSH); user interativo dá `Permission denied (publickey)` (deploy key só no contexto root/CI).
  - `.env` em `/opt/forzion/.env`; compose roda em `/opt/forzion/app` (sempre `--env-file /opt/forzion/.env`).
  - CI `Deploy→homolog` pode quebrar com `dial tcp ***:22: i/o timeout` (SSH runner→VM: firewall/IP allowlist/oscilação) com build/testes verdes — só o step "Deploy na VM" falha. Fallback: deploy manual (pull root + `up -d --force-recreate`) ou `gh run rerun --job <id>`.

## AMBIENTES
| Ambiente | ASPNETCORE_ENVIRONMENT | Schema | Host | Estado |
|----------|------------------------|--------|------|--------|
| local-docker | Development | develop (PG local) | localhost | dev |
| local-run (`dotnet run`) | Development (recomendado; profiles http/https forçam Homolog — §LOCAL-RUN) | **develop** (= `Search Path` do User Secret; FIXO — NÃO muda com o env) — **Supabase REMOTO** | localhost | dev (⚠️ migra/seeda remoto) |
| homolog | Homolog | homolog | homologacao.forzion.tech (VPS Hostinger) | **ATIVO** |
| produção | Production | public | forzion.tech/www/app | **PREPARADO/NÃO-ATIVO** |
- Migrate + Seed no startup **só em Development** (`MigrationStartup.ShouldAutoMigrateOnBoot`). Homolog/Prod: migrate DESACOPLADO do boot (deploy-safety R1) — aplicado pelo step `app migrate` (modo CLI one-shot) pré-deploy; boot normal NÃO toca DDL. Prod: `appsettings.Production.json` (AllowedHosts forzion.tech/www/app, schema `public`, CORS prod) existe, mas SEM deploy automatizado e sem CI de imagem.
- **Schema NÃO é função do env**: vem do `Search Path` da connection. No local-run há UM só User Secret store (`ConnectionStrings:AppConnection` com `Search Path=develop`), carregado tanto em Development quanto em Homolog (`Program.cs` L8). Logo Dev vs Homolog local muda só os defaults de `appsettings.{Env}.json` (Email markers de teste, CORS, AllowedHosts) — **não** o schema. Pra apontar a outro schema, editar o `Search Path` do próprio secret.

## LOCAL-RUN SEM DOCKER (receita — Development + schema develop, Supabase REMOTO)
Caminho mais rápido p/ subir a instância p/ testar à mão (sem Docker). ⚠️ migra/seeda o Supabase REMOTO (schema `develop`) no startup — ver [specification-db].
- **Pré**: User Secrets do projeto `forzion.tech.Api` preenchidos (`ConnectionStrings:AppConnection` com `Search Path=develop`, `Auth:JwtSecret`, `Stripe:*`, `Seed:*`) — `dotnet user-secrets list --project forzion.tech.Api`.
- **GOTCHA launch-profile**: `dotnet run` puro pega o profile `http`/`https` do `launchSettings.json`, que **força `ASPNETCORE_ENVIRONMENT=Homolog`** (não Development). Pra rodar Development, bypassar o profile e setar env+URL na mão:
  ```powershell
  $env:ASPNETCORE_ENVIRONMENT="Development"; $env:ASPNETCORE_URLS="http://localhost:5230"
  dotnet run --project forzion.tech.Api --no-launch-profile
  ```
  Backend sobe em `http://localhost:5230`. (HTTPS :7220 só vale via profile `https`, que é Homolog — evitar p/ não lidar com cert dev.)
- **Frontend** precisa de `frontend/.env.local` (gitignored; NÃO commitar) — sem ele o proxy aponta p/ o default `https://localhost:7220`, que não casa com o backend HTTP:
  ```
  API_BASE_URL=http://localhost:5230          # proxy Next /api/backend + /api/auth/* → backend
  JWT_SECRET=<MESMO valor de Auth:JwtSecret>  # /api/auth/me valida a assinatura do JWT
  JWT_ISSUER=forzion.tech
  JWT_AUDIENCE=forzion.tech
  NEXT_PUBLIC_API_BASE_URL=/api/backend
  ```
  Subir: `cd frontend; npm run dev` → `:3000` (ou `npm run dev -- -p 3001` se `:3000` ocupado — `next dev` NÃO migra de porta sozinho no Next 16; falha `EADDRINUSE`).
- **Verificar**: `/health` 200 (liveness), `/health/ready` 200 (conexão Supabase OK). Admin = `Seed:AdminEmail`/`Seed:AdminPassword` do User Secret (pré-verificado pelo seed → login direto).
- **Cross-check Docker**: a stack `docker-compose.yml` é o caminho alternativo (PG local schema `develop`, sem tocar Supabase) — frontend :3001, ver [specification-local-ci-repro] §4.

## ENV / SECRETS
- **VM hmg** `/opt/forzion/.env` (NÃO versionado): `APP_ENV`, `DB_CONNECTION`, `DB_SCHEMA`, `JWT_SECRET`/`ISSUER`/`AUDIENCE`, `CORS_ORIGINS`, `STRIPE_SECRET_KEY`/`WEBHOOK_SECRET`/`URL_BASE`, `RESEND_API_KEY`/`WEBHOOK_SECRET`, `APP_FRONTEND_BASE_URL`, `INTERNAL_API_KEY` (chave dos 3 endpoints internos `/internal/processar-renovacoes-treinador`, `/internal/processar-renovacoes`, `/internal/reconciliar-pagamentos`; vazio → 401), WhatsApp (opcional), `PACT_*`. Compose mapeia → `Resend__ApiKey`, `Stripe__SecretKey`, `Internal__ApiKey`, etc.
- **GitHub Actions secrets**: `HOMOLOG_HOST`/`HOMOLOG_SSH_KEY` (deploy), `INTERNAL_API_KEY` (billing-renewal-treinador + billing-renewal + billing-reconciliation; MESMO valor do `.env` da VM — comparação constant-time no endpoint).
- **GitHub Actions secrets — `db-backup.yml`** (backup diário, [specification-dr §2]): `BACKUP_DATABASE_URL` (URI libpq do Session pooler, `postgresql://forzion_api.<ref>:<pw>@<pooler-host>:5432/postgres?sslmode=require` — URL-encode `<pw>` se tiver char especial), `BACKUP_AGE_PUBLIC_KEY` (recipient `age1…`; privada NÃO vai pro GitHub — fica offline com o dono), `R2_ACCOUNT_ID`/`R2_ACCESS_KEY_ID`/`R2_SECRET_ACCESS_KEY`/`R2_BUCKET` (Cloudflare R2, S3-compat). Fork PR não acessa secrets (sem trigger de PR + fork-pr approval ON, [go-public]).
- **Local**: User Secrets `forzion-prod` (`dotnet user-secrets`) — `ConnectionStrings:AppConnection` (Supabase remoto), `Auth:JwtSecret`, `Stripe:*`, `Resend:*`, `Seed:*`, `AI:Internal:*`. OU `.env` (ver `.env.example`) p/ `docker-compose.yml`.
- `appsettings.{Env}.json`: só defaults não-secret (AllowedHosts, Cors, Email[markers de teste], App:FrontendBaseUrl, Resend:ApiUrl). Schema NÃO sai daqui — vem do `Search Path` da connection (§AMBIENTES). Secrets vazios no repo.
- ⚠️ `Program.cs` adiciona User Secrets DEPOIS do `CreateBuilder` → secrets sobrescrevem env em RUNTIME. Ver [specification-db].

## OBSERVABILITY
- Healthcheck backend `curl -f http://localhost:8080/health` (compose + frontend `depends_on healthy`). Liveness puro; readiness `/health/ready` (DbContextCheck) — ver [specification-observability] §2.
- Sentry (frontend, `withSentryConfig` em `next.config.ts`): source maps só com `SENTRY_AUTH_TOKEN`; RUM/replay via DSN (no-op sem DSN); CSP `connect-src https://*.sentry.io`.
- `smoke.yml`: smoke tests pós-deploy (`workflow_run` após CI/CD).

## INTEGRAÇÕES EXTERNAS
- Resend (e-mail + webhook entrega Svix) → [specification-email].
- Stripe (Connect + PaymentIntents/Pix). Webhook `/webhooks/stripe` (rota nginx `/webhooks/`).
- WhatsApp Meta Cloud API (opcional; `NullWhatsAppNotifier` se não configurado).
- Supabase (PostgreSQL gerenciado) → [specification-db].
- Pact Broker (contract testing self-hosted na VM, subdomínio `pact.`).

## ACESSO EXTERNO / WARP
- **Sintoma + causa**: `connection refused` ao abrir `homologacao.forzion.tech` **só via Cloudflare WARP** (direto OK). Falha isolada ao egress WARP→AS47583 Hostinger (`2.24.104.224`), não ao código/VM (TCP 443/80 abertos, cert LE OK, nginx/VM logs limpos fora WARP). Causa provável: client WARP stale (RST local) ou proteção upstream Hostinger nos egress CF compartilhados. Descartados (não reinvestigar): IPv6/AAAA (sem AAAA), DNS, origem, ufw.
- **Triagem rápida**: WARP modo DNS-only funciona ⇒ é túnel/egress. Egress (`cdn-cgi/trace`) ausente nos logs nginx ⇒ bloqueio upstream (fora do VM). 2º device WARP OK ⇒ causa local → *Reset registration*.
- **Fix durável (NÃO feito — gate)**: fronting Cloudflare (orange cloud) não adotado — blast radius da zona inteira (Resend SPF/DKIM/MX, pact, prod-prep), homolog=staging, causa não confirmada. ⚠️ NÃO adicionar `real_ip`/ufw-allowlist CF antes de migrar NS (inerte ou quebra acesso direto; `CF-Connecting-IP` sem CF na frente = spoof de IP).

## DICAS / GOTCHAS
(restart-nginx por bind-mount e `up -d` por env-var: ver §DEPLOY MANUAL / GOTCHAS — não repetir.)
- PR/merge só-docs não roda CI/CD (paths-ignore) → docs chegam na VM no próximo deploy de código (`git pull`), sem deploy próprio.
- Supabase host direto IPv6-only → containers podem não alcançar por hostname; ver [specification-db].
- `docker-compose.server.yml` (registry) sem CI de build/push hoje → não usar como ativo sem antes criar o pipeline de imagem.
- (Migration destrutiva no startup contra REMOTO: ver §STACKS/§AMBIENTES/§LOCAL-RUN — não repetir.)
