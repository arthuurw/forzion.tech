# specification-dr — backup, restore & disaster recovery (forzion.tech)

DOC PARA AGENTES. Continuidade: backup, restore, rollback de deploy, runbook de incidente. Infra de deploy (VM Hostinger/docker-compose/nginx/SSL) → [specification-infrastructure]; esta spec é o "e quando quebra / perde dado?". Ler antes de mexer em backup, migration destrutiva, ou processo de deploy. Formato denso. ATENÇÃO: várias entradas são ALVO/aspiracional (não implementado) — marcadas [ALVO]; o que existe é marcado [EXISTE].

## MANUTENÇÃO
- Atualizar ao mudar provider de banco, cadência de backup, ou processo de deploy. Resolver um [ALVO] → mover p/ [EXISTE] com a referência concreta.

## 1. BACKUP (Supabase / PG 17)
- [EXISTE parcial] Projeto Supabase único `forzion` (ref `fdpdbtiuuitndbeujcbj`, sa-east-1) — backups gerenciados do tier Supabase. DOCUMENTAR aqui o tier atual + janela/retenção real (PITR disponível? frequência?). Hoje indefinido nesta spec = risco.
- [EXISTE] 3 schemas (`homolog` canônico, `develop`, `public`) no MESMO banco — backup do banco cobre os 3; restore seletivo por schema exige cuidado ([specification-db] ownership diverge por schema).
- [EXISTE] `ai_token_usage` é NON-EF (criada fora de migration) — restore de estrutura precisa recriá-la (`CREATE TABLE ... LIKE homolog.ai_token_usage INCLUDING ALL`), [specification-db].

## 2. RESTORE (drill — backup não testado = sem backup)
- [ALVO] Drill periódico: restaurar em ambiente isolado, validar integridade (contagem de tabelas = 32/schema, seed presente, migrations em dia). Definir cadência.
- [ALVO] Procedimento de restore documentado passo-a-passo (PITR se o tier oferecer; senão último backup + replay de migrations).
- [EXISTE] Gotcha de tooling: cliente PG local pode divergir do servidor PG17 (version mismatch) → usar container `postgres:17` p/ pg_restore; host Supabase direto é IPv6-only ([specification-db]).

## 3. RTO / RPO (definir alvo de negócio)
- [ALVO] RTO (tempo até voltar ao ar) e RPO (perda máxima de dado aceitável) — DEFINIR números com o negócio. Hoje implícito = indefinido. Dado financeiro (pagamentos/assinaturas) tolera pouco RPO.

## 4. ROLLBACK DE DEPLOY
- [EXISTE] Deploy homolog = `docker compose build/up` na VM via SSH ([specification-infrastructure]). Rollback de CÓDIGO: re-deploy da imagem/tag anterior.
- [EXISTE risco] `Program.cs` roda `MigrateAsync`+`SeedAsync` no startup em Dev/Homolog contra o REMOTO ([specification-db §ACESSOS]) — migration aplicada NÃO volta sozinha no rollback de imagem.
- [ALVO] Schema forward-compatible (expand/contract, [specification-db §BACKFILL]) habilita rollback de código SEM rollback de schema — a regra que torna deploy revertível. Migration destrutiva sem janela expand/contract trava o rollback → exige backup verificado (§1) ANTES.

## 5. RUNBOOK DE INCIDENTE
- [ALVO] Passos mínimos a documentar concretamente: DETECTAR (`/health`+`/health/ready` liveness/readiness + log→DB `error_logs` + relatório diário de saúde, [specification-observability]) → CONTER (parar o efeito; ex. desabilitar renovações via `/internal` ou flag) → COMUNICAR → RESTAURAR (§2/§4) → POST-MORTEM (alimenta [specification-coding] se for bug de classe nova).
- [EXISTE parcial] Sinais de detecção já existem (health endpoints, error_logs, health_snapshots); falta o runbook que os amarra em procedimento.

## 6. ROADMAP DE HA (ALVO — não implementado; fases serão marcadas [EXISTE] ao concluir)

Estado real: 1 VPS (backend+frontend+nginx) · 1 Supabase, 1 região, sem replica/pooler · DNS sem failover · backup tier indefinido, drill nunca feito · RTO=manual, RPO até 24h se Free tier.
Alvo SaaS financeiro: RTO<15min (processo)/<4h (VM); RPO<5min · ≥2 instâncias de app + LB · Supabase Pro+PITR+replica · drill trimestral.

### Fase 1 — Quick-wins sem downtime (1–2 dias) [parcialmente em execução — DR-01/02]
- [ALVO/em execução] Pooler Supabase Transaction mode (:6543) em vez de conexão direta :5432 — elimina esgotamento de conexão + suporte a replica futura.
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

## 9. REFERÊNCIAS
[specification-infrastructure] (VM/compose/deploy/SSH/SSL), [specification-db] (migration/backfill/ownership/restore tooling), [specification-observability] (health/error_logs/relatório), [specification-security] (acesso a backup/segredos, branch protection), [specification-coding] (post-mortem → regra de classe nova).
