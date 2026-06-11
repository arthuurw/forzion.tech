# specification-dr — backup, restore & disaster recovery (forzion.tech)

DOC PARA AGENTES. Continuidade: backup, restore, rollback de deploy, runbook de incidente. Infra de deploy (VM Hostinger/docker-compose/nginx/SSL) → [specification-infrastructure]; esta spec é o "e quando quebra / perde dado?". Ler antes de mexer em backup, migration destrutiva, ou processo de deploy. Formato denso. ATENÇÃO: várias entradas são ALVO/aspiracional (não implementado) — marcadas [ALVO]; o que existe é marcado [EXISTE].

## MANUTENÇÃO
- Atualizar ao mudar provider de banco, cadência de backup, ou processo de deploy. Resolver um [ALVO] → mover p/ [EXISTE] com a referência concreta.

## 1. BACKUP (Supabase / PG 17)
- [EXISTE] Projeto Supabase único `forzion` (ref `fdpdbtiuuitndbeujcbj`, sa-east-1, PG 17.6.1.104), org `forzion.tech` — **tier FREE** (confirmado 2026-06-10 via Management API). **Free = ZERO backup gerenciado, SEM PITR** ([docs Supabase](https://supabase.com/docs/guides/platform/backups): recomenda `supabase db dump`/pg_dump manual + cópia off-site). Não há backup automático hoje → RPO real = ∞ até o pipeline lógico de §2 rodar. Pro = daily backup (retenção 7d) + PITR add-on (RPO<1min, exige compute add-on ≥Small).
- [EXISTE] 3 schemas (`homolog` canônico, `develop`, `public`) no MESMO banco — backup do banco cobre os 3; restore seletivo por schema exige cuidado ([specification-db] ownership diverge por schema).
- [EXISTE] `ai_token_usage` é NON-EF (criada fora de migration) — restore de estrutura precisa recriá-la (`CREATE TABLE ... LIKE homolog.ai_token_usage INCLUDING ALL`), [specification-db].

## 2. RESTORE — DRILL (backup não testado = sem backup)
- [EXISTE] No FREE não há backup gerenciado p/ "baixar" → o drill PROVA o pipeline de dump lógico, que NO FREE É o próprio mecanismo de backup. O mesmo procedimento serve de restore real em incidente.
- [EXISTE] Version-match: cliente PG local = 16 e 18 (NÃO 17); servidor = 17.6 → fazer dump E restore pelo MESMO container `postgres:17` zera o skew ([specification-db]). Host direto `db.<ref>.supabase.co` é IPv6-only → dump via **Session pooler (IPv4, porta 5432)**; **Transaction pooler :6543 NÃO suporta pg_dump** (sem sessão).
- [EXISTE] RUNBOOK (PowerShell/Windows; Docker rodando). Senha do source via `$env:SRC_PW` setada no shell — NUNCA inline/commit/echo. Host+user EXATOS do Dashboard → Connect → Session pooler (`user = postgres.<ref>`):
  ```
  $drill="C:\temp\drill"; New-Item -ItemType Directory -Force $drill | Out-Null
  # alvo descartável PG17
  docker run -d --name drill-pg17 -e POSTGRES_PASSWORD=drill -p 55432:5432 postgres:17
  # dump do source (-n homolog = só o schema canônico; sem -n = os 3 schemas)
  docker run --rm -e PGPASSWORD=$env:SRC_PW -v "${drill}:/out" postgres:17 `
    pg_dump -h <SESSION_POOLER_HOST> -p 5432 -U postgres.fdpdbtiuuitndbeujcbj -d postgres `
    -n homolog -Fc -f /out/homolog.dump
  # restore no alvo (host.docker.internal alcança a porta publicada)
  docker run --rm -e PGPASSWORD=drill -v "${drill}:/out" postgres:17 `
    pg_restore -h host.docker.internal -p 55432 -U postgres -d postgres `
    --no-owner --no-privileges /out/homolog.dump
  # validar (socket local = trust, sem senha)
  docker exec drill-pg17 psql -U postgres -d postgres -c 'SELECT count(*) FROM homolog."__EFMigrationsHistory";'  # esperado 32
  docker exec drill-pg17 psql -U postgres -d postgres -c 'SELECT count(*) FROM homolog.contas;'
  # teardown
  docker rm -f drill-pg17; Remove-Item -Recurse -Force $drill; Remove-Item Env:\SRC_PW
  ```
- [EXISTE] Variante validada (drill 2026-06-11): dump do HOST com `pg_dump 18` local (`C:\Program Files\PostgreSQL\18\bin`) contra o host direto (Windows resolve IPv6) → `pg_restore 18` no `postgres:17` em `localhost:55432`. Dispensa o pooler; client v18 ≥ server v17 (forward-compat) OK. GOTCHAS: identificador case-sensitive `"__EFMigrationsHistory"` só sobrevive via stdin (`$sql | docker exec -i … psql`), não por `-c` (PowerShell/docker comem as aspas → vira lowercase → "does not exist"); colunas EF = snake_case (`migration_id`).
- [EXISTE — automação] `.github/workflows/db-backup.yml` (cron diário 06:00 UTC + `workflow_dispatch`): runner GitHub instala `postgresql-client-17`+`age`, roda `pg_dump -Fc` do schema (`BACKUP_SCHEMA=homolog`) via Session pooler (secret `BACKUP_DATABASE_URL`, user `forzion_api.<ref>`:5432), CRIPTOGRAFA com `age` (chave pública `BACKUP_AGE_PUBLIC_KEY`; privada fica offline com o dono — sem ela o dump é inútil) e sobe p/ Cloudflare R2 (`R2_*`, S3-compat, egress grátis). Dump em claro NUNCA persiste (PII/LGPD): `rm` pós-cifra; guarda de tamanho <1KB aborta antes de subir lixo; falha → issue `ops`/`db-backup-failed`. **Off-site real** (domínio de falha ≠ Supabase, ≠ repo; repo é PÚBLICO → dump JAMAIS em git/artifact). Decisão: Free + cron sobre Pro (usuário, 2026-06-11). Pré-req operacional (usuário, 1×): criar bucket R2 + par de chaves `age` + setar os 6 secrets (`BACKUP_DATABASE_URL`, `BACKUP_AGE_PUBLIC_KEY`, `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_BUCKET`) + lifecycle de retenção no bucket (ex. 30d) + `workflow_dispatch` 1× p/ validar.
- [EXISTE — restore do backup automático] Baixar+decifrar+restaurar: `aws s3 cp s3://<bucket>/<arq>.dump.age . --endpoint-url https://<acct>.r2.cloudflarestorage.com` → `age -d -i <chave-privada-age> <arq>.dump.age > restore.dump` → `pg_restore --no-owner --no-privileges -d <conn-alvo> restore.dump` (alvo = `postgres:17` descartável p/ teste, ou banco real em incidente). `-Fc` permite restore seletivo (`-t`/`-n`).
- [EXISTE] Cadência: dump automático DIÁRIO (workflow acima, assim que os secrets forem setados); drill de restore manual (§RUNBOOK) p/ provar que o backup volta. Resultado de cada execução em §DRILL LOG.

## 3. RTO / RPO (definir alvo de negócio)
- [EXISTE — estado] Free, sem backup gerenciado. Sem o workflow: **RPO = ∞** (perda total possível — nenhum ponto de restauração). Com `db-backup.yml` ativo (secrets setados): **RPO ≤ 24h** (intervalo do cron diário) — risco DR-02 mitigado p/ o nível aceito na decisão Free+cron (usuário, 2026-06-11). RTO = manual/indefinido. RPO<5min (PITR) só viria com Supabase Pro (Fase 2, não escolhido).
- [ALVO] RTO/RPO alvo definidos com o negócio. Dado financeiro (pagamentos/assinaturas) tolera pouco RPO → alvo RPO<5min exige Pro+PITR (Fase 2).

## 4. ROLLBACK DE DEPLOY
- [EXISTE] Deploy homolog = `docker compose build/up` na VM via SSH ([specification-infrastructure]). Rollback de CÓDIGO: re-deploy da imagem/tag anterior.
- [EXISTE risco] `Program.cs` roda `MigrateAsync`+`SeedAsync` no startup em Dev/Homolog contra o REMOTO ([specification-db §ACESSOS]) — migration aplicada NÃO volta sozinha no rollback de imagem.
- [ALVO] Schema forward-compatible (expand/contract, [specification-db §BACKFILL]) habilita rollback de código SEM rollback de schema — a regra que torna deploy revertível. Migration destrutiva sem janela expand/contract trava o rollback → exige backup verificado (§1) ANTES.

## 5. RUNBOOK DE INCIDENTE
- [ALVO] Passos mínimos a documentar concretamente: DETECTAR (`/health`+`/health/ready` liveness/readiness + log→DB `error_logs` + relatório diário de saúde, [specification-observability]) → CONTER (parar o efeito; ex. desabilitar renovações via `/internal` ou flag) → COMUNICAR → RESTAURAR (§2/§4) → POST-MORTEM (alimenta [specification-coding] se for bug de classe nova).
- [EXISTE parcial] Sinais de detecção já existem (health endpoints, error_logs, health_snapshots); falta o runbook que os amarra em procedimento.

## 6. ROADMAP DE HA (ALVO — não implementado; fases serão marcadas [EXISTE] ao concluir)

Estado real: 1 VPS (backend+frontend+nginx) · 1 Supabase Free, 1 região, sem replica · conexão via **Session pooler :5432 ATIVO** (DR-01, flip+verificado 2026-06-11; `pg_stat_activity` confirma `application_name=Supavisor`) · DNS sem failover · **Free = SEM backup gerenciado/PITR** (confirmado), drill feito 2026-06-11 (ver §DRILL LOG) · RTO=manual, **RPO=∞** (nenhum backup automático).
Alvo SaaS financeiro: RTO<15min (processo)/<4h (VM); RPO<5min · ≥2 instâncias de app + LB · Supabase Pro+PITR+replica · drill trimestral.

### Fase 1 — Quick-wins sem downtime (1–2 dias) [parcialmente em execução — DR-01/02]
- [FEITO 2026-06-11 — DR-01] Conexão runtime via **Session pooler Supabase (:5432, IPv4)** em vez de direct (IPv6-only): pooling de conexão + IPv4, drop-in SEM código (session suporta migration/prepared stmt → `MigrateAsync` no boot intacto). Transaction :6543 descartado (quebraria migration no boot). Flip aplicado na VM + verificado (`/health/ready`=Healthy; `pg_stat_activity.forzion_api` com `application_name=Supavisor`). Detalhe canônico: [specification-db] §DICAS.
- [ALVO/em execução] Documentar tier Supabase atual + janela/retenção real de backup em §1 desta spec.
- [ALVO/em execução] 1º restore drill real (projeto temp Supabase · container `postgres:17` · validar `__EFMigrationsHistory`=32 + contagem `contas`) e resultado documentado (§2).

### Fase 2 — Supabase Pro, PITR e read-offloading (1–2 semanas) [ALVO]
- [ALVO] Upgrade para Supabase Pro → habilita PITR (Point-in-Time Recovery, RPO<1min) + read-replica na mesma região sa-east-1.
- [ALVO] `AppDbContextReadOnly`: segundo `DbContext` registrado como `Scoped`, apontando à read-replica (connection string com `Search Path` idêntico, sem `MigrateAsync`/`SeedAsync`). Handlers de leitura pesada (listagens de billing, relatório de saúde, cron de reconciliação) injetam `AppDbContextReadOnly`; handlers de mutação continuam no `AppDbContext` primário. NÃO chamar `MigrateAsync`/`SeedAsync` na instância read-only.
- [ALVO] Runbook de failover manual (ver §7 abaixo).
- [ALVO] Drill de restore mensal agendado (cadência mínima enquanto não há automatização).

### Fase 3 — 2ª VPS, Load Balancer e outbox multi-host (2–4 semanas) [ALVO]
- [ALVO] 2ª VPS Hostinger idêntica à 1ª (mesmo `setup-vm.sh`, mesma stack compose); DNS round-robin ou LB (nginx upstream / Cloudflare LB) distribuindo tráfego entre as duas VMs.
- [ALVO] Deploy sincronizado: CI `deploy-homolog` faz SSH nas 2 VMs sequencialmente (`git pull` + `up -d --remove-orphans`); se 1 falhar, alertar sem derrubar a outra.
- [ALVO] Outbox multi-host: `OutboxRepository.ObterProcessaveisAsync` já usa `FOR UPDATE SKIP LOCKED` (confirmado — ver §6.1). Com 2 VMs cada uma rodando o worker de outbox, `SKIP LOCKED` garante que itens travados pela VM-A são pulados pela VM-B, sem processamento duplo.
- [ALVO] Drill mensal automatizado: GitHub Actions `workflow_dispatch` + cron que restaura snapshot num projeto Supabase temporário e valida contagem de tabelas.

### §6.1 — Confirmação SKIP LOCKED (OutboxRepository)
[EXISTE] `OutboxRepository.ObterProcessaveisAsync` em `forzion.tech.Infrastructure/Persistence/Repositories/OutboxRepository.cs` emite:
```sql
SELECT * FROM outbox_efeitos
WHERE status = {status}
  AND proxima_tentativa <= {agora}
ORDER BY proxima_tentativa
LIMIT {max}
FOR UPDATE SKIP LOCKED
```
via `FromSqlInterpolated` (linha 18). Workers concorrentes (múltiplas VMs ou múltiplos hosted-services) pularão itens já travados por outra transação — sem efeito 2×. Pré-requisito já satisfeito para a Fase 3.

## 7. RUNBOOK DE FAILOVER MANUAL (ALVO — procedimento operador, ~10–15 min)

Acionar quando: `/health` ou `/health/ready` falha persistentemente na VM primária, ou VM inacessível via SSH. Pré-requisito: acesso SSH à VM secundária (Fase 3) e acesso ao Supabase Dashboard.

### Passo 1 — Confirmar falha (2 min)
1. `curl -f https://homologacao.forzion.tech/health` — se timeout/5xx por >2min, prosseguir.
2. SSH na VM primária: `docker compose -f docker-compose.homolog.yml ps` — verificar se containers caíram.
3. Se VM inacessível (SSH timeout): pular para Passo 3.

### Passo 2 — Tentar recuperação da VM primária (3 min)
1. SSH root na VM primária: `cd /opt/forzion/app && docker compose -f docker-compose.homolog.yml up -d --remove-orphans`.
2. Aguardar healthcheck (`docker compose ps` → `healthy`). Se OK, encerrar runbook — falha foi transiente.
3. Se ainda falha: prosseguir para Passo 3.

### Passo 3 — Failover para VM secundária (5 min) [ALVO — exige Fase 3]
1. SSH root na VM secundária: confirmar que o deploy mais recente está aplicado (`git -C /opt/forzion/app log -1`).
2. Se desatualizado: `cd /opt/forzion/app && git pull origin homolog && docker compose -f docker-compose.homolog.yml up -d --remove-orphans`.
3. Verificar `/health/ready` na VM secundária (conexão Supabase OK — banco é compartilhado, sem failover de DB).
4. DNS: no hPanel Hostinger, redirecionar `homologacao.forzion.tech` A-record para IP da VM secundária. TTL baixo (300s) → propagação ~5min.
5. Confirmar: `curl -f https://homologacao.forzion.tech/health` a partir de dispositivo externo.

### Passo 4 — Contenção de efeitos (paralelo ao Passo 3)
- Se billing-renewal/reconciliation pode ter duplicado durante o janela de falha: acionar `billing-reconciliation.yml` manualmente (`gh workflow run billing-reconciliation.yml`) para reconciliar eventos Stripe após retomada.
- Outbox: `ObterProcessaveisAsync` usa `FOR UPDATE SKIP LOCKED` — itens travados no momento da falha da VM primária serão liberados automaticamente quando a conexão cair (PG libera locks de sessão); a VM secundária os processará no próximo ciclo.

### Passo 5 — Comunicar e documentar
1. Registrar hora de detecção, hora de failover, causa provável, impacto estimado (RPO real da janela).
2. Abrir issue no repositório com label `ops`/`incident`.
3. Se RPO excedeu alvo (<5min Fase 2, <15min Fase 1): checar backup Supabase + decidir se restauração de dado é necessária (§1/§2).
4. Alimentar [specification-coding] se for bug de classe nova (§5 desta spec — post-mortem).

### Passo 6 — Retornar VM primária ao serviço
1. Corrigir causa-raiz na VM primária.
2. Re-sincronizar deploy: `git pull` + `up -d --force-recreate`.
3. Re-apontar DNS de volta à VM primária (ou manter round-robin se ambas estiverem saudáveis).
4. Confirmar outbox sem itens `Pendente` presos (query: `SELECT status, COUNT(*) FROM outbox_efeitos GROUP BY status`).

## 8. ENFORCEMENT
- Fraco/processo (não é gate de CI). Drill de restore = tarefa AGENDADA (não pipeline). Migration destrutiva = revisão obrigatória + checklist de backup verificado. Branch protection / push direto é [specification-security §8] + [specification-infrastructure].
- Roadmap de fases (§6): rastrear como [ALVO]; ao completar cada fase, mover entradas para [EXISTE] com referência concreta (PR/commit/data).

## DRILL LOG
Registro de cada execução do drill de §2 (data · escopo · counts · tempo · resultado · operador). Append-only.

| Data | Escopo | migrations (`__EFMigrationsHistory`) | `contas` | tabelas BASE | Tempo | Resultado | Operador |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2026-06-11 | homolog · dump lógico `-Fc` (pg_dump 18, host direto) → restore em `postgres:17` local | 31 | 5 | 32 | dump 3s · restore <1s | ✅ sucesso — restore validado, dados íntegros | arthuurw |

> 2026-06-11 — 1º drill PROVOU o pipeline (dump→restore→counts coerentes). **31 migrations = HEAD deployado de homolog**; o repo tem 34 — as 3 de 2026-06-10/11 (`AdicionarOutboxEfeitos`, `UniqueDeliveryLogIdempotencia`, `AdicionarAnonimizadoEmAlunosETreinadores`) são branch-local ainda não deployadas → NÃO é perda de dado. 32 tabelas BASE casa com o anchor "32/schema". Credencial usada = dev (`forzion_api`, lê homolog ok); host direto IPv6 funcionou do host Windows (não de container).
> Decisão aberta (não-código, usuário): tier = **Free** = sem backup gerenciado, e este dump foi pontual (sem cópia off-site recorrente) → **RPO segue ∞**. Para fechar: ou migrar p/ **Pro** (daily backup + PITR) ou agendar `db dump` off-site recorrente (cron/Action). Registrar a escolha aqui.

## 9. REFERÊNCIAS
[specification-infrastructure] (VM/compose/deploy/SSH/SSL), [specification-db] (migration/backfill/ownership/restore tooling), [specification-observability] (health/error_logs/relatório), [specification-security] (acesso a backup/segredos, branch protection), [specification-coding] (post-mortem → regra de classe nova).
