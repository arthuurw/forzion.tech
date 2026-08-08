# specification-dr — backup, restore & disaster recovery (forzion.tech)

DOC PARA AGENTES. Continuidade: backup, restore, rollback de deploy, runbook de incidente. Infra de deploy (VM Hostinger/docker-compose/nginx/SSL) → [specification-infrastructure]; esta spec é o "e quando quebra / perde dado?". Ler antes de mexer em backup, migration destrutiva, ou processo de deploy. Formato denso.

Marcação: **[EXISTE]** = implementado e verificado · **[GAP]** = lacuna aberta, nenhuma correção implementada · **[ALVO]** = planejado, não implementado. Não tratar [ALVO] como estado.

**Resumo do que é REAL hoje**: backup lógico diário cifrado dos DOIS ambientes (homolog + produção) no R2 · restore provado ponta-a-ponta para o artefato de HOMOLOG · pipeline de deploy com dry-run, migrate one-shot, health-gate e rollback automático de imagem · dead-man switch de backup. **O que NÃO existe**: PITR, réplica, 2ª VPS, failover, RTO definido, drill do artefato de PROD, runbook de incidente escrito.

## MANUTENÇÃO
- Atualizar ao mudar provider de banco, cadência de backup, ou processo de deploy. Resolver um [ALVO]/[GAP] → mover p/ [EXISTE] com a referência concreta.

## 1. BACKUP (Supabase / PG 17)
- [EXISTE] **DOIS projetos Supabase, um por ambiente** — canônico em [specification-db §STACK]: homolog = `forzion` (`fdpdbtiuuitndbeujcbj`, sa-east-1, schema `homolog`); produção = `forzion_p` (`zdmgqnostkqiolutrbwq`, us-east-1, schema `public`). Backup e restore são por ALVO: restaurar homolog não restaura prod.
- [EXISTE] `forzion` = **tier FREE** → **zero backup gerenciado, SEM PITR** ([docs Supabase](https://supabase.com/docs/guides/platform/backups): recomendam `pg_dump` manual + cópia off-site). Pro traria daily backup (retenção 7d) + PITR add-on (RPO<1min, exige compute add-on ≥Small). **Tier do `forzion_p` NÃO registrado aqui** — não assumir backup gerenciado em prod; o dump lógico abaixo é o único mecanismo comprovado.
- [EXISTE] O `public` do projeto `forzion` é vestigial (prod migrou p/ projeto próprio) e saiu do dump; `develop` é sandbox e não é dumpado.
- [EXISTE] `ai_token_usage` é criada por migration EF (`AdicionarAiTokenUsage`, idempotente `IF NOT EXISTS`) — restore de estrutura vem junto no migrate/`pg_restore`, sem passo manual ([specification-db]).
- [EXISTE — automação] `.github/workflows/db-backup.yml` (cron diário 06:00 UTC + `workflow_dispatch`; branch default `homolog` = onde os crons vivem, então dispara sozinho): runner instala `postgresql-client-17`+`age` e roda a função `backup_one` em **2 alvos**:

| Alvo | Secret da URL | Escopo | Prefixo R2 | Condição |
|---|---|---|---|---|
| homolog | `BACKUP_DATABASE_URL` | `--schema=homolog` | (raiz) | sempre |
| prod | `BACKUP_DATABASE_URL_PROD` | `--schema=public` | `prod/` | secret presente (setado — **prod tem backup ATIVO**) |

  - **Guard fail-closed**: secret de prod ausente COM `vars.PROD_DEPLOY_ENABLED=true` REPROVA o run (`::error::`) — prod no ar sem DR é falha, não no-op. Sem o deploy armado, o alvo é pulado e o run segue VERDE (não red-alerta o backup de homolog). Coberto por `scripts/test/db-backup-prod-guard-check.sh`.
  - Conexão via **Session pooler** (`forzion_api.<ref>`:5432) — runner do GitHub não tem rota IPv6 p/ o host direto, e `pg_dump` exige sessão (Transaction :6543 não serve).
  - `pg_dump -Fc` → **cifra com `age`** (chave PÚBLICA `BACKUP_AGE_PUBLIC_KEY`; a privada fica OFFLINE com o dono e NUNCA vira secret de CI) → upload p/ Cloudflare R2 (`R2_*`, S3-compat, egress grátis). **Off-site real**: domínio de falha ≠ Supabase e ≠ repo.
  - **Dump em claro NUNCA persiste** (PII/LGPD; o repo é PÚBLICO → dump JAMAIS em git/artifact): `trap EXIT` remove os dumps dos 2 alvos em qualquer saída, `rm` pós-cifra, e guarda de tamanho <1KB aborta antes de subir lixo.
  - Senha fora do process list: a URI é quebrada com `urlsplit` (python3) → senha URL-decodada p/ `PGPASSWORD`, URI sem senha passada ao `pg_dump`. Senha percent-encoded (`%40`, `%2F`) FALHA a auth se não for decodada — libpq decoda quando vem na URI, mas usa `PGPASSWORD` literal.
  - **Dead-man switch**: ping ao Healthchecks (`HC_PING_KEY`, endpoint `db-backup` / `db-backup/fail`) em sucesso E falha → backup que deixa de rodar (workflow desabilitado, repo dormente) alerta sozinho. Falha do run também abre issue com labels `ops`/`db-backup-failed`.
- [EXISTE] Dump de prod **exercitado com sucesso** (cifrado e no R2 sob `prod/`), sem `permission denied for sequence` — os GRANTs de sequence de `scripts/provision-forzion_p-grants.sql` seguram esse modo de falha. **INVARIANTE**: dumpar um schema exige `forzion_api` com SELECT nas TABELAS **e nas SEQUENCES** (tabela com PK identity, ex. `data_protection_keys`, cria sequence cujo `last_value` o `pg_dump` lê) — ver [specification-db §APLICAÇÃO DE MIGRATIONS].
- **GOTCHA — senha da ROLE ≠ senha do BANCO**: a URI que o dashboard entrega em Connect → Session pooler vem com usuário `postgres.<ref>` e `[YOUR-PASSWORD]` = senha do banco. Trocar só o usuário p/ `forzion_api.<ref>` mantendo aquela senha dá `FATAL: password authentication failed for user "forzion_api"` (o tenant resolve, a senha não). Sintoma distinto de usuário sem o sufixo `.<ref>`, que daria `Tenant or user not found`.
- **Retenção do bucket = 30 dias** — regra de Object Lifecycle `expire-30d` configurada no dashboard Cloudflare R2 (Settings → Object lifecycle rules). Dump ~0.3MB/dia → ~9MB no teto de 30 dumps. **Implicação RPO**: só há restore até 30 dias atrás — corrupção não detectada nesse prazo não tem backup limpo p/ voltar. Lifecycle gerido no **console, não em código**: o token R2 de backup é object-scope (Object R/W) e `PutBucketLifecycleConfiguration` via S3 API exige token bucket-admin → `AccessDenied`; gerir no dashboard evita deixar token bucket-admin parado no repo p/ uma op one-shot.
- **INVARIANTE — identity `age` privada**: guardada OFFLINE em local durável (password manager/cofre), fora do repo e do CI. Perdê-la torna **todo** dump já cifrado irrecuperável — não existe re-cifra retroativa. Rotacionar a pública só protege dumps FUTUROS.

## 2. RESTORE — DRILL (backup não testado = sem backup)
- [EXISTE] Sem backup gerenciado, o dump lógico É o mecanismo de backup → o drill PROVA o próprio backup. O mesmo procedimento serve de restore real em incidente.
- [EXISTE] Version-match: cliente PG local = 16 e 18 (NÃO 17); servidor = 17.6 → fazer dump E restore pelo MESMO container `postgres:17` zera o skew ([specification-db]). Host direto `db.<ref>.supabase.co` é IPv6-only → dump via **Session pooler (IPv4, :5432)**; **Transaction pooler :6543 NÃO suporta pg_dump** (sem sessão).
- [EXISTE — RUNBOOK PRIMÁRIO] Drill do ARTEFATO REAL do R2 (exercita o mesmo `.dump.age` que um incidente usaria). Script commitado `scripts/dr/restore-drill.ps1` (PowerShell/Windows; Docker rodando) encapsula: `aws s3 cp` do `.dump.age` mais recente (ou `-Key` explícita) → `age -d -i <identity-offline>` → `postgres:17` efêmero → restore via binário DO PRÓPRIO CONTAINER (`docker exec ... pg_restore`, zera skew de versão) `--no-owner --no-privileges` → valida counts-âncora via stdin (`"__EFMigrationsHistory"`, `contas`, tabelas BASE) → imprime linha pronta pro DRILL LOG → teardown `try/finally` (remove dump claro + artefato baixado + container, mesmo em falha). Segredos (identity age, creds R2) só via param/env — nunca hard-coded:
  ```powershell
  $env:AWS_ACCESS_KEY_ID="..."; $env:AWS_SECRET_ACCESS_KEY="..."   # creds R2, leitura no bucket
  scripts/dr/restore-drill.ps1 -Bucket <bucket> -AccountId <r2-account-id> -AgeIdentity C:\caminho\identity-offline.txt
  ```
  **Roda LOCAL, pelo dono da identity age privada — NUNCA em CI.** Automação via `workflow_dispatch` foi REJEITADA: decifrar em CI exigiria a chave privada como secret, contradizendo o invariante do backup (§1). Automação com modelo de chave dedicado (identity de drill separada) é [ALVO] Fase 3 (§6).
- [GAP] **O drill nunca rodou contra o artefato de PRODUÇÃO** (`prod/…dump.age`). Só o de homolog foi restaurado ponta-a-ponta. O dump de prod é gerado e cifrado, mas o caminho de VOLTA do dado real de titular segue não-provado. Prioridade alta: prod está no ar.
- [EXISTE] Variante — dump direto do source (sem passar pelo R2; valida o pipeline dump→restore isoladamente, NÃO prova o artefato do backup real). Senha do source via `$env:SRC_PW` setada no shell — NUNCA inline/commit/echo. Host+user EXATOS do Dashboard → Connect → Session pooler:
  ```
  $drill="C:\temp\drill"; New-Item -ItemType Directory -Force $drill | Out-Null
  # alvo descartável PG17
  docker run -d --name drill-pg17 -e POSTGRES_PASSWORD=drill -p 55432:5432 postgres:17
  # dump do source (-n homolog = só o schema canônico)
  docker run --rm -e PGPASSWORD=$env:SRC_PW -v "${drill}:/out" postgres:17 `
    pg_dump -h <SESSION_POOLER_HOST> -p 5432 -U postgres.<ref> -d postgres `
    -n homolog -Fc -f /out/homolog.dump
  # restore no alvo (host.docker.internal alcança a porta publicada)
  docker run --rm -e PGPASSWORD=drill -v "${drill}:/out" postgres:17 `
    pg_restore -h host.docker.internal -p 55432 -U postgres -d postgres `
    --no-owner --no-privileges /out/homolog.dump
  # validar (socket local = trust, sem senha)
  docker exec drill-pg17 psql -U postgres -d postgres -c 'SELECT count(*) FROM homolog."__EFMigrationsHistory";'
  docker exec drill-pg17 psql -U postgres -d postgres -c 'SELECT count(*) FROM homolog.contas;'
  # teardown
  docker rm -f drill-pg17; Remove-Item -Recurse -Force $drill; Remove-Item Env:\SRC_PW
  ```
  GOTCHAS: identificador case-sensitive `"__EFMigrationsHistory"` só sobrevive via stdin (`$sql | docker exec -i … psql`), não por `-c` (PowerShell/docker comem as aspas → vira lowercase → "does not exist"); colunas EF = snake_case (`migration_id`). Client v18 ≥ server v17 (forward-compat) funciona do host Windows contra o host direto IPv6 — dispensa o pooler, mas não de dentro de container.
  `pg_restore` loga warning não-fatal `schema "public" already exists` (a imagem `postgres:17` já cria `public`) — inofensivo.
- [EXISTE — restore do backup automático, manual] Equivalente ao RUNBOOK PRIMÁRIO se o script não estiver disponível: `aws s3 cp s3://<bucket>/[prod/]<arq>.dump.age . --endpoint-url https://<acct>.r2.cloudflarestorage.com` → `age -d -i <identity-age> <arq>.dump.age > restore.dump` → `pg_restore --no-owner --no-privileges -d <conn-alvo> restore.dump` (alvo = `postgres:17` descartável p/ teste, ou banco real em incidente). `-Fc` permite restore seletivo (`-t`/`-n`).
- [EXISTE] Cadência: dump automático DIÁRIO; drill de restore MANUAL p/ provar que o backup volta. Resultado de cada execução em §DRILL LOG.

## 3. RTO / RPO (definir alvo de negócio)
- [EXISTE — estado] **RPO ≤ 24h** nos dois ambientes (intervalo do cron diário), teto de 30 dias de histórico (§1). Para homolog isso é COMPROVADO, não nominal: o drill do artefato real do R2 restaurou com sucesso (§DRILL LOG). Para prod é nominal — o dump existe, o restore não foi exercitado (§2 [GAP]). **RTO = manual/indefinido** (nenhum alvo acordado, nenhuma automação de restauração). RPO<5min (PITR) só viria com Supabase Pro (Fase 2, não escolhido).
- [ALVO] RTO/RPO alvo definidos com o negócio. Dado financeiro (pagamentos/assinaturas) tolera pouco RPO → alvo RPO<5min exige Pro+PITR (Fase 2).

## 4. ROLLBACK DE DEPLOY
- [EXISTE] Deploy = `docker compose build/up` na VM via SSH ([specification-infrastructure]). Rollback de CÓDIGO: re-deploy da imagem/tag anterior.
- [EXISTE — deploy-safety R1] Migrate DESACOPLADO do boot: `MigrationStartup.ShouldAutoMigrateOnBoot` só é true em Development; Homolog/Production aplicam via `app migrate` (modo CLI one-shot — aplica schema+seed e sai 0/1). Boot normal NÃO toca DDL ⇒ migration quebrada não derruba o container DEPOIS do `up -d`.
- [EXISTE — deploy-safety] Pipeline de deploy com 4 gates (ci.yml; detalhe em [specification-infrastructure §DEPLOY]):
  - **A — dry-run** (`scripts/migrate-dryrun.sh` + `docker-compose.dryrun.yml`): clona o schema real (pg_dump estrutura+dados, na VM) num Postgres efêmero e roda o migrate ali ANTES do real. Pega falha data-dependente (ex.: índice UNIQUE sobre linha duplicada) invisível ao CI em DB vazio.
  - **B — migrate one-shot pré-`up -d`**: `compose run --rm --no-deps backend ... migrate`. Falha aborta (`set -e`) com os containers ANTIGOS no ar (zero downtime).
  - **C — health-gate + rollback**: pós-`up -d`, poll `/health`+`/health/ready` por dentro do container; reprovou → re-tag da imagem anterior (`:previous`, guardada antes do build) + `up -d --no-build` + exit 1. Smoke E2E pós-deploy: `smoke.yml` (gateia quando `HOMOLOG_BASE_URL` setado).
  - **D — lint de migration arriscada** (`scripts/lint-migrations.sh`, job PR-only): reprova `CreateIndex unique:true`, `AddColumn`/`AlterColumn` NOT NULL sem default; justificar via comentário `lint-migrations:allow`.
- [ALVO] Schema forward-compatible (expand/contract, [specification-db §BACKFILL]) habilita rollback de código SEM rollback de schema — a regra que torna deploy revertível. Migration destrutiva sem janela expand/contract trava o rollback → exige backup verificado (§1) ANTES.

## 5. RUNBOOK DE INCIDENTE
- [EXISTE parcial] Sinais de detecção existem (`/health`+`/health/ready`, `error_logs`, `health_snapshots`, relatório diário — [specification-observability]; dead-man do backup — §1). Falta o runbook que os amarra em procedimento.
- [ALVO] Passos mínimos a documentar concretamente: DETECTAR → CONTER (parar o efeito; ex. desabilitar renovações via `/internal` ou flag) → COMUNICAR → RESTAURAR (§2/§4) → POST-MORTEM (alimenta [specification-coding] se for bug de classe nova).
- [GAP] **Incidente de DADO PESSOAL não tem runbook.** O runbook existente ([specification-security] §9.1) cobre vazamento de SEGREDO e para na fronteira técnica (rotacionar/revogar/purgar). Não há procedimento de comunicação à **ANPD e aos titulares** (LGPD art. 48): sem critério de gravidade, sem prazo interno, sem responsável, sem modelo de comunicação. Com produção no ar e titulares reais, isto é lacuna de conformidade, não item de backlog técnico. Cross-ref [specification-lgpd] §PENDÊNCIAS.
- [GAP] **Backend sem Sentry** ⇒ a detecção depende de `error_logs` + relatório diário; não há alerta em tempo real de erro de servidor em produção. Ver [specification-observability] / [specification-security] §8.

## 6. ROADMAP DE HA (ALVO — não implementado; fases serão marcadas [EXISTE] ao concluir)

Estado real: 1 VPS (backend+frontend+nginx, prod e homolog no mesmo host) · 2 projetos Supabase Free-tier, 1 região cada, sem replica · conexão via **Session pooler :5432** (DR-01; `pg_stat_activity` confirma `application_name=Supavisor`) · DNS sem failover · SEM backup gerenciado/PITR; backup diário cifrado no R2 dos 2 ambientes + drill do artefato REAL provado para homolog · RTO=manual, RPO≤24h.
Alvo SaaS financeiro: RTO<15min (processo)/<4h (VM); RPO<5min · ≥2 instâncias de app + LB · Supabase Pro+PITR+replica · drill trimestral.

### Fase 1 — Quick-wins sem downtime [parcialmente concluída]
- [EXISTE — DR-01] Conexão runtime via **Session pooler Supabase (:5432, IPv4)** em vez de direct (IPv6-only): pooling + IPv4, drop-in SEM código (session suporta migration/prepared stmt). Transaction :6543 descartado (quebraria o migrate). Detalhe canônico: [specification-db §DICAS].
- [EXISTE] Drill do artefato REAL do R2 (`scripts/dr/restore-drill.ps1`, §2 RUNBOOK PRIMÁRIO) executado ponta-a-ponta contra dump real de homolog — RPO≤24h comprovado para esse alvo.
- [GAP] Mesmo drill contra o artefato de PROD (§2).
- [GAP] Tier real do projeto `forzion_p` não registrado (§1).

### Fase 2 — Supabase Pro, PITR e read-offloading [ALVO]
- [ALVO] Upgrade para Supabase Pro → habilita PITR (RPO<1min) + read-replica na mesma região.
- [ALVO] `AppDbContextReadOnly`: segundo `DbContext` `Scoped` apontando à read-replica (connection string com `Search Path` idêntico, sem `MigrateAsync`/`SeedAsync`). Handlers de leitura pesada (listagens de billing, relatório de saúde, cron de reconciliação) injetam o read-only; handlers de mutação continuam no `AppDbContext` primário. NÃO chamar `MigrateAsync`/`SeedAsync` na instância read-only.
- [ALVO] Runbook de failover manual (§7).
- [ALVO] Drill de restore mensal agendado (cadência mínima enquanto não há automatização).

### Fase 3 — 2ª VPS, Load Balancer e outbox multi-host [ALVO]
- [ALVO] 2ª VPS Hostinger idêntica à 1ª (mesmo `setup-vm.sh`, mesma stack compose); DNS round-robin ou LB (nginx upstream / Cloudflare LB) distribuindo tráfego.
- [ALVO] Deploy sincronizado: CI faz SSH nas 2 VMs sequencialmente; se 1 falhar, alertar sem derrubar a outra.
- [ALVO] Outbox multi-host: com 2 VMs cada uma rodando o worker, `FOR UPDATE SKIP LOCKED` garante que itens travados pela VM-A são pulados pela VM-B, sem processamento duplo (§6.1).
- [ALVO] Drill mensal automatizado: GitHub Actions restaurando snapshot num projeto Supabase temporário e validando contagem de tabelas (exige modelo de chave `age` dedicado ao drill — §2).

### §6.1 — Confirmação SKIP LOCKED (OutboxRepository)
[EXISTE] `OutboxRepository.ObterProcessaveisAsync` (`forzion.tech.Infrastructure/Persistence/Repositories/OutboxRepository.cs`, `FromSqlInterpolated`) emite o `SELECT ... FOR UPDATE SKIP LOCKED` sobre `outbox_efeitos`. Workers concorrentes (múltiplas VMs ou hosted-services) pulam itens já travados por outra transação — sem efeito 2×. Pré-requisito da Fase 3 já satisfeito.

## 7. RUNBOOK DE FAILOVER MANUAL [ALVO — exige Fase 3]
Prematuro hoje (1 só VPS). Quando a Fase 3 (2ª VPS + LB/DNS) existir, escrever o procedimento operador concreto: CONFIRMAR falha (`/health`+`/health/ready` persistente / VM inacessível) → tentar RECUPERAR a primária (`up -d --remove-orphans`) → FAILOVER p/ a secundária (deploy aplicado + `/health/ready`) → re-apontar DNS (A-record TTL baixo) → CONTER efeitos (reconciliar billing duplicado; outbox `SKIP LOCKED` libera locks na queda) → RESTAURAR dado se RPO excedido (§1/§2) → comunicar/post-mortem (§5) → retornar a primária ao serviço.

## 8. ENFORCEMENT
- Fraco/processo (não é gate de CI), com DUAS exceções automatizadas: o guard `PROD_DEPLOY_ENABLED` sem `BACKUP_DATABASE_URL_PROD` reprova o `db-backup` (§1), e o dead-man do Healthchecks alerta backup que deixa de rodar.
- Drill de restore = tarefa AGENDADA (não pipeline). Migration destrutiva = revisão obrigatória + checklist de backup verificado ([specification-db §MIGRATION-SAFETY]).
- Roadmap de fases (§6): rastrear como [ALVO]; ao completar cada fase, mover entradas para [EXISTE] com referência concreta.

## DRILL LOG
Registro de cada execução do drill de §2 (data · escopo · counts · tempo · resultado · operador). Append-only.

| Data | Escopo | migrations | `contas` | tabelas BASE | Tempo | Resultado | Operador |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2026-06-11 | homolog · dump lógico `-Fc` (pg_dump 18, host direto) → restore em `postgres:17` local | 31 | 5 | 32 | dump 3s · restore <1s | ✅ pipeline dump→restore validado | arthuurw |
| 2026-07-09 | tentativa de drill do artefato REAL do R2 | — | — | — | — | ❌ identity `age` privada não localizada ⇒ dumps anteriores permanentemente ilegíveis; par rotacionado | arthuurw |
| 2026-07-09 | homolog · artefato REAL do R2 (`age -d`, chave rotacionada) → restore em `postgres:17` via `scripts/dr/restore-drill.ps1` | 55 | 10 | 46 | 6.7s | ✅ restore validado, dados íntegros | arthuurw |

> Counts do drill refletem o HEAD **deployado** no alvo dumpado, não o HEAD do repo — migrations mergeadas e ainda não deployadas aparecem "faltando" e NÃO são perda de dado. Total canônico atual em [specification-db §STACK].
> Lição da linha 2, já virada invariante em §1: perder a identity `age` privada torna todo dump cifrado irrecuperável — não há re-cifra retroativa; rotacionar a pública protege só os dumps futuros. Aceitou-se perder os dumps pré-rotação porque a fonte primária (Supabase) seguia viva.
> Próximo drill devido: artefato `prod/` (§2 [GAP]).

## 9. REFERÊNCIAS
[specification-infrastructure] (VM/compose/deploy/SSH/SSL/CI-CD), [specification-db] (migration/backfill/ownership/restore tooling/projetos por ambiente), [specification-observability] (health/error_logs/relatório), [specification-security] (acesso a backup/segredos, branch protection, runbook de vazamento §9.1), [specification-lgpd] (incidente com dado pessoal), [specification-coding] (post-mortem → regra de classe nova).
