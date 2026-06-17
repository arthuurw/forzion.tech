# specification-db — estrutura de banco (forzion.tech)

DOC PARA AGENTES. Fonte de verdade da estrutura de banco. Formato denso. Consultar antes de qualquer alteração de banco.

Notação coluna: `nome(tipo, NN|null[, nota])`. PK / FK(col→tabela, ONDELETE) / UQ(cols[, parcial WHERE]). tstz = timestamptz. Enums persistidos como `text` (HasConversion<string>; valor = nome do enum).

## MANUTENÇÃO DESTE ARQUIVO
- Este arquivo DEVE ser mantido atualizado. Sempre que uma alteração RELEVANTE de estrutura for feita (nova migration, tabela, coluna, FK, índice, enum, mudança de tipo/nullability/default), ajustar este arquivo NA MESMA TAREFA.
- Ao mudar contagem de tabelas/migrations, atualizar os números nas seções relevantes.

## STACK & SCHEMAS
- PostgreSQL 17 (Supabase). EF Core 8, snake_case naming convention. Stack macro da app em AGENTS.md §STACK.
- Migrations SCHEMA-AGNOSTIC: `AppDbContext` SEM `HasDefaultSchema`. Schema-alvo vem do `search_path` da connection (ex.: `Search Path=homolog`). Mesmas migrations aplicam em qualquer schema.
- **History table — RUNTIME pina o schema; DESIGN-TIME não** (gotcha Npgsql 8.0.11): `NpgsqlHistoryRepository.ExistsSql` checa a existência da `__EFMigrationsHistory` com schema HARDCODED em `public` (`n.nspname = TableSchema ?? "public"`), mas CREATE/leitura usam o search_path. Sem pinar, num alvo cujo `public` NÃO tem a history (ex.: dry-run clonando só `homolog`), o Exists dá falso-negativo → CREATE plano cai no search_path e colide (`42P07 already exists`). Por isso o **runtime** (`InfrastructureExtensions`, usado por `app migrate`/dry-run) pina `MigrationsHistoryTable("__EFMigrationsHistory", <1º schema do Search Path>)` via `MigrationHistorySchemaResolver` → Exists/CREATE/leitura no MESMO schema. O **design-time** (`AppDbContextFactory`) fica SEM schema (unqualified) de propósito: `dotnet ef migrations script` precisa gerar SQL portável e reusável por schema (§APLICAÇÃO DE MIGRATIONS depende de unqualified + `SET search_path`).
- Schemas com estrutura IDÊNTICA: `homolog` (deploy ativo, canônico), `develop` (sandbox), `public` (sandbox/legado sincronizado). 36 tabelas BASE cada (34 entidades EF + `ai_token_usage` não-EF + `__EFMigrationsHistory` de controle) após todas as 39 migrations aplicadas (table count inalterado nas 2 últimas: `DeliveryLogPseudonimizarRecipient` e `AdicionarCheckConstraintsIntegridade` não criam/removem tabela); paridade dos 3 schemas verificada 2026-06-16 no nível de então — as 2 últimas migrations podem não estar aplicadas nos 3 schemas. Nível atual por schema pode divergir — conferir antes (§APLICAÇÃO DE MIGRATIONS §GOTCHA).
- `ai_token_usage`: existe nos 3 schemas mas NÃO é gerenciada por migration EF (criada fora do EF). Recriar via `CREATE TABLE <schema>.ai_token_usage (LIKE homolog.ai_token_usage INCLUDING ALL)`.
- 39 migrations EF (arquivos não-Designer/Snapshot em `Infrastructure/Migrations/`; última `AdicionarCheckConstraintsIntegridade`). Tabela de controle `__EFMigrationsHistory` por schema (colunas snake_case: `migration_id` varchar(150) PK, `product_version` varchar(32); EF `ProductVersion` = versão do pacote em `forzion.tech.Infrastructure.csproj`, não fixar aqui).
- `AdicionarConcurrencyTokenTreinador`: mapeia o system column `xmin` de `treinadores` como concurrency token (concorrência otimista). NÃO gera DDL — o `AddColumn` no `.cs` é artefato de modelo; o SQL gerado só insere a linha de history. Aplicar é no-op estrutural (só registra a migration).

## APLICAÇÃO DE MIGRATIONS (multi-schema — descobertas operacionais)
Projeto Supabase único: `forzion` (ref `fdpdbtiuuitndbeujcbj`, região sa-east-1). Os 3 schemas vivem no MESMO banco.

- **GOTCHA — schemas DRIFTAM**: develop/homolog/public podem estar em níveis DIFERENTES de migration (ex.: 2026-06-16 develop+public estavam 5 migrations atrás de homolog — niveladas a 37 cada via MCP, seguindo esta seção). SEMPRE conferir antes: `select max(migration_id), count(*) from <schema>."__EFMigrationsHistory"`. NÃO assumir que estão iguais.
- **Caminho normal (preferido)**: `dotnet ef database update` com connection `ConnectionStrings:AppConnection` + `Search Path=<schema>` (uma vez por schema). Conecta como `forzion_api` (dono em develop/homolog).
- **Caminho via Supabase MCP** (quando sem connection local): o MCP conecta como `postgres` (NÃO superuser; tem `createrole`+`bypassrls`+`admin_option` em `forzion_api`).
  - **OWNERSHIP DIVERGE POR SCHEMA** (gotcha central): tabelas em `develop`/`homolog` são owned por **`forzion_api`**; em `public` são owned por **`postgres`**. DDL exige ser owner.
    - develop/homolog: `GRANT forzion_api TO postgres WITH SET TRUE;` (membership nasce com `set_option=false`) → `SET ROLE forzion_api;` → DDL → no fim reverter `GRANT forzion_api TO postgres WITH SET FALSE;`.
    - public: rodar como `postgres` direto (já é owner); NÃO usar SET ROLE.
  - Em todos: `SET search_path TO <schema>;` antes do DDL (migrations são schema-agnostic, sem qualificador).
  - **`public` exige GRANT manual nas tabelas novas**: tabelas EF em public são criadas por postgres com RLS desabilitado e `GRANT ALL` para `anon, authenticated, forzion_api, service_role` (espelhar o padrão de `public.pagamentos`). Em develop/homolog isso é automático (dono = forzion_api, role da app). Sequências: N/A (PKs uuid).
  - Gerar SQL mínimo por schema: `dotnet ef migrations script <ultimaAplicada> <alvo> --project forzion.tech.Infrastructure --startup-project forzion.tech.Api`; remover linhas `START TRANSACTION;`/`COMMIT;` e rodar tudo num `execute_sql` (atômico por schema). `--idempotent` gera guards `IF NOT EXISTS(... migration_id ...)` se preferir um script único.
  - History: inserir `INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) VALUES ('<id>','<ProductVersion do dotnet ef>')` por migration aplicada (o `dotnet ef migrations script` já inclui o valor correto).

## CONVENÇÕES
- PK: `id` uuid gerado na app (Guid.NewGuid), não pelo banco. Exceção: `tokens_revogados` PK=`jti`.
- Timestamps: `created_at`(NN), `updated_at`(null) tstz. Datas de negócio: prefixo `data_*`.
- Email: VO normalizado lowercase.
- Money: numeric.
- FK default ON DELETE RESTRICT. CASCADE só em filhos de composição (ver por tabela).
- Tokens (password_reset/email_verification/refresh): armazenam SHA-256 hex(64) do token; cru só no e-mail (reset/verify) ou no cookie httpOnly (refresh); `conta_id` SEM FK física (só índice). EXCEÇÃO: `refresh_tokens.familia_id` TEM FK física com ON DELETE CASCADE (GC/purga da família apaga os tokens no nível do banco).
- UQ parciais impõem regra de negócio (pagamentos 1 Pendente/assinatura; treino_alunos 1 Ativo/treino).
- CHECK constraints impõem invariantes de domínio no último nível (barreira contra write fora do app/bug) — ver §CHECK CONSTRAINTS. Espelhados nos `*Configuration` via `ToTable(t => t.HasCheckConstraint(...))` (snapshot em sync); writes legítimos já passam (app valida antes), zero impacto em UX.

## CHECK CONSTRAINTS
Migration `AdicionarCheckConstraintsIntegridade` (DB-02, 2026-06-16; schema-agnostic, sem qualificador). Nome = `ck_<tabela>_<regra>`. Bounds alinhadas às factories de domínio (não rejeitam dado legítimo):
- `pacotes`: `preco >= 0`.
- `planos_plataforma`: `preco >= 0`; `max_alunos > 0`.
- `assinaturas_aluno`: `valor >= 0`. `assinaturas_treinador`: `valor >= 0`.
- `pagamentos`: `valor >= 0`. `pagamentos_treinador`: `valor >= 0`.
- `treino_exercicio_series`: `quantidade > 0`; `repeticoes_min > 0`; `repeticoes_max IS NULL OR repeticoes_max >= repeticoes_min`.
- `execucoes_exercicio`: `series_executadas > 0`; `repeticoes_executadas > 0`.
- Violação → `PostgresException` SqlState `23514` (check_violation); via SaveChanges vem envolto em `DbUpdateException`.

## ENUMS — BINDING DE COLUNA
Valores e semântica em [specification-model] §4. Binding enum→coluna é 1:1 MECÂNICO (`text` via `HasConversion<string>`, valor = nome do enum; coluna = snake_case do campo) e §TABELAS já lista o enum de cada coluna — aqui só EXCEÇÕES/notas db-specific:
- `TempoDisponivel` → `alunos.tempo_disponivel_minutos`: **`int`** (não text; valor = minutos).
- `PagamentoStatus` e `MetodoPagamento`: cada um vincula DUAS tabelas (`pagamentos` + `pagamentos_treinador`).
- `TipoGrupoMuscular`: **NÃO é coluna** (só seed de `grupos_musculares`; entidade `GrupoMuscular` é distinta).
- Defaults de coluna (`DificuldadeTreino`=Iniciante, `MetodoPagamento`=Pix, `ModoPagamentoAluno`=Plataforma): ver §TABELAS.

## TABELAS

### Identidade & Auth
contas — credenciais + tipo. id(uuid,NN); email(varchar256,NN); password_hash(text,NN,bcrypt); tipo_conta(text,NN,TipoConta); email_verificado(bool,NN,default false); verificado_em(tstz,null); anonimizada_em(tstz,null,LGPD); sessoes_invalidas_antes_de_utc(tstz,null,SEC-05 token-epoch — rejeita access token com nbf anterior); created_at(NN); updated_at(null). PK(id) UQ(email).

system_users — perfil admin plataforma. id(uuid,NN); conta_id(uuid,NN); nome(varchar,NN); role(text,NN,SystemRole); status(text,NN,UsuarioStatus); created_at(NN); updated_at(null). PK(id) FK(conta_id→contas,RESTRICT).

tokens_revogados — blacklist JWT (logout). jti(uuid,NN); expira_em(tstz,NN). PK(jti). Limpeza por hosted service.

password_reset_tokens — reset de senha. id(uuid,NN); conta_id(uuid,NN,sem FK); token_hash(varchar64,NN); expires_at(tstz,NN,+1h); used_at(tstz,null); created_at(NN). PK(id) UQ(token_hash) idx(conta_id).

refresh_token_families — agregado de sessão (rotação de refresh). id(uuid,NN); conta_id(uuid,NN,sem FK); criada_em(tstz,NN); absoluto_expira_em(tstz,NN,teto absoluto server-side); revogada_em(tstz,null); motivo_revogacao(varchar32,null,MotivoRevogacaoFamilia); rotulo(varchar256,null,device/user-agent). PK(id) idx(conta_id), idx(revogada_em). GC `LimparExpiradasAsync` (revogada/pós-absoluto); purga LGPD `ExcluirPorContaIdAsync`.

refresh_tokens — token de refresh single-use (cadeia de rotação). id(uuid,NN); familia_id(uuid,NN); token_hash(varchar64,NN,SHA-256 hex); criado_em(tstz,NN); expira_em(tstz,NN,idle); usado_em(tstz,null); substituido_por_id(uuid,null,sucessor). PK(id) UQ(token_hash) idx(familia_id) FK(familia_id→refresh_token_families,**CASCADE**). Raw só no cookie httpOnly; usado_em set ⇒ reuso = ataque (revoga família). Tokens usados RETIDOS p/ reuse-detection enquanto a família vive.

email_verification_tokens — verificação de e-mail no cadastro. id(uuid,NN); conta_id(uuid,NN,sem FK); token_hash(varchar64,NN); expires_at(tstz,NN,+24h); verified_at(tstz,null); created_at(NN). PK(id) UQ(token_hash) idx(conta_id).

email_delivery_logs — auditoria entrega e-mail (webhook Resend/Svix). id(uuid,NN); resend_message_id(varchar100,NN); event_type(varchar50,NN); recipient_email(varchar254,NN); ocorrido_em(tstz,NN); payload(text,NN,JSON cru); created_at(NN). PK(id) idx(resend_message_id), idx(event_type).

whatsapp_delivery_logs — auditoria entrega WhatsApp (webhook Meta Cloud API). id(uuid,NN); meta_message_id(varchar100,NN); event_type(varchar50,NN); recipient_phone(varchar32,NN); ocorrido_em(tstz,NN); payload(text,NN,JSON cru); created_at(NN). PK(id) idx(meta_message_id), idx(event_type).

mensagens_suporte — ticket de contato com o suporte (aluno/treinador). id(uuid,NN); conta_id(uuid,NN); categoria(varchar20,NN,CategoriaSuporte); assunto(varchar120,NN); descricao(varchar2000,NN); criada_em(tstz,NN). PK(id) FK(conta_id→contas,RESTRICT) idx(conta_id). NÃO snapshota nome/e-mail (PII resolvida live no envio). Apagada na anonimização LGPD (`ExcluirPorContaIdAsync`).

### Planos & Recebimento (treinador↔plataforma)
planos_plataforma — planos de assinatura do treinador. id(uuid,NN); nome(varchar,NN); max_alunos(int,NN); preco(numeric,NN); is_ativo(bool,NN); tier(varchar,NN,TierPlano); descricao(varchar,null); created_at(NN); updated_at(null). PK(id).

conta_recebimento — Stripe Connect do treinador. id(uuid,NN); treinador_id(uuid,NN); stripe_connect_account_id(varchar,null); onboarding_completo(bool,NN,default false); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,CASCADE) UQ(treinador_id).

### Treinadores, Pacotes, Vínculos
treinadores — perfil treinador. id(uuid,NN); conta_id(uuid,NN); nome(varchar,NN); plano_plataforma_id(uuid,null); modo_pagamento_aluno(text,NN,default Plataforma,ModoPagamentoAluno); modo_pagamento_aluno_alterado_em(tstz,null); status(text,NN,TreinadorStatus); aprovado_por_id(uuid,null,sem FK); aprovado_em(tstz,null); telefone(varchar,null); anonimizado(bool,NN,default false,LGPD); created_at(NN); updated_at(null). PK(id) FK(conta_id→contas,RESTRICT) FK(plano_plataforma_id→planos_plataforma,RESTRICT) UQ(conta_id). (`modo_pagamento_aluno_alterado_em`: última troca de modo; null=nunca; cooldown 90d. Migration `AdicionarModoPagamentoAlteradoEm`.) Concurrency token = system column `xmin` (mapeado via EF, sem coluna física; migration `AdicionarConcurrencyTokenTreinador`): UPDATE concorrente do mesmo treinador aborta com DbUpdateConcurrencyException.

pacotes — serviços oferecidos pelo treinador. id(uuid,NN); treinador_id(uuid,NN); nome(varchar,NN); preco(numeric,NN); is_ativo(bool,NN); descricao(varchar,null); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,RESTRICT).

vinculos_treinador_aluno — relação treinador↔aluno (aprovação+pacote). id(uuid,NN); treinador_id(uuid,NN); aluno_id(uuid,NN); pacote_id(uuid,null); status(text,NN,VinculoStatus); aprovado_por_id(uuid,null); aprovado_em(tstz,null); data_inicio(tstz,null); data_fim(tstz,null); created_at(NN). PK(id) FK(treinador_id→treinadores,RESTRICT) FK(aluno_id→alunos,RESTRICT) FK(pacote_id→pacotes,RESTRICT).

logs_aprovacao — auditoria de aprovações/inativações. id(uuid,NN); tipo_acao(text,NN,TipoAcaoAprovacao); realizado_por_id(uuid,NN); entidade_id(uuid,NN); entidade_tipo(varchar,NN); observacao(varchar,null); created_at(NN). PK(id).

### Alunos
alunos — perfil aluno + anamnese. id(uuid,NN); conta_id(uuid,NN); nome(varchar,NN); email(varchar,null,VO); telefone(varchar,null); status(text,NN,AlunoStatus); dias_disponiveis(int,null); tempo_disponivel_minutos(int,null,TempoDisponivel); finalidade(varchar,null,FinalidadeTreino); foco_treino(varchar,null); nivel_condicionamento(varchar,null,NivelCondicionamento); limitacoes_fisicas(varchar,null); doencas(varchar,null); observacoes_adicionais(varchar,null); anonimizado(bool,NN,default false,LGPD); created_at(NN); updated_at(null). PK(id) FK(conta_id→contas,RESTRICT).

### Treinos & Exercícios
grupos_musculares — catálogo (seedado). id(uuid,NN); nome(varchar,NN); created_at(NN); updated_at(null). PK(id) UQ(nome).

exercicios — global (treinador_id null) ou do treinador. id(uuid,NN); treinador_id(uuid,null); grupo_muscular_id(uuid,NN); nome(varchar,NN); descricao(varchar,null); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,RESTRICT,null=global) FK(grupo_muscular_id→grupos_musculares,RESTRICT).

treinos — ficha de treino. id(uuid,NN); treinador_id(uuid,NN); nome(varchar,NN); objetivo(text,NN,ObjetivoTreino); dificuldade(text,NN,default Iniciante,DificuldadeTreino); data_inicio(date,null); data_fim(date,null); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,RESTRICT).

treino_exercicios — exercícios na ficha (ordenados). id(uuid,NN); treino_id(uuid,NN); exercicio_id(uuid,NN); ordem(int,NN); observacao(varchar,null). PK(id) FK(treino_id→treinos,CASCADE) FK(exercicio_id→exercicios,RESTRICT).

treino_exercicio_series — séries configuráveis. id(uuid,NN); treino_exercicio_id(uuid,NN); quantidade(int,NN); repeticoes_min(int,NN); repeticoes_max(int,null); carga(numeric,null); descanso(int,null); ordem(int,NN); descricao(varchar,null). PK(id) FK(treino_exercicio_id→treino_exercicios,CASCADE).

treino_alunos — atribuição de ficha a aluno. id(uuid,NN); treino_id(uuid,NN); aluno_id(uuid,NN); status(text,NN,TreinoAlunoStatus); created_at(NN); updated_at(null). PK(id) FK(treino_id→treinos,RESTRICT) FK(aluno_id→alunos,RESTRICT) UQ(treino_id WHERE status='Ativo').

execucoes_treino — sessão executada pelo aluno. id(uuid,NN); treino_id(uuid,NN); aluno_id(uuid,NN); data_execucao(tstz,NN); observacao(varchar,null); created_at(NN). PK(id) FK(treino_id→treinos,RESTRICT) FK(aluno_id→alunos,RESTRICT).

execucoes_exercicio — detalhe por exercício da execução. id(uuid,NN); execucao_treino_id(uuid,NN); treino_exercicio_id(uuid,NN); series_executadas(int,NN); repeticoes_executadas(int,NN); carga_executada(numeric,null); observacao(varchar,null). PK(id) FK(execucao_treino_id→execucoes_treino,CASCADE) FK(treino_exercicio_id→treino_exercicios,RESTRICT).

### Assinaturas & Pagamentos (aluno↔treinador)
assinaturas_aluno — assinatura recorrente. id(uuid,NN); vinculo_id(uuid,NN); pacote_id(uuid,NN); treinador_id(uuid,NN); aluno_id(uuid,NN); valor(numeric,NN); status(text,NN,AssinaturaAlunoStatus); tentativas_falhas_consecutivas(int,NN,default 0); data_inicio(tstz,NN); data_proxima_cobranca(tstz,NN); data_cancelamento(tstz,null); created_at(NN); updated_at(null). PK(id) FK(vinculo_id→vinculos_treinador_aluno,RESTRICT) FK(pacote_id→pacotes,RESTRICT) FK(treinador_id→treinadores,RESTRICT) FK(aluno_id→alunos,RESTRICT) UQ(vinculo_id).

pagamentos — cobranças da assinatura. id(uuid,NN); assinatura_aluno_id(uuid,NN); valor(numeric,NN); status(text,NN,PagamentoStatus); metodo_pagamento(text,NN,default Pix,MetodoPagamento); stripe_payment_intent_id(varchar,null); client_secret(varchar,null); pix_qr_code(text,null); pix_qr_code_url(varchar,null); pix_expiracao(tstz,null); data_pagamento(tstz,null); created_at(NN); updated_at(null). PK(id) FK(assinatura_aluno_id→assinaturas_aluno,RESTRICT) UQ(stripe_payment_intent_id) UQ(assinatura_aluno_id WHERE status='Pendente').

assinaturas_treinador — assinatura recorrente do plano da plataforma (treinador→plataforma). id(uuid,NN); treinador_id(uuid,NN); plano_plataforma_id(uuid,NN); plano_plataforma_id_agendado(uuid,null,downgrade p/ próxima renovação); valor(numeric,NN); status(text,NN,AssinaturaTreinadorStatus); tentativas_falhas_consecutivas(int,NN,default 0); data_inicio(tstz,NN); data_proxima_cobranca(tstz,NN); data_cancelamento(tstz,null); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,RESTRICT) FK(plano_plataforma_id→planos_plataforma,RESTRICT) FK(plano_plataforma_id_agendado→planos_plataforma,RESTRICT).

pagamentos_treinador — cobranças do plano do treinador (PaymentIntent direto-plataforma, sem Connect). id(uuid,NN); treinador_id(uuid,NN); assinatura_treinador_id(uuid,NN); valor(numeric,NN); status(text,NN,PagamentoStatus); metodo_pagamento(text,NN,default Pix,MetodoPagamento); finalidade(text,NN,FinalidadePagamentoTreinador); plano_alvo_id(uuid,null,plano da troca); stripe_payment_intent_id(varchar,null); client_secret(varchar,null); pix_qr_code(text,null); pix_qr_code_url(varchar,null); pix_expiracao(tstz,null); data_pagamento(tstz,null); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,RESTRICT) FK(assinatura_treinador_id→assinaturas_treinador,RESTRICT) UQ(stripe_payment_intent_id) UQ(assinatura_treinador_id WHERE status='Pendente').

### Projeção / IA
assinantes — read model derivado de Aluno (sync via domain events). id(uuid,NN); aluno_id(uuid,NN); nome(varchar,NN); email(varchar,null); created_at(NN); updated_at(null). PK(id) UQ(aluno_id) [sem FK física].

ai_token_usage — consumo de tokens IA por user/agente/dia. NON-EF. id(uuid,NN); user_id(uuid,NN); agent_type(varchar,NN); date(date,NN); token_count(int,NN). PK(id) UQ(user_id,agent_type,date).

### Observabilidade / Saúde (relatório diário)
health_report_config — config runtime do relatório diário de saúde (1 linha por schema). id(uuid,NN); ativo(bool,NN); hora_envio_utc(time,NN); destinatarios(text,NN,csv emails normalizados); incluir_liveness(bool,NN); incluir_kpis(bool,NN); incluir_entregabilidade(bool,NN); incluir_erros(bool,NN); ultimo_envio_em(tstz,null); created_at(NN); updated_at(null). PK(id).

health_snapshots — snapshot diário da saúde do ambiente. id(uuid,NN); capturado_em(tstz,NN); ambiente(varchar100,NN); status_geral(text,NN,StatusSaude); payload_json(text,NN,JSON das seções); created_at(NN). PK(id) idx(capturado_em).

error_logs — log de ERROR/Critical (sink custom, best-effort) p/ a seção de erros. id(uuid,NN); ocorrido_em(tstz,NN); nivel(varchar20,NN); origem(varchar256,NN); mensagem(varchar4000,NN,truncada); created_at(NN). PK(id) idx(ocorrido_em).

### Outbox de efeitos externos
outbox_efeitos — fila durável de efeito externo pós-commit (entrega garantida + retry; gravada no MESMO commit do agregado de origem). id(uuid,NN); tipo(varchar200,NN, `evt:<CLR>`|`fx:<nome>`); payload(jsonb,NN); status(text,NN,OutboxStatus); tentativas(int,NN); proxima_tentativa(tstz,NN, scan do worker `<= agora`); ultimo_erro(text,null); chave_idempotencia(varchar300,NN); criado_em(tstz,NN); processado_em(tstz,null). PK(id) UQ(chave_idempotencia) idx(status,proxima_tentativa). [sem FK — desacoplado; payload carrega ids].

## ACESSOS / ROLES (Supabase)
- forzion_api: usado em `ConnectionStrings:AppConnection` (runtime do app + `dotnet ef`). Dono dos objetos em homolog/develop. Em public: NÃO é dono (objetos do postgres) → precisa `GRANT ALL ON ALL TABLES IN SCHEMA public TO forzion_api` + USAGE/CREATE. Search Path da connection define o schema ativo.
- postgres: `ConnectionStrings:DefaultConnection`. Admin/DDL privilegiado; dono dos objetos de public. NÃO é superuser pleno no Supabase → NÃO consegue `SET ROLE forzion_api`.
- Runtime: app conecta como forzion_api. Development/Homolog → `Program.cs` roda MigrateAsync + SeedAsync no startup.
- ⚠️ `Program.cs` adiciona User Secrets DEPOIS do CreateBuilder → secrets sobrescrevem env vars em RUNTIME. `dotnet run` em Development conecta no SUPABASE REMOTO (não local) e migra/seeda lá. `AppDbContextFactory` (design-time, `dotnet ef`) adiciona env por último → env vence (override por env funciona só no ef).

## MIGRATION-SAFETY (política de mudança de schema)

### Padrão expand/contract (obrigatório p/ mudanças breaking)
Toda mudança que quebra código em voo ou impede rollback de deploy DEVE seguir expand/contract em DEPLOYS SEPARADOS:
1. **EXPAND** — `AddColumn` nullable (ou com `defaultValue`); código escreve em ambos (velho+novo).
2. **BACKFILL** — preencher histórico em lotes idempotentes (ver §BACKFILL). Validar dados antes do contrato.
3. **CONTRACT** — drop do velho só após o código novo estar estável em produção.

Nunca drop+add atômico num app vivo: quebra requests em voo e trava rollback de código.

### Regra `AddColumn` NOT NULL
`AddColumn` com `NOT NULL` EXIGE `defaultValue` na migration (sem default o Postgres rejeita para tabelas com dados). Precedente no repo: migration `AdicionarModoPagamentoAlteradoEm` usa `nullable: true` para adicionar sem default; `AdicionarAnonimizadoEmAlunosETreinadores` usa `defaultValue: false` no `AddColumn boolean NOT NULL`. Escolha o padrão adequado ao campo; documentar o motivo no migration file.

### Checklist — migration DESTRUTIVA (DROP column/table, type narrowing, rename)
Uma migration é destrutiva se: remove coluna/tabela, estreita tipo (varchar maior→menor, numeric→int), renomeia coluna/tabela sem alias de compatibilidade, ou remove UQ/FK que o código ainda referencia.

Antes de mergear PR com migration destrutiva:
- [ ] **Backup verificado**: backup recente (`specification-dr §1`) confirmado existente e íntegro — não assumir; documentar a evidência no PR.
- [ ] **Drill de restore executado**: restaurar em ambiente isolado e validar integridade (contagem tabelas = total EF + NON-EF do §STACK, seed presente, migrations em dia) — procedimento em `specification-dr §2`. Não mergear sem o drill feito.
- [ ] **Expand/contract respeitado**: a fase CONTRACT só chega neste PR após código novo estável (EXPAND e BACKFILL já deployados e monitorados).
- [ ] **Schema-agnostic**: sem qualificador de schema hardcoded na migration.
- [ ] **Rollback planejado**: se o schema for revertido, o código anterior ainda funciona? Documentar no PR.

> Enforcement: processo/revisão (não gate de CI). Ver `specification-dr §6`.

### DECISÃO PENDENTE — `MigrateAsync` no startup vs. pipeline de deploy
**Status: DECISION-PENDING (2026-06-10)**

Situação atual: `Program.cs` chama `MigrateAsync` + `SeedAsync` no startup em Dev e Homolog, aplicando migrations diretamente contra o banco remoto ao subir a app (`specification-db §ACESSOS`).

Trade-off:
- **Startup (atual)**: zero infraestrutura extra; zero chance de subir sem schema atualizado; mas migrations destrutivas/backfill de dados rodam com a app já recebendo tráfego (janela de risco), e rollback de imagem NÃO reverte schema (risco DR).
- **Pipeline (alvo)**: migration vira step do deploy antes do `docker compose up`; app sobe só após schema estar pronto; habilita dry-run e revisão do SQL antes de aplicar; exige step extra no CI/deploy script (ex.: `dotnet ef database update` ou `dotnet ef migrations script | psql`).

Migrar para pipeline é pré-requisito para migrations destrutivas sem janela de downtime e habilita rollback de schema independente da imagem. Decisão requer aprovação do usuário (mudança de processo de deploy — `specification-infrastructure`).

## BACKFILL & MIGRAÇÃO DE DADOS (runtime — migration cobre SCHEMA, isto cobre DADO)
Migrations criam/alteram estrutura; mudar/preencher DADO existente tem regras próprias. Lembrar: migration destrutiva/backfill roda no startup em Dev/Homolog contra o REMOTO (§ACESSOS) → afeta dado real de homolog.
- **Expand/contract (zero-downtime + rollback-safe)** — mudança breaking em 3 passos, em DEPLOYS SEPARADOS: (1) EXPAND — add coluna nullable / nova tabela; código escreve em ambas (velha+nova); (2) BACKFILL — popular o histórico em batch; (3) CONTRACT — remover o velho só depois do código novo estável. NUNCA drop+add atômico num app vivo (quebra requests em voo + impede rollback de código).
- **Backfill em batch idempotente** — preencher dado histórico em LOTES (não um `UPDATE` de N milhões = lock longo/timeout/bloat de WAL); re-rodável sem efeito duplo (guard por estado, ex. `WHERE col IS NULL`).
- **Rollback de DADO ≠ rollback de schema** — código forward-compat (lê velho E novo) permite reverter o deploy de código SEM reverter a migration. É a condição que torna o deploy revertível ([specification-dr §4]). Migration destrutiva sem janela expand/contract trava rollback → exige backup verificado ANTES ([specification-dr §1]).
- **Schema-agnostic** (search_path, §STACK) vale também p/ backfill: SQL sem qualificador de schema; aplica por schema via Search Path.
- Concorrência durante backfill: escrita nova concorrente ao backfill não pode ser sobrescrita — backfill toca só linhas antigas (`WHERE`-guard), código novo é a fonte das linhas novas. Cross-ref [specification-concurrency].

## DICAS — ALTERAÇÕES DE BANCO
- Nova migration: `dotnet ef migrations add <Nome>`. Manter agnóstica: NÃO inserir `schema:`/`principalSchema:`/`newSchema:` nem prefixar SQL raw com schema. Atualizar ESTE arquivo.
- Aplicar em schema X: `dotnet ef database update` com `Search Path=X`. Schema novo: `CREATE SCHEMA IF NOT EXISTS X` antes.
- Sincronizar develop/public: `dotnet ef database update` com Search Path. Estrutura antiga divergente → limpar tabelas app primeiro (em public usar conn postgres, pois forzion_api não é dono dos legados). Depois clonar ai_token_usage + copiar seed de homolog.
- pg_dump/restore: servidor PG17, cliente local PG16 → version mismatch. Usar container postgres:17 OU `dotnet ef migrations script` (offline) p/ extrair DDL.
- Conectividade: host direto Supabase (db.<ref>.supabase.co) é IPv6-only → containers Docker NÃO alcançam; usar psql do host p/ ops ad-hoc.
- Conexão RUNTIME do app (homolog/prod): **Session pooler Supabase** (host `aws-0-<região>.pooler.supabase.com`, porta **5432**, user `forzion_api.<ref>`) — IPv4 + pooling; suporta prepared statements + advisory lock → `MigrateAsync()`/seed no startup (`Program.cs`, Dev/Homolog) funcionam (DR-01, decidido 2026-06-11). NÃO usar **Transaction pooler (:6543)** na string do app: sem prepared stmt/session var/temp table → migration no boot quebra. Direct (`db.<ref>`, IPv6-only) = só ops ad-hoc/fallback. String EXATA: Dashboard → Connect → Session pooler; vive em `DB_CONNECTION` da VM (operacional, fora do repo).
- Scripts do `dotnet ef`: gravados com BOM UTF-8 → remover (`sed '1s/^\xEF\xBB\xBF//'`) antes de aplicar via psql.
- Migration destrutiva/backfill roda no startup em Dev/Homolog contra o REMOTO. Validar antes; backfills afetam dados reais de homolog.
- Seed: 9 grupos, 90 exercícios globais, 5 planos, 1 admin (DataSeeder no startup, idempotente, ou cópia de homolog). Admin nasce email_verificado=true.
