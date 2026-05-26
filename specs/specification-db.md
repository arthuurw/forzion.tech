# specification-db — estrutura de banco (forzion.tech)

DOC PARA AGENTES. Fonte de verdade da estrutura de banco. Formato denso. Consultar antes de qualquer alteração de banco.

Notação coluna: `nome(tipo, NN|null[, nota])`. PK / FK(col→tabela, ONDELETE) / UQ(cols[, parcial WHERE]). tstz = timestamptz. Enums persistidos como `text` (HasConversion<string>; valor = nome do enum).

## MANUTENÇÃO DESTE ARQUIVO
- Este arquivo DEVE ser mantido atualizado. Sempre que uma alteração RELEVANTE de estrutura for feita (nova migration, tabela, coluna, FK, índice, enum, mudança de tipo/nullability/default), ajustar este arquivo NA MESMA TAREFA.
- Vive em `specs/` (versionado; NÃO confundir com `.specs/` gitignorado). Arquivos de `specs/` devem ser commitados.
- Ao mudar contagem de tabelas/migrations, atualizar os números nas seções relevantes.

## STACK & SCHEMAS
- PostgreSQL 17 (Supabase). EF Core 8, snake_case naming convention. App = ASP.NET Core 8, DDD.
- Migrations SCHEMA-AGNOSTIC: `AppDbContext` SEM `HasDefaultSchema`. Schema-alvo vem do `search_path` da connection (ex.: `Search Path=homolog`). Mesmas migrations aplicam em qualquer schema. `MigrationsHistoryTable("__EFMigrationsHistory")` sem schema (segue search_path).
- Schemas com estrutura IDÊNTICA: `homolog` (deploy ativo, canônico), `develop` (sandbox), `public` (sandbox/legado sincronizado). 26 tabelas cada (25 EF + ai_token_usage).
- `ai_token_usage`: existe nos 3 schemas mas NÃO é gerenciada por migration EF (criada fora do EF). Recriar via `CREATE TABLE <schema>.ai_token_usage (LIKE homolog.ai_token_usage INCLUDING ALL)`.
- 24 migrations EF aplicadas. Tabela de controle `__EFMigrationsHistory` por schema.

## CONVENÇÕES
- PK: `id` uuid gerado na app (Guid.NewGuid), não pelo banco. Exceção: `tokens_revogados` PK=`jti`.
- Timestamps: `created_at`(NN), `updated_at`(null) tstz. Datas de negócio: prefixo `data_*`.
- Email: VO normalizado lowercase.
- Money: numeric.
- FK default ON DELETE RESTRICT. CASCADE só em filhos de composição (ver por tabela).
- Tokens (password_reset/email_verification): armazenam SHA-256 hex(64) do token; cru só no e-mail; `conta_id` SEM FK física (só índice).
- UQ parciais impõem regra de negócio (pagamentos 1 Pendente/assinatura; treino_alunos 1 Ativo/treino).

## ENUMS (text)
- TipoConta (contas.tipo_conta): SystemAdmin|Treinador|Aluno
- SystemRole (system_users.role): SuperAdmin|Support|Operator
- UsuarioStatus (system_users.status): Ativo|Inativo
- TreinadorStatus (treinadores.status): AguardandoAprovacao|Ativo|Inativo
- AlunoStatus (alunos.status): AguardandoAprovacao|Ativo|Inativo
- VinculoStatus (vinculos_treinador_aluno.status): AguardandoAprovacao|Ativo|Inativo
- TierPlano (planos_plataforma.tier): Free|Basic|Pro|ProPlus|Elite
- TreinoAlunoStatus (treino_alunos.status): Ativo|Inativo
- ObjetivoTreino (treinos.objetivo): Hipertrofia|Forca|Resistencia|Emagrecimento|Reabilitacao
- DificuldadeTreino (treinos.dificuldade, default Iniciante): Iniciante|Intermediario|Avancado
- FinalidadeTreino (alunos.finalidade): Hipertrofia|Emagrecimento|CondicionamentoFisico|Saude|PerformanceEsportiva|Reabilitacao|Outro
- NivelCondicionamento (alunos.nivel_condicionamento): Sedentario|Iniciante|Intermediario|Avancado
- TempoDisponivel (alunos.tempo_disponivel_minutos, int): 30|45|60|90|120
- AssinaturaAlunoStatus (assinaturas_aluno.status): Pendente|Ativa|Inadimplente|Cancelada
- PagamentoStatus (pagamentos.status): Pendente|Pago|Expirado|Falhou
- MetodoPagamento (pagamentos.metodo_pagamento, default Pix): Pix|Cartao
- TipoAcaoAprovacao (logs_aprovacao.tipo_acao): AprovacaoTreinador|ReprovacaoTreinador|InativacaoTreinador|AprovacaoVinculo|ReprovacaoVinculo|InativacaoVinculo|AtribuicaoPlanTreinador
- GrupoMuscular (seed de grupos_musculares; não é coluna): Peito|Costas|Ombro|Biceps|Triceps|Pernas|Gluteos|Core|FullBody

## TABELAS

### Identidade & Auth
contas — credenciais + tipo. id(uuid,NN); email(varchar256,NN); password_hash(text,NN,bcrypt); tipo_conta(text,NN,TipoConta); email_verificado(bool,NN,default false); verificado_em(tstz,null); created_at(NN); updated_at(null). PK(id) UQ(email).

system_users — perfil admin plataforma. id(uuid,NN); conta_id(uuid,NN); nome(varchar,NN); role(text,NN,SystemRole); status(text,NN,UsuarioStatus); created_at(NN); updated_at(null). PK(id) FK(conta_id→contas,RESTRICT).

tokens_revogados — blacklist JWT (logout). jti(uuid,NN); expira_em(tstz,NN). PK(jti). Limpeza por hosted service.

password_reset_tokens — reset de senha. id(uuid,NN); conta_id(uuid,NN,sem FK); token_hash(varchar64,NN); expires_at(tstz,NN,+1h); used_at(tstz,null); created_at(NN). PK(id) UQ(token_hash) idx(conta_id).

email_verification_tokens — verificação de e-mail no cadastro. id(uuid,NN); conta_id(uuid,NN,sem FK); token_hash(varchar64,NN); expires_at(tstz,NN,+24h); verified_at(tstz,null); created_at(NN). PK(id) UQ(token_hash) idx(conta_id).

email_delivery_logs — auditoria entrega e-mail (webhook Resend/Svix). id(uuid,NN); resend_message_id(varchar100,NN); event_type(varchar50,NN); recipient_email(varchar254,NN); ocorrido_em(tstz,NN); payload(text,NN,JSON cru); created_at(NN). PK(id) idx(resend_message_id), idx(event_type).

### Planos & Recebimento (treinador↔plataforma)
planos_plataforma — planos de assinatura do treinador. id(uuid,NN); nome(varchar,NN); max_alunos(int,NN); preco(numeric,NN); is_ativo(bool,NN); tier(varchar,NN,TierPlano); descricao(varchar,null); created_at(NN); updated_at(null). PK(id).

conta_recebimento — Stripe Connect do treinador. id(uuid,NN); treinador_id(uuid,NN); stripe_connect_account_id(varchar,null); onboarding_completo(bool,NN,default false); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,CASCADE) UQ(treinador_id).

### Treinadores, Pacotes, Vínculos
treinadores — perfil treinador. id(uuid,NN); conta_id(uuid,NN); nome(varchar,NN); plano_plataforma_id(uuid,null); status(text,NN,TreinadorStatus); aprovado_por_id(uuid,null,sem FK); aprovado_em(tstz,null); telefone(varchar,null); created_at(NN); updated_at(null). PK(id) FK(conta_id→contas,RESTRICT) FK(plano_plataforma_id→planos_plataforma,RESTRICT) UQ(conta_id).

pacotes — serviços oferecidos pelo treinador. id(uuid,NN); treinador_id(uuid,NN); nome(varchar,NN); preco(numeric,NN); is_ativo(bool,NN); descricao(varchar,null); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,RESTRICT).

vinculos_treinador_aluno — relação treinador↔aluno (aprovação+pacote). id(uuid,NN); treinador_id(uuid,NN); aluno_id(uuid,NN); pacote_id(uuid,null); status(text,NN,VinculoStatus); aprovado_por_id(uuid,null); aprovado_em(tstz,null); data_inicio(tstz,null); data_fim(tstz,null); created_at(NN). PK(id) FK(treinador_id→treinadores,RESTRICT) FK(aluno_id→alunos,RESTRICT) FK(pacote_id→pacotes,RESTRICT).

logs_aprovacao — auditoria de aprovações/inativações. id(uuid,NN); tipo_acao(text,NN,TipoAcaoAprovacao); realizado_por_id(uuid,NN); entidade_id(uuid,NN); entidade_tipo(varchar,NN); observacao(varchar,null); created_at(NN). PK(id).

### Alunos
alunos — perfil aluno + anamnese. id(uuid,NN); conta_id(uuid,NN); nome(varchar,NN); email(varchar,null,VO); telefone(varchar,null); status(text,NN,AlunoStatus); dias_disponiveis(int,null); tempo_disponivel_minutos(int,null,TempoDisponivel); finalidade(varchar,null,FinalidadeTreino); foco_treino(varchar,null); nivel_condicionamento(varchar,null,NivelCondicionamento); limitacoes_fisicas(varchar,null); doencas(varchar,null); observacoes_adicionais(varchar,null); created_at(NN); updated_at(null). PK(id) FK(conta_id→contas,RESTRICT).

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
assinaturas_aluno — assinatura recorrente. id(uuid,NN); vinculo_id(uuid,NN); pacote_id(uuid,NN); treinador_id(uuid,NN); aluno_id(uuid,NN); valor(numeric,NN); status(text,NN,AssinaturaAlunoStatus); data_inicio(tstz,NN); data_proxima_cobranca(tstz,NN); data_cancelamento(tstz,null); created_at(NN); updated_at(null). PK(id) FK(vinculo_id→vinculos_treinador_aluno,RESTRICT) FK(pacote_id→pacotes,RESTRICT) FK(treinador_id→treinadores,RESTRICT) FK(aluno_id→alunos,RESTRICT) UQ(vinculo_id).

pagamentos — cobranças da assinatura. id(uuid,NN); assinatura_aluno_id(uuid,NN); valor(numeric,NN); status(text,NN,PagamentoStatus); metodo_pagamento(text,NN,default Pix,MetodoPagamento); stripe_payment_intent_id(varchar,null); client_secret(varchar,null); pix_qr_code(text,null); pix_qr_code_url(varchar,null); pix_expiracao(tstz,null); data_pagamento(tstz,null); created_at(NN); updated_at(null). PK(id) FK(assinatura_aluno_id→assinaturas_aluno,RESTRICT) UQ(stripe_payment_intent_id) UQ(assinatura_aluno_id WHERE status='Pendente').

### Projeção / IA
assinantes — read model derivado de Aluno (sync via domain events). id(uuid,NN); aluno_id(uuid,NN); nome(varchar,NN); email(varchar,null); created_at(NN); updated_at(null). PK(id) UQ(aluno_id) [sem FK física].

ai_token_usage — consumo de tokens IA por user/agente/dia. NON-EF. id(uuid,NN); user_id(uuid,NN); agent_type(varchar,NN); date(date,NN); token_count(int,NN). PK(id) UQ(user_id,agent_type,date).

## ACESSOS / ROLES (Supabase)
- forzion_api: usado em `ConnectionStrings:AppConnection` (runtime do app + `dotnet ef`). Dono dos objetos em homolog/develop. Em public: NÃO é dono (objetos do postgres) → precisa `GRANT ALL ON ALL TABLES IN SCHEMA public TO forzion_api` + USAGE/CREATE. Search Path da connection define o schema ativo.
- postgres: `ConnectionStrings:DefaultConnection`. Admin/DDL privilegiado; dono dos objetos de public. NÃO é superuser pleno no Supabase → NÃO consegue `SET ROLE forzion_api`.
- Runtime: app conecta como forzion_api. Development/Homolog → `Program.cs` roda MigrateAsync + SeedAsync no startup.
- ⚠️ `Program.cs` adiciona User Secrets DEPOIS do CreateBuilder → secrets sobrescrevem env vars em RUNTIME. `dotnet run` em Development conecta no SUPABASE REMOTO (não local) e migra/seeda lá. `AppDbContextFactory` (design-time, `dotnet ef`) adiciona env por último → env vence (override por env funciona só no ef).

## DICAS — ALTERAÇÕES DE BANCO
- Nova migration: `dotnet ef migrations add <Nome>`. Manter agnóstica: NÃO inserir `schema:`/`principalSchema:`/`newSchema:` nem prefixar SQL raw com schema. Atualizar ESTE arquivo.
- Aplicar em schema X: `dotnet ef database update` com `Search Path=X`. Schema novo: `CREATE SCHEMA IF NOT EXISTS X` antes.
- Sincronizar develop/public: `dotnet ef database update` com Search Path. Estrutura antiga divergente → limpar tabelas app primeiro (em public usar conn postgres, pois forzion_api não é dono dos legados). Depois clonar ai_token_usage + copiar seed de homolog.
- pg_dump/restore: servidor PG17, cliente local PG16 → version mismatch. Usar container postgres:17 OU `dotnet ef migrations script` (offline) p/ extrair DDL.
- Conectividade: host direto Supabase (db.<ref>.supabase.co) é IPv6-only → containers Docker NÃO alcançam; usar psql do host p/ ops ad-hoc.
- Scripts do `dotnet ef`: gravados com BOM UTF-8 → remover (`sed '1s/^\xEF\xBB\xBF//'`) antes de aplicar via psql.
- Migration destrutiva/backfill roda no startup em Dev/Homolog contra o REMOTO. Validar antes; backfills afetam dados reais de homolog.
- Seed: 9 grupos, 90 exercícios globais, 5 planos, 1 admin (DataSeeder no startup, idempotente, ou cópia de homolog). Admin nasce email_verificado=true.
