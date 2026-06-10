# specification-dr — backup, restore & disaster recovery (forzion.tech)

DOC PARA AGENTES. Continuidade: backup, restore, rollback de deploy, runbook de incidente. Infra de deploy (VM Hostinger/docker-compose/nginx/SSL) → [specification-infrastructure]; esta spec é o "e quando quebra / perde dado?". Ler antes de mexer em backup, migration destrutiva, ou processo de deploy. Formato denso. ATENÇÃO: várias entradas são ALVO/aspiracional (não implementado) — marcadas [ALVO]; o que existe é marcado [EXISTE].

## MANUTENÇÃO
- Atualizar ao mudar provider de banco, cadência de backup, ou processo de deploy. Resolver um [ALVO] → mover p/ [EXISTE] com a referência concreta. Vive em `specs/` (commitado).

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

## 6. ENFORCEMENT
- Fraco/processo (não é gate de CI). Drill de restore = tarefa AGENDADA (não pipeline). Migration destrutiva = revisão obrigatória + checklist de backup verificado. Branch protection / push direto é [specification-security §8] + [specification-infrastructure].

## 7. REFERÊNCIAS
[specification-infrastructure] (VM/compose/deploy/SSH/SSL), [specification-db] (migration/backfill/ownership/restore tooling), [specification-observability] (health/error_logs/relatório), [specification-security] (acesso a backup/segredos, branch protection), [specification-coding] (post-mortem → regra de classe nova).
