# specification-db — estrutura de banco (forzion.tech)

DOC PARA AGENTES. Fonte de verdade da estrutura de banco. Formato denso. Consultar antes de qualquer alteração de banco.

Notação coluna: `nome(tipo, NN|null[, nota])`. PK / FK(col→tabela, ONDELETE) / UQ(cols[, parcial WHERE]). tstz = timestamptz. Enums persistidos como `text` (HasConversion<string>; valor = nome do enum).

## MANUTENÇÃO DESTE ARQUIVO
- Este arquivo DEVE ser mantido atualizado. Sempre que uma alteração RELEVANTE de estrutura for feita (nova migration, tabela, coluna, FK, índice, enum, mudança de tipo/nullability/default), ajustar este arquivo NA MESMA TAREFA.
- Ao mudar contagem de tabelas/migrations, atualizar os números nas seções relevantes.

## STACK & SCHEMAS
- PostgreSQL 17 (Supabase). EF Core 10, snake_case naming convention. Stack macro da app em AGENTS.md §STACK.
- Migrations SCHEMA-AGNOSTIC: `AppDbContext` SEM `HasDefaultSchema`. Schema-alvo vem do `search_path` da connection (ex.: `Search Path=homolog`). Mesmas migrations aplicam em qualquer schema.
- **History table — RUNTIME pina o schema; DESIGN-TIME não** (gotcha Npgsql 8.0.11): `NpgsqlHistoryRepository.ExistsSql` checa a existência da `__EFMigrationsHistory` com schema HARDCODED em `public` (`n.nspname = TableSchema ?? "public"`), mas CREATE/leitura usam o search_path. Sem pinar, num alvo cujo `public` NÃO tem a history (ex.: dry-run clonando só `homolog`), o Exists dá falso-negativo → CREATE plano cai no search_path e colide (`42P07 already exists`). Por isso o **runtime** (`InfrastructureExtensions`, usado por `app migrate`/dry-run) pina `MigrationsHistoryTable("__EFMigrationsHistory", <1º schema do Search Path>)` via `MigrationHistorySchemaResolver` → Exists/CREATE/leitura no MESMO schema. O **design-time** (`AppDbContextFactory`) fica SEM schema (unqualified) de propósito: `dotnet ef migrations script` precisa gerar SQL portável e reusável por schema (§APLICAÇÃO DE MIGRATIONS depende de unqualified + `SET search_path`).
- Schemas com estrutura IDÊNTICA: `homolog` (homologação, deploy ativo canônico), `develop` (sandbox), `public` (**PRODUÇÃO** — dados reais de usuários). **SAFETY**: `public` é PRODUÇÃO, NÃO sandbox apesar do nome default do Postgres — NUNCA semear conta de teste, rodar DML ad-hoc nem apontar fixture E2E p/ `public`. Toda escrita de teste/seed vai p/ `homolog`. Qualificar SEMPRE o schema (`homolog.<tabela>`) ou pinar `Search Path=homolog` antes de DML manual; o ownership por postgres em `public` (vs forzion_api em homolog/develop) é a 2ª linha de defesa, não a 1ª. 45 tabelas BASE cada (43 entidades EF + `ai_token_usage` não-EF + `__EFMigrationsHistory` de controle) após todas as migrations aplicadas (inclui MFA e DataProtection, já merjadas; a tabela fiscal `notas_fiscais` foi dropada pela migration `RemoverNotasFiscais`, mantidas as colunas `dados_fiscais_*` em `treinadores`). Nível atual por schema pode divergir — conferir antes (§APLICAÇÃO DE MIGRATIONS §GOTCHA).
- `ai_token_usage`: existe nos 3 schemas mas NÃO é gerenciada por migration EF (criada fora do EF). Recriar via `CREATE TABLE <schema>.ai_token_usage (LIKE homolog.ai_token_usage INCLUDING ALL)`.
- 56 migrations EF (arquivos não-Designer/Snapshot em `Infrastructure/Migrations/`; última por timestamp `AdicionarEmailEnviadoHealthSnapshot` (20260709155310)). Tabela de controle `__EFMigrationsHistory` por schema (colunas snake_case: `migration_id` varchar(150) PK, `product_version` varchar(32); EF `ProductVersion` = versão do pacote em `forzion.tech.Infrastructure.csproj`, não fixar aqui).
- `AdicionarConcurrencyTokenTreinador`: mapeia o system column `xmin` de `treinadores` como concurrency token (concorrência otimista). `AdicionarConcurrencyTokenPagamentos`: idem para `pagamentos` + `pagamentos_treinador` (configs `PagamentoConfiguration.cs:16`, `PagamentoTreinadorConfiguration.cs:16`). NÃO geram DDL — o `AddColumn` no `.cs` é artefato de modelo; o SQL gerado só insere a linha de history. Aplicar é no-op estrutural (só registra a migration).

## APLICAÇÃO DE MIGRATIONS (multi-schema — descobertas operacionais)
Projeto Supabase único: `forzion` (ref `fdpdbtiuuitndbeujcbj`, região sa-east-1). Os 3 schemas vivem no MESMO banco.

- **GOTCHA — schemas DRIFTAM**: develop/homolog/public podem estar em níveis DIFERENTES de migration (ex.: 2026-06-16 develop+public estavam 5 migrations atrás de homolog — niveladas a 37 cada via MCP, seguindo esta seção). SEMPRE conferir antes: `select max(migration_id), count(*) from <schema>."__EFMigrationsHistory"`. NÃO assumir que estão iguais.
- **Caminho normal (preferido)**: `dotnet ef database update` com connection `ConnectionStrings:AppConnection` + `Search Path=<schema>` (uma vez por schema). Conecta como `forzion_api` (dono em develop/homolog).
- **Caminho via Supabase MCP** (quando sem connection local): o MCP conecta como `postgres` (NÃO superuser; tem `createrole`+`bypassrls`+`admin_option` em `forzion_api`).
  - **OWNERSHIP DIVERGE POR SCHEMA** (gotcha central): tabelas em `develop`/`homolog` são owned por **`forzion_api`**; em `public` são owned por **`postgres`**. DDL exige ser owner.
    - develop/homolog: `GRANT forzion_api TO postgres WITH SET TRUE;` (membership nasce com `set_option=false`) → `SET ROLE forzion_api;` → DDL → no fim reverter `GRANT forzion_api TO postgres WITH SET FALSE;`.
    - public: rodar como `postgres` direto (já é owner); NÃO usar SET ROLE.
  - Em todos: `SET search_path TO <schema>;` antes do DDL (migrations são schema-agnostic, sem qualificador).
  - **`public` exige GRANT manual nas tabelas novas**: tabelas EF em public são criadas por postgres com RLS desabilitado e `GRANT ALL` para `anon, authenticated, forzion_api, service_role` (espelhar o padrão de `public.pagamentos`). Em develop/homolog isso é automático (dono = forzion_api, role da app). **Sequências**: tabela public com PK int/identity (ex.: `data_protection_keys`, store do ASP.NET DataProtection) cria sequence owned por postgres — forzion_api ganha USAGE mas NÃO SELECT por default. `pg_dump` (backup DR, roda como forzion_api) lê `last_value` → exige SELECT, senão `permission denied for sequence` aborta o backup do schema public. Grant obrigatório: `GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO forzion_api` + `ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO forzion_api` (cobre futuras). (Incidente 2026-06-29/30, issues #260/#271: a premissa antiga "PKs uuid → sem sequence" quebrou com DataProtection.)
  - Gerar SQL mínimo por schema: `dotnet ef migrations script <ultimaAplicada> <alvo> --project forzion.tech.Infrastructure --startup-project forzion.tech.Api`; remover linhas `START TRANSACTION;`/`COMMIT;` e rodar tudo num `execute_sql` (atômico por schema). `--idempotent` gera guards `IF NOT EXISTS(... migration_id ...)` se preferir um script único.
  - History: inserir `INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) VALUES ('<id>','<ProductVersion do dotnet ef>')` por migration aplicada (o `dotnet ef migrations script` já inclui o valor correto).

## CONVENÇÕES
- PK: `id` uuid gerado na app (Guid.NewGuid), não pelo banco. Exceção: `tokens_revogados` PK=`jti`.
- Timestamps: `created_at`(NN), `updated_at`(null) tstz. Datas de negócio: prefixo `data_*`.
- Email: VO normalizado lowercase. Coluna value-converted (`HasConversion`) → EF NÃO traduz string-fn/LIKE/ILike sobre `c.Email.Value` (estoura `could not be translated` em runtime); filtro por sufixo/padrão de e-mail roda em memória (ex.: `ListarTesteAsync` materializa + `EndsWith`).
- Money: numeric.
- FK default ON DELETE RESTRICT. CASCADE só em filhos de composição (ver por tabela).
- Tokens (password_reset/email_verification/refresh): armazenam SHA-256 hex(64) do token; cru só no e-mail (reset/verify) ou no cookie httpOnly (refresh); `conta_id` SEM FK física (só índice). EXCEÇÃO: `refresh_tokens.familia_id` TEM FK física com ON DELETE CASCADE (GC/purga da família apaga os tokens no nível do banco).
- UQ parciais impõem regra de negócio (pagamentos 1 Pendente/assinatura; treino_alunos 1 Ativo/treino; assinaturas_treinador ≤1 não-Cancelada/treinador — AD-002).
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
contas — credenciais + tipo. id(uuid,NN); email(varchar256,NN); password_hash(text,NN,bcrypt); tipo_conta(text,NN,TipoConta); email_verificado(bool,NN,default false); verificado_em(tstz,null); anonimizada_em(tstz,null,LGPD); sessoes_invalidas_antes_de_utc(tstz,null,SEC-05 token-epoch — rejeita access token com nbf anterior); notificacoes_engajamento_email_opt_out(bool,NN,default false,NOTIF-13 opt-out do e-mail de engajamento; billing/transacional IGNORA a flag); created_at(NN); updated_at(null). PK(id) UQ(email).

system_users — perfil admin plataforma. id(uuid,NN); conta_id(uuid,NN); nome(varchar,NN); role(text,NN,SystemRole); status(text,NN,UsuarioStatus); created_at(NN); updated_at(null). PK(id) FK(conta_id→contas,RESTRICT).

tokens_revogados — blacklist JWT (logout). jti(uuid,NN); expira_em(tstz,NN). PK(jti). Limpeza por hosted service.

password_reset_tokens — reset de senha. id(uuid,NN); conta_id(uuid,NN,sem FK); token_hash(varchar64,NN); expires_at(tstz,NN,+1h); used_at(tstz,null); created_at(NN). PK(id) UQ(token_hash) UQ-parcial(conta_id) WHERE used_at IS NULL (`ux_password_reset_tokens_conta_id_pendente` — ≤1 pendente por conta; serializa 2 forgot-password concorrentes → 23505 tratado como idempotente; ver concurrency).

redefinicao_senha_segundo_fator — lockout durável por-conta do 2º fator (TOTP) do reset de senha; imune a rotação de IP (a defesa por-IP do limiter `auth` é camada extra). id(uuid,NN); conta_id(uuid,NN,sem FK); tentativas(int,NN,cap 5=`MaximoTentativas`→`Bloqueado` dentro da janela); janela_inicio(tstz,NN,janela 15min — expira→reinicia contador); atualizado_em(tstz,NN). PK(id) UQ(conta_id, `ix_redefinicao_senha_segundo_fator_conta_id` — ≤1 linha por conta). Sem PII; falha no 2º fator commita o incremento mesmo sem consumir token/trocar senha.

refresh_token_families — agregado de sessão (rotação de refresh). id(uuid,NN); conta_id(uuid,NN,sem FK); criada_em(tstz,NN); absoluto_expira_em(tstz,NN,teto absoluto server-side); revogada_em(tstz,null); motivo_revogacao(varchar32,null,MotivoRevogacaoFamilia); rotulo(varchar256,null,device/user-agent). PK(id) idx(conta_id), idx(revogada_em). GC `LimparExpiradasAsync` (revogada/pós-absoluto); purga LGPD `ExcluirPorContaIdAsync`.

refresh_tokens — token de refresh single-use (cadeia de rotação). id(uuid,NN); familia_id(uuid,NN); token_hash(varchar64,NN,SHA-256 hex); criado_em(tstz,NN); expira_em(tstz,NN,idle); usado_em(tstz,null); substituido_por_id(uuid,null,sucessor). PK(id) UQ(token_hash) idx(familia_id) FK(familia_id→refresh_token_families,**CASCADE**). Raw só no cookie httpOnly; usado_em set ⇒ reuso = ataque (revoga família). Tokens usados RETIDOS p/ reuse-detection enquanto a família vive.

email_verification_tokens — verificação de e-mail no cadastro. id(uuid,NN); conta_id(uuid,NN,sem FK); token_hash(varchar64,NN); expires_at(tstz,NN,+24h); verified_at(tstz,null); created_at(NN). PK(id) UQ(token_hash) idx(conta_id).

email_delivery_logs — auditoria entrega e-mail (webhook Resend/Svix). id(uuid,NN); resend_message_id(varchar100,NN); event_type(varchar50,NN); recipient_email(varchar254,NN); ocorrido_em(tstz,NN); payload(text,NN,JSON cru); created_at(NN). PK(id) idx(resend_message_id), idx(event_type).

whatsapp_delivery_logs — auditoria entrega WhatsApp (webhook Meta Cloud API). id(uuid,NN); meta_message_id(varchar100,NN); event_type(varchar50,NN); recipient_phone(varchar32,NN); ocorrido_em(tstz,NN); payload(text,NN,JSON cru); created_at(NN). PK(id) idx(meta_message_id), idx(event_type).

mensagens_suporte — ticket de contato com o suporte (aluno/treinador). id(uuid,NN); conta_id(uuid,NN); categoria(varchar20,NN,CategoriaSuporte); assunto(varchar120,NN); descricao(varchar2000,NN); criada_em(tstz,NN). PK(id) FK(conta_id→contas,RESTRICT) idx(conta_id). NÃO snapshota nome/e-mail (PII resolvida live no envio). Apagada na anonimização LGPD (`ExcluirPorContaIdAsync`).

troca_email_tokens — confirmação de troca de e-mail (step-up + verificação do novo endereço; migration `AdicionarTrocaEmailToken`). id(uuid,NN); conta_id(uuid,NN,sem FK); novo_email(varchar256,NN); token_hash(varchar64,NN,SHA-256 hex; cru só no e-mail enviado ao NOVO endereço); expira_em(tstz,NN); usado_em(tstz,null); criado_em(NN). PK(id) UQ(token_hash) idx(conta_id).

### MFA / 2FA (segundo fator — feature MFA)
Tabelas sem FK física em `conta_id` (padrão das tabelas de token/credencial — §CONVENÇÕES). Apagadas na anonimização LGPD ([specification-lgpd]); desafios/dispositivos expirados purgados por GC horário (`LimparTokensRevogadosService`). Migration `AdicionarTabelasMfa`.

conta_mfa — estado do TOTP por conta. id(uuid,NN); conta_id(uuid,NN); totp_secret_cifrado(text,null,AES-256-GCM via `MfaSecretProtector` — nonce+tag+ct base64; chave `Mfa:EncryptionKey`); habilitado(bool,NN); ultimo_time_step(bigint,null,anti-replay TOTP — rejeita time-step ≤ último aceito); criado_em(NN); confirmado_em(tstz,null); atualizado_em(tstz,null). PK(id) UQ(conta_id).

mfa_recovery_codes — códigos de recuperação (10/lote, single-use). id(uuid,NN); conta_id(uuid,NN); codigo_hash(varchar64,NN,SHA-256 hex do código hex 10-char); usado_em(tstz,null,single-use); criado_em(NN). PK(id) idx(conta_id).

mfa_challenges — OTP de e-mail (login pendente / step-up). id(uuid,NN); conta_id(uuid,NN); codigo_hash(varchar64,NN,SHA-256 hex do OTP 6 dígitos); proposito(varchar32,NN,MfaProposito); expira_em(tstz,NN,+10min); usado_em(tstz,null); tentativas(int,NN,cap 5=`MaximoTentativas`→`Bloqueado`); criado_em(NN). PK(id) idx(conta_id,proposito).

trusted_devices — dispositivo confiável ("lembrar" pula 2º fator). id(uuid,NN); conta_id(uuid,NN); token_hash(varchar64,NN,SHA-256 hex; cru só no cookie httpOnly); expira_em(tstz,NN,+30d); criado_em(NN); ultimo_uso_em(tstz,null); rotulo(varchar256,null,user-agent=PII); revogado_em(tstz,null). PK(id) UQ(token_hash). Ativo = `!revogado_em && agora<expira_em`.

### Planos & Recebimento (treinador↔plataforma)
planos_plataforma — planos de assinatura do treinador. id(uuid,NN); nome(varchar,NN); max_alunos(int,NN); preco(numeric,NN); is_ativo(bool,NN); tier(varchar,NN,TierPlano); descricao(varchar,null); created_at(NN); updated_at(null). PK(id).

conta_recebimento — Stripe Connect do treinador. id(uuid,NN); treinador_id(uuid,NN); stripe_connect_account_id(varchar,null); onboarding_completo(bool,NN,default false); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,CASCADE) UQ(treinador_id).

### Treinadores, Pacotes, Vínculos
treinadores — perfil treinador. id(uuid,NN); conta_id(uuid,NN); nome(varchar,NN); plano_plataforma_id(uuid,null); plano_cortesia_id(uuid,null,tier efetivo de cortesia — quando setado, sobrepõe `plano_plataforma_id` no cálculo do tier/cap efetivo de alunos sem alterar a assinatura paga); alunos_acima_do_cap_desde(tstz,null,início da janela de graça — setado por `Treinador.MarcarAcimaDoCap` quando o treinador fica acima do cap do plano efetivo (downgrade ou perda de cortesia); null=dentro do cap ou graça encerrada via `LimparAcimaDoCap`); modo_pagamento_aluno(text,NN,default Plataforma,ModoPagamentoAluno); modo_pagamento_aluno_alterado_em(tstz,null); status(text,NN,TreinadorStatus); aprovado_por_id(uuid,null,sem FK); aprovado_em(tstz,null); telefone(varchar,null); anonimizado(bool,NN,default false,LGPD); created_at(NN); updated_at(null). PK(id) FK(conta_id→contas,RESTRICT) FK(plano_plataforma_id→planos_plataforma,RESTRICT) FK(plano_cortesia_id→planos_plataforma,RESTRICT) idx(plano_cortesia_id) UQ(conta_id). Migration `AdicionarCortesiaEGracaLimiteAlunos`: ambas colunas `AddColumn` nullable sem default — expand puro, sem backfill (linha pré-existente = "sem cortesia"/"dentro do cap", já o valor correto; não há CONTRACT pendente). (`modo_pagamento_aluno_alterado_em`: última troca de modo; null=nunca; cooldown 90d. Migration `AdicionarModoPagamentoAlteradoEm`.) Concurrency token = system column `xmin` (mapeado via EF, sem coluna física; migration `AdicionarConcurrencyTokenTreinador`): UPDATE concorrente do mesmo treinador aborta com DbUpdateConcurrencyException. **Dados fiscais (owned VO opcional, table-splitting na mesma tabela; migration `CriarNotasFiscaisEDadosFiscaisTreinador`)**: dados_fiscais_tipo_documento(text,null,TipoDocumentoFiscal); dados_fiscais_documento(varchar14,null,CPF/CNPJ só dígitos); dados_fiscais_razao_social(varchar150,null); dados_fiscais_inscricao_municipal(varchar30,null); dados_fiscais_endereco_logradouro(varchar200,null); dados_fiscais_endereco_numero(varchar20,null); dados_fiscais_endereco_complemento(varchar100,null); dados_fiscais_endereco_bairro(varchar100,null); dados_fiscais_endereco_codigo_municipio_ibge(varchar7,null); dados_fiscais_endereco_uf(varchar2,null); dados_fiscais_endereco_cep(varchar8,null). Todas null = treinador sem dados fiscais. SOBREVIVE à anonimização LGPD (guarda fiscal, retenção legal — `Anonimizar` não limpa).

pacotes — serviços oferecidos pelo treinador. id(uuid,NN); treinador_id(uuid,NN); nome(varchar,NN); preco(numeric,NN); is_ativo(bool,NN); descricao(varchar,null); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,RESTRICT).

vinculos_treinador_aluno — relação treinador↔aluno (aprovação+pacote). id(uuid,NN); treinador_id(uuid,NN); aluno_id(uuid,NN); pacote_id(uuid,null); status(text,NN,VinculoStatus); aprovado_por_id(uuid,null); aprovado_em(tstz,null); data_inicio(tstz,null); data_fim(tstz,null); preservar_no_limite(bool,NN,default false,marca o aluno escolhido pelo treinador p/ permanecer ativo durante a graça de limite — `Treinador.AlunosAcimaDoCapDesde`; os demais vínculos acima do cap são candidatos a inativação ao fim da janela); created_at(NN). PK(id) FK(treinador_id→treinadores,RESTRICT) FK(aluno_id→alunos,RESTRICT) FK(pacote_id→pacotes,RESTRICT). Migration `AdicionarCortesiaEGracaLimiteAlunos`: `AddColumn boolean NOT NULL DEFAULT false` — sem backfill (default cobre linhas pré-existentes; nenhum vínculo antigo estava sob graça).

logs_aprovacao — auditoria de aprovações/inativações. id(uuid,NN); tipo_acao(text,NN,TipoAcaoAprovacao); realizado_por_id(uuid,NN); entidade_id(uuid,NN); entidade_tipo(varchar,NN); observacao(varchar,null); created_at(NN). PK(id).

### Alunos
alunos — perfil aluno + anamnese. id(uuid,NN); conta_id(uuid,NN); nome(varchar,NN); email(varchar,null,VO); telefone(varchar,null); status(text,NN,AlunoStatus); dias_disponiveis(int,null); tempo_disponivel_minutos(int,null,TempoDisponivel); finalidade(varchar,null,FinalidadeTreino); foco_treino(varchar,null); nivel_condicionamento(varchar,null,NivelCondicionamento); limitacoes_fisicas(varchar,null); doencas(varchar,null); observacoes_adicionais(varchar,null); anonimizado(bool,NN,default false,LGPD); created_at(NN); updated_at(null). PK(id) FK(conta_id→contas,RESTRICT).

### Treinos & Exercícios
grupos_musculares — catálogo (seedado). id(uuid,NN); nome(varchar,NN); created_at(NN); updated_at(null). PK(id) UQ(nome).

exercicios — global (treinador_id null) ou do treinador. id(uuid,NN); treinador_id(uuid,null); grupo_muscular_id(uuid,NN); nome(varchar,NN); descricao(varchar,null); como_executar(varchar2000,null); video_id(varchar16,null,id YouTube 11ch); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,RESTRICT,null=global) FK(grupo_muscular_id→grupos_musculares,RESTRICT). Migração `ExercicioOrientacao` (aditiva, 2 colunas nullable).

treinos — ficha de treino. id(uuid,NN); treinador_id(uuid,NN); nome(varchar,NN); objetivo(text,NN,ObjetivoTreino); dificuldade(text,NN,default Iniciante,DificuldadeTreino); data_inicio(date,null); data_fim(date,null); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,RESTRICT).

treino_exercicios — exercícios na ficha (ordenados). id(uuid,NN); treino_id(uuid,NN); exercicio_id(uuid,NN); ordem(int,NN); observacao(varchar,null). PK(id) FK(treino_id→treinos,CASCADE) FK(exercicio_id→exercicios,RESTRICT).

treino_exercicio_series — séries configuráveis. id(uuid,NN); treino_exercicio_id(uuid,NN); quantidade(int,NN); repeticoes_min(int,NN); repeticoes_max(int,null); carga(numeric,null); descanso(int,null); ordem(int,NN); descricao(varchar,null). PK(id) FK(treino_exercicio_id→treino_exercicios,CASCADE).

treino_alunos — atribuição de ficha a aluno. id(uuid,NN); treino_id(uuid,NN); aluno_id(uuid,NN); status(text,NN,TreinoAlunoStatus); created_at(NN); updated_at(null). PK(id) FK(treino_id→treinos,RESTRICT) FK(aluno_id→alunos,RESTRICT) UQ(treino_id WHERE status='Ativo').

execucoes_treino — sessão executada pelo aluno. id(uuid,NN); treino_id(uuid,NN); aluno_id(uuid,NN); data_execucao(tstz,NN); observacao(varchar,null); idempotency_key(varchar64,null,GUID normalizado do cliente p/ dedup de reenvio offline/double-tap); created_at(NN). PK(id) FK(treino_id→treinos,RESTRICT) FK(aluno_id→alunos,RESTRICT) UQ(aluno_id,idempotency_key WHERE idempotency_key IS NOT NULL, `ix_execucoes_treino_aluno_id_idempotency_key_unique`; permite múltiplos NULL — legado/registro sem key). idx(treino_id); idx(aluno_id,data_execucao DESC, `ix_execucoes_treino_aluno_id_data_execucao`) — composto serve range/sort do histórico+dashboard+progressão (tabela de maior crescimento; o líder aluno_id cobre os lookups que o antigo single `ix_execucoes_treino_aluno_id` servia → single removido p/ cortar custo de escrita). Migrations `20260618162830_AdicionarIdempotencyKeyExecucao`, `20260626173942_AdicionarIndiceExecucaoAlunoData`.

execucoes_exercicio — detalhe por exercício da execução. id(uuid,NN); execucao_treino_id(uuid,NN); treino_exercicio_id(uuid,NN); series_executadas(int,NN); repeticoes_executadas(int,NN); carga_executada(numeric,null); observacao(varchar,null). PK(id) FK(execucao_treino_id→execucoes_treino,CASCADE) FK(treino_exercicio_id→treino_exercicios,RESTRICT).

### Assinaturas & Pagamentos (aluno↔treinador)
assinaturas_aluno — assinatura recorrente. id(uuid,NN); vinculo_id(uuid,NN); pacote_id(uuid,NN); treinador_id(uuid,NN); aluno_id(uuid,NN); valor(numeric,NN); status(text,NN,AssinaturaAlunoStatus); tentativas_falhas_consecutivas(int,NN,default 0); data_inicio(tstz,NN); data_proxima_cobranca(tstz,NN); data_cancelamento(tstz,null); created_at(NN); updated_at(null). PK(id) FK(vinculo_id→vinculos_treinador_aluno,RESTRICT) FK(pacote_id→pacotes,RESTRICT) FK(treinador_id→treinadores,RESTRICT) FK(aluno_id→alunos,RESTRICT) UQ(vinculo_id).

pagamentos — cobranças da assinatura. id(uuid,NN); assinatura_aluno_id(uuid,NN); valor(numeric,NN); status(text,NN,PagamentoStatus); metodo_pagamento(text,NN,default Pix,MetodoPagamento); stripe_payment_intent_id(varchar,null); client_secret(varchar,null); pix_qr_code(text,null); pix_qr_code_url(varchar,null); pix_expiracao(tstz,null); data_pagamento(tstz,null); created_at(NN); updated_at(null). PK(id) FK(assinatura_aluno_id→assinaturas_aluno,RESTRICT) UQ(stripe_payment_intent_id) UQ(assinatura_aluno_id WHERE status='Pendente'). Concurrency token = system column `xmin` (migration `AdicionarConcurrencyTokenPagamentos`; redelivery concorrente de webhook → 2ª perde a corrida, DbUpdateConcurrencyException). NOTA-ÍNDICE (remediação leak-linq 2026-06-24): os 3 reads por `assinatura_aluno_id` (display paginado `ListarPorAssinaturaAlunoPaginadoAsync` com `ORDER BY created_at DESC, id DESC`; cancel; export LGPD) varrem sem índice geral em `assinatura_aluno_id` (FK no PG NÃO cria índice; a UQ parcial só cobre `status='Pendente'`). Índice `(assinatura_aluno_id, created_at DESC, id DESC)` ajudaria sob crescimento — DEFERIDO: scan pré-existe a M4 (não introduzido pela paginação) e a cardinalidade por-assinatura é baixa (≈1 pagamento/mês). Reavaliar se a tabela crescer ou p95 do endpoint degradar.

assinaturas_treinador — assinatura recorrente do plano da plataforma (treinador→plataforma). id(uuid,NN); treinador_id(uuid,NN); plano_plataforma_id(uuid,NN); plano_plataforma_id_agendado(uuid,null,downgrade p/ próxima renovação); valor(numeric,NN); status(text,NN,AssinaturaTreinadorStatus); tentativas_falhas_consecutivas(int,NN,default 0); data_inicio(tstz,NN); data_proxima_cobranca(tstz,NN); data_cancelamento(tstz,null); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,RESTRICT) FK(plano_plataforma_id→planos_plataforma,RESTRICT) FK(plano_plataforma_id_agendado→planos_plataforma,RESTRICT) UQ-parcial(treinador_id WHERE status<>'Cancelada', `ux_assinaturas_treinador_nao_cancelada_por_treinador`, migration `AdicionarUniqueParcialAssinaturaTreinadorNaoCancelada` 20260629185519; AD-002; invariante ≤1 assinatura não-cancelada/treinador; auditoria 2026-06-29 fdpdbtiuuitndbeujcbj: 0 conflitos em homolog/develop/public).

pagamentos_treinador — cobranças do plano do treinador (PaymentIntent direto-plataforma, sem Connect). id(uuid,NN); treinador_id(uuid,NN); assinatura_treinador_id(uuid,NN); valor(numeric,NN); status(text,NN,PagamentoStatus); metodo_pagamento(text,NN,default Pix,MetodoPagamento); finalidade(text,NN,FinalidadePagamentoTreinador); plano_alvo_id(uuid,null,plano da troca); stripe_payment_intent_id(varchar,null); client_secret(varchar,null); pix_qr_code(text,null); pix_qr_code_url(varchar,null); pix_expiracao(tstz,null); data_pagamento(tstz,null); created_at(NN); updated_at(null). PK(id) FK(treinador_id→treinadores,RESTRICT) FK(assinatura_treinador_id→assinaturas_treinador,RESTRICT) UQ(stripe_payment_intent_id) UQ(assinatura_treinador_id WHERE status='Pendente'). Concurrency token = system column `xmin` (migration `AdicionarConcurrencyTokenPagamentos`).

reconciliacao_stripe_estado — cursor singleton (high-water-mark) da reconciliação Stripe. id(uuid,NN); ultimo_evento_reconciliado_utc(tstz,NN,maior `event.created` já processado); created_at(NN); updated_at(null). PK(id). 1 linha por schema; `AvancarCursor` é monotônico (nunca retrocede). Migration `CriarReconciliacaoStripeEstado`.

### Projeção / IA
assinantes — read model derivado de Aluno (sync via domain events). id(uuid,NN); aluno_id(uuid,NN); nome(varchar,NN); email(varchar,null); created_at(NN); updated_at(null). PK(id) UQ(aluno_id) [sem FK física].

ai_token_usage — consumo de tokens IA por user/agente/dia. NON-EF. id(uuid,NN); user_id(uuid,NN); agent_type(varchar,NN); date(date,NN); token_count(int,NN). PK(id) UQ(user_id,agent_type,date).

### Observabilidade / Saúde (relatório diário)
health_report_config — config runtime do relatório diário de saúde (1 linha por schema). id(uuid,NN); ativo(bool,NN); hora_envio_utc(time,NN); destinatarios(text,NN,csv emails normalizados); incluir_liveness(bool,NN); incluir_kpis(bool,NN); incluir_entregabilidade(bool,NN); incluir_erros(bool,NN); ultimo_envio_em(tstz,null); created_at(NN); updated_at(null). PK(id).

health_snapshots — snapshot diário da saúde do ambiente. id(uuid,NN); capturado_em(tstz,NN); ambiente(varchar100,NN); status_geral(text,NN,StatusSaude); payload_json(text,NN,JSON das seções); email_enviado(bool,null,resultado real do envio do e-mail do relatório — null=não rastreado/pré-migration; migration `AdicionarEmailEnviadoHealthSnapshot` 20260709155310, `AddColumn boolean NULL` — expand puro, sem backfill/contract); created_at(NN). PK(id) idx(capturado_em).

error_logs — log de ERROR/Critical (sink custom, best-effort) p/ a seção de erros. id(uuid,NN); ocorrido_em(tstz,NN); nivel(varchar20,NN); origem(varchar256,NN); mensagem(varchar4000,NN,truncada — PII mascarada na ESCRITA via `MascaraPii.Scrub`, [specification-lgpd]); created_at(NN). PK(id) idx(ocorrido_em). GC `LimparAntigosAsync` (`ExecuteDelete` em `ocorrido_em < agora-90d`, retenção LGPD) no GC horário `LimparTokensRevogadosService` — sem migration (reusa `ix_error_logs_ocorrido_em`).

### Notificações (feed in-app / engajamento)
notificacoes — feed in-app do usuário (novo treino, execução, nudges de aderência, digest). id(uuid,NN); destinatario_conta_id(uuid,NN); tipo(text,NN,TipoNotificacao); titulo(varchar120,NN); corpo(varchar500,NN); link_relativo(text,null); dia_referencia(date,null — set p/ tipos de scan, null p/ event-driven); lida(bool,NN,default false); created_at(NN); updated_at(null). PK(id) FK(destinatario_conta_id→contas,**CASCADE**) idx(`ix_notificacoes_conta_lida_created`=destinatario_conta_id,lida,created_at — contador/feed) UQ-parcial(`ix_notificacoes_dedup`=destinatario_conta_id,tipo,dia_referencia) WHERE dia_referencia IS NOT NULL (idempotência do scan `(conta,tipo,dia)` — 23505 tratado como no-op; NOTIF-10). Migration `AdicionarNotificacoes` (`lint-migrations:allow` — UQ sobre tabela criada na mesma migration, sem duplicatas pré-existentes).

### Outbox de efeitos externos
outbox_efeitos — fila durável de efeito externo pós-commit (entrega garantida + retry; gravada no MESMO commit do agregado de origem). id(uuid,NN); tipo(varchar200,NN, `evt:<CLR>`|`fx:<nome>`); payload(jsonb,NN); status(text,NN,OutboxStatus); tentativas(int,NN); proxima_tentativa(tstz,NN, scan do worker `<= agora`); ultimo_erro(text,null); chave_idempotencia(varchar300,NN); criado_em(tstz,NN); processado_em(tstz,null). PK(id) UQ(chave_idempotencia) idx(status,proxima_tentativa). [sem FK — desacoplado; payload carrega ids].

### Infra ASP.NET Core
data_protection_keys — keyring de ASP.NET Core DataProtection persistido (`IDataProtectionKeyContext`/`PersistKeysToDbContext<AppDbContext>`; migration `AdicionarDataProtectionKeys`). Substitui o repositório efêmero in-memory (chaves regeneravam a cada restart, antiforgery/`IDataProtector` invalidavam no deploy — issue #179). id(int,NN,IDENTITY — exceção à convenção uuid; schema fixo da lib); friendly_name(text,null); xml(text,null,XML da chave cifrado em repouso com AES-256-GCM via `AesGcmXmlEncryptor`, chave `DataProtection:EncryptionKey`). PK(id). Keyring compartilhado entre réplicas via `SetApplicationName("forzion.tech")`. Não-EF no sentido de domínio mas gerenciada por migration EF; conta como tabela BASE.

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
- **Expand/contract**: canônico em §MIGRATION-SAFETY acima (3 passos EXPAND→BACKFILL→CONTRACT em deploys separados); este §cobre só o passo BACKFILL (dado).
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
- Conexão RUNTIME do app (homolog/prod): **Session pooler Supabase** (host `aws-0-<região>.pooler.supabase.com`, porta **5432**, user `forzion_api.<ref>`) — IPv4 + pooling; suporta prepared statements + advisory lock → `MigrateAsync()`/seed no startup (`Program.cs`, Dev/Homolog) funcionam (DR-01, decidido 2026-06-11). NÃO usar **Transaction pooler (:6543)** na string do app: sem prepared stmt/session var/temp table → migration no boot quebra. **Guard de boot (fail-closed)**: `AddInfrastructure` RECUSA boot (throw) se a connection string usar `:6543` — bloqueia o erro de config antes de subir. Reforço runtime: `SchemaHealthCheck` (readiness) compara `current_schema()` vs Search Path esperado e reprova `Unhealthy` se divergir (search_path perdido = sinal de Transaction pooler) — ver [specification-observability §2]. Direct (`db.<ref>`, IPv6-only) = só ops ad-hoc/fallback. String EXATA: Dashboard → Connect → Session pooler; vive em `DB_CONNECTION` da VM (operacional, fora do repo).
- **Pool de conexões (PERF-08)**: canônico em [specification-performance §3]. Resumo db-specific: 1 scope DI = 1 `AppDbContext` = 1 conexão; o **Session pooler do Supabase** (porta 5432) multiplexa server-side e o teto efetivo é o limite de conexões do projeto Supabase, não o do Npgsql. String de conexão vive fora do repo (`DB_CONNECTION` da VM) → ajuste de pool é operacional, não versionado.
- **`CommandTimeout` + retry transitório no `UseNpgsql` (FR-1, 2026-06-26)**: `InfrastructureExtensions` configura `o.CommandTimeout(15)` (teto por comando, corta query presa em shared CPU) e `o.ExecutionStrategy(AppRetryingExecutionStrategy: maxRetryCount 3, maxRetryDelay 5s)` (retenta reset transitório do Session pooler). A estratégia EXCLUI `40001`/`40P01` do retry — serialização fica no loop app-level ([specification-concurrency §3]). `MaxPoolSize` NÃO inflado. Canônico em [specification-performance §3].
- Scripts do `dotnet ef`: gravados com BOM UTF-8 → remover (`sed '1s/^\xEF\xBB\xBF//'`) antes de aplicar via psql.
- Migration destrutiva/backfill roda no startup em Dev/Homolog contra o REMOTO. Validar antes; backfills afetam dados reais de homolog.
- Seed: 9 grupos, 90 exercícios globais, 5 planos, 1 admin (DataSeeder no startup, idempotente, ou cópia de homolog). Admin nasce email_verificado=true.
