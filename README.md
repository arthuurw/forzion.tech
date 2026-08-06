# forzion.tech

Plataforma de gestão de treinos para personal trainers e alunos.

**Backend**: ASP.NET Core 10 · **Frontend**: Next.js 16 + MUI v9 · **Banco**: PostgreSQL 17 (Supabase)

**Status**: ✅ 3.378 testes unit backend (0 falhas, sem Docker) + suíte de integração via Testcontainers (Postgres real, no CI) + suíte frontend (Vitest + Playwright + Storybook) | Clean Architecture | DDD tático (43 entidades, 35 domain events, 4 value objects) | Result<T> pattern (erros de negócio sem exceção) | Auth próprio: JWT HMAC + refresh token rotativo (famílias/detecção de reuso) + MFA TOTP (recovery codes, trusted devices, step-up) + verificação HIBP de senha | Isolamento por TreinadorId | Stripe Connect (Pix + cartão, refund/dispute, reconciliação por cursor, inadimplência) | Billing recorrente treinador↔plataforma | NFS-e Nacional (SEFIN gov.br: assinatura + comissão mensal, cancelamento, reconciliação) | Transactional Outbox | Notificações multicanal (e-mail Resend + WhatsApp Meta Cloud + in-app) com gate por tier | LGPD (exportação XLSX, anonimização, purga, consentimento) | Health report | Harness de testes completo (arch, property-based, mutation, snapshot, E2E real, Pact) | Segurança OWASP (DAST ZAP, SAST Semgrep, gitleaks, SBOM)

---

## Índice

- [Pré-requisitos](#pré-requisitos)
- [Estrutura do Repositório](#estrutura-do-repositório)
- [Setup Local](#setup-local)
- [Backend](#backend)
  - [Stack](#stack)
  - [Estrutura do Projeto](#estrutura-do-projeto-backend)
  - [Modelo de Domínio](#modelo-de-domínio)
  - [Domain Events](#domain-events)
  - [Autenticação e Autorização](#autenticação-e-autorização)
  - [MFA e Step-up](#mfa-e-step-up)
  - [Rate Limiting](#rate-limiting)
  - [Segurança](#segurança-backend)
  - [Notificações](#notificações)
  - [Outbox e Reconciliação](#outbox-e-reconciliação)
  - [Fiscal — NFS-e](#fiscal--nfs-e)
  - [Endpoints](#endpoints)
  - [Regras de Negócio](#regras-de-negócio)
  - [Tratamento de Erros](#tratamento-de-erros)
  - [Configuração e Secrets](#configuração-e-secrets)
  - [Stripe CLI (webhook local)](#stripe-cli-webhook-local)
  - [Migrations](#migrations)
  - [Testes e CI](#testes-e-ci)
- [Frontend](#frontend)
- [Deploy](#deploy)

---

## Pré-requisitos

| Ferramenta | Versão mínima |
|-----------|--------------|
| .NET SDK | 10.0.100 (pinado em `global.json`, `rollForward: latestFeature`) |
| Node.js | 22 |
| Docker + Docker Compose plugin | 24+ |
| EF Core CLI | `dotnet tool install -g dotnet-ef` |
| PostgreSQL | 17 (via Supabase ou local) |

---

## Estrutura do Repositório

```
forzion.tech/
├── forzion.tech.Api/           # HTTP — Minimal APIs, middleware, filtros, DI, hosted services
├── forzion.tech.Application/   # Use cases, handlers, interfaces, Result<T>, validators, settings
├── forzion.tech.Domain/        # Entidades, Value Objects, Events, Enums, Shared/Errors
├── forzion.tech.Infrastructure/# EF Core, repositórios, migrations, integrações, outbox, notificações
├── forzion.tech.Tests/         # xUnit + Moq + FluentAssertions + Testcontainers + Verify + CsCheck
├── forzion.tech.PactVerification/ # Pact provider verification (entra no build da .slnx)
├── frontend/                   # Next.js 16 — ver frontend/README.md
├── nginx/                      # nginx.conf da borda única (HTTPS + proxy homolog/prod)
├── infra/                      # Config de infraestrutura versionada (ex.: fail2ban jail p/ auth nginx)
├── scripts/                    # setup-vm.sh, init-ssl.sh, reload-edge.sh, gen-openapi.sh, lint-migrations.sh,
│                               # migrate-dryrun.sh, check-coverage.sh, setup-firewall.sh, perf/, systemd/
├── presentation/               # Deck/pitch do projeto (PRESENTATION.md + presentation.html)
├── docker-compose.yml          # Stack local (Postgres local + backend + frontend)
├── docker-compose.homolog.yml  # Stack de homologação (build-on-VM; deploy ativo)
├── docker-compose.server.yml   # Stack por imagem de registry (GHCR — usado no fluxo de produção)
├── docker-compose.edge.yml     # Borda única (nginx + certbot) — serve homolog + prod
├── docker-compose.dryrun.yml   # Migrate dry-run contra cópia do schema (gate de deploy)
├── .github/workflows/          # CI/CD + crons de billing/NFS-e/LGPD + segurança + backup
├── AGENTS.md                   # Guia macro para agentes (referenciado por CLAUDE.md)
├── .env.example                # Variáveis do docker-compose
├── global.json                 # SDK .NET pinado (floor 10.0.100)
├── forzion.tech.slnx           # Solution (formato .slnx)
├── specs/                      # 24 docs de referência agent-oriented `specification-*.md` (versionados):
│                               # model, backend, db, coding, design-review, concurrency, performance,
│                               # load-testing, tests, local-ci-repro, git, workflow, security, observability,
│                               # stripe, fiscal, email, whatsapp, lgpd, dr, infrastructure, frontend,
│                               # frontend-ui, seo
└── docs/                       # Documentação (gitignored, exceto docs/api/)
    └── api/openapi.v1.json     # Contrato OpenAPI versionado (baseline do gate openapi-drift)
```

> `docs/` (planos, notas de design gerados por agente) é ignorado pelo git via `.gitignore`, **exceto** `docs/api/` — o `openapi.v1.json` ali é o baseline do check de drift de contrato no CI.

---

## Setup Local

### Opção A — Docker Compose (mais rápido)

```bash
# 1. Copiar e preencher variáveis
cp .env.example .env
# editar .env: JWT_SECRET, MFA_ENCRYPTION_KEY, DATA_PROTECTION_KEY, STRIPE_*, SEED_*

# 2. Subir tudo (backend + frontend + postgres local)
docker compose up --build

# Backend:  http://localhost:8080
# Frontend: http://localhost:3001
# Scalar:   http://localhost:8080/scalar
```

### Opção B — Manual

```bash
# 1. Backend — configurar secrets (ver seção Configuração e Secrets)
dotnet user-secrets set "Auth:JwtSecret"   "<chave-hmac-min-32bytes>"   --project forzion.tech.Api
dotnet user-secrets set "Mfa:EncryptionKey"            "<base64-32bytes>" --project forzion.tech.Api
dotnet user-secrets set "DataProtection:EncryptionKey" "<base64-32bytes>" --project forzion.tech.Api
dotnet user-secrets set "ConnectionStrings:AppConnection" "<conn-pg>"     --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminEmail"    "admin@forzion.tech"         --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminPassword" "<senha>"                    --project forzion.tech.Api

# 2. Aplicar migrations (schema homolog)
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef database update \
  --project forzion.tech.Infrastructure \
  --startup-project forzion.tech.Api

# 3. Rodar API (HTTP :5230 | HTTPS :7220)
dotnet run --project forzion.tech.Api

# 4. Frontend (outro terminal)
cd frontend
npm install
npm run dev   # http://localhost:3000
```

> Em Development, o backend roda `MigrateAsync` + `SeedAsync` no boot. O `DataSeeder` cria a conta admin (`Seed:AdminEmail`/`Seed:AdminPassword`) e popula os planos padrão da plataforma de forma idempotente. Em Homolog/Production o migrate é one-shot (fora do web host), não no boot.

---

## Backend

### Stack

| Camada | Tecnologia |
|--------|------------|
| Framework | ASP.NET Core 10 / C# (LangVersion `latest`) / Minimal APIs |
| Banco de dados | PostgreSQL 17 (Supabase) |
| ORM | Entity Framework Core 10 + Npgsql 10 + EFCore.NamingConventions (snake_case) |
| Auth | JWT HMAC-SHA256 + refresh token rotativo + MFA TOTP (Otp.NET) + BCrypt (sem Supabase Auth) |
| Validação | FluentValidation |
| Pagamentos | Stripe.net (Connect + PaymentIntents/Pix) |
| E-mail | Resend (REST) + Svix (webhook) |
| Exportação | ClosedXML (LGPD XLSX) |
| Testes | xUnit + Moq + FluentAssertions + Testcontainers + Verify.Xunit + CsCheck + NetArchTest |
| Documentação | OpenAPI nativo (`Microsoft.AspNetCore.OpenApi`) + UI Scalar (`/scalar`) — só Development |

### Comandos

```bash
# Executar a API (HTTP :5230 | HTTPS :7220)
dotnet run --project forzion.tech.Api

# Executar em ambiente específico
ASPNETCORE_ENVIRONMENT=Homolog dotnet run --project forzion.tech.Api

# Build completo (inclui forzion.tech.PactVerification)
dotnet build forzion.tech.slnx

# Todos os testes
dotnet test forzion.tech.Tests

# Só unit (rápido, sem Docker)
dotnet test forzion.tech.Tests --filter "Category!=Integration"

# Teste único
dotnet test forzion.tech.Tests --filter "FullyQualifiedName~NomeDoTeste"

# Testes com cobertura
dotnet test forzion.tech.Tests --settings forzion.tech.Tests/coverage.runsettings

# Formatar antes de git add em .cs novos
dotnet format forzion.tech.slnx

# Gerar migration (SEMPRE com Homolog)
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef migrations add <Nome> \
  --project forzion.tech.Infrastructure \
  --startup-project forzion.tech.Api

# Aplicar migration em homolog
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef database update \
  --project forzion.tech.Infrastructure \
  --startup-project forzion.tech.Api

# Gerar script SQL idempotente (offline, schema-agnostic)
dotnet ef migrations script --idempotent \
  --project forzion.tech.Infrastructure \
  --startup-project forzion.tech.Api \
  --output migration.sql
```

> **Migrations são schema-agnostic** (`AppDbContext` sem `HasDefaultSchema`; schema vem do `search_path` da connection — ex.: `Search Path=homolog`). As MESMAS migrations aplicam em `homolog`/`develop`/`public`. Detalhes em [`specs/specification-db.md`](specs/specification-db.md).
>
> **Atenção**: `InicioDominio.Up()` contém `DROP TABLE` — nunca reaplicar em ambiente com dados.

---

### Estrutura do Projeto (Backend)

```
forzion.tech.Api/
├── Configuration/        # JWT, DataProtection, MFA protection, OpenAPI/Scalar, autenticação
├── Context/              # HttpUserContext — extrai claims do JWT
├── Endpoints/            # Minimal API por grupo de recurso
│   ├── Admin/            # /admin — SystemAdmin
│   ├── Auth/             # /auth, /auth/mfa, /auth/step-up — público / escopo restrito
│   ├── Conta/            # /conta, /conta/mfa — perfil, senha, e-mail, MFA, LGPD
│   ├── AlunoArea/        # /aluno — Aluno
│   ├── Alunos/           # /alunos — JWT
│   ├── Exercicios/       # /exercicios — JWT (legado; subconjunto de /treinador/exercicios)
│   ├── Notificacoes/     # /notificacoes — in-app
│   ├── Pagamentos/       # /aluno/pagamentos, /treinador/pagamentos, /internal, /webhooks
│   ├── Suporte/          # /suporte/mensagens
│   ├── Treinador/        # /treinador, /treinador/plano, /treinador/dados-fiscais
│   └── Treinos/          # /treinos
├── Extensions/           # RouteBuilder, DI, rate-limit partitions, InternalApiKeyValidator, ResultExtensions
├── Filters/              # PerfilIdRequiredFilter, PaginacaoFilter, RequerStepUpFilter,
│                         # RequireAssinaturaAtivaFilter, RequireAssinaturaTreinadorAtivaFilter
├── Middleware/           # GlobalExceptionHandler (RFC 7807)
├── Startup/              # MigrationStartup (migrate one-shot / auto-migrate no boot em Dev)
└── Services/             # Hosted services: LimparTokensRevogados, OutboxProcessor, OutboxLimpeza,
                          # RelatorioSaudeDiario, ErrorLogDbSinkDreno

forzion.tech.Application/
├── Auth/                 # MfaScopes (mfa_pending / step_up)
├── Interfaces/           # IUserContext, IJwtService, IRefreshTokenService, IPasswordHasher,
│                         # IUnitOfWork, IDomainEventDispatcher, IOutboxEnfileirador,
│                         # IPlanoNotificationPolicy, Repositories/ (interface por entidade)
├── Settings/             # AppSettings, EmailSettings, WhatsAppSettings, InternalSettings, DeliveryLogSettings
├── Services/             # LimiteTreinadorService (valida MaxAlunos ao aprovar vínculo)
└── UseCases/             # Handler CQRS-like por domínio: Admin, Auth (Login, RenovarSessao, Mfa, StepUp),
                          # Conta (Mfa, Logout, AlterarSenha, TrocaEmail), Alunos, AssinaturaAlunos,
                          # Exercicios, Pacotes, Pagamentos (inclui ReconciliarPagamentosStripe),
                          # Planos, Treinadores, Treinos, Vinculos, Nfse, Notificacoes, Suporte

forzion.tech.Domain/
├── Entities/             # 43 entidades (ver Modelo de Domínio)
├── Shared/               # Result, Result<T>, Error, ErrorType + Errors/*Errors.cs (33 agregados de erro tipado)
├── Enums/                # 32 enums (ver Domain Events)
├── Events/               # IDomainEvent, IHasDomainEvents + 35 eventos concretos
└── ValueObjects/         # Email, DadosFiscais, EnderecoFiscal, YouTubeVideoId

forzion.tech.Infrastructure/
├── DependencyInjection/  # InfrastructureExtensions (gate real/null das integrações)
├── Migrations/           # 55 EF Core migrations (schema-agnostic)
├── Handlers/             # Handlers de domain events em Infra (cria AssinaturaAluno, sincroniza Assinante, etc.)
├── Notifications/
│   ├── Email/            # ResendEmailService gate + EnvironmentEmailDecorator + EmailTemplates + ~30 handlers
│   ├── WhatsApp/         # MetaWhatsAppCloudNotifier + NullWhatsAppNotifier + EnvironmentWhatsAppDecorator
│   │                     # + PhoneNumberNormalizer (E.164) + WhatsAppTemplates + 17 handlers (paridade e-mail)
│   ├── InApp/            # Handlers da entidade Notificacao
│   └── PlanoNotificationPolicy.cs  # Gate de canais por tier (e-mail ≥Pro, WhatsApp ≥ProPlus)
├── Outbox/               # OutboxDispatcher, OutboxEnfileirador, handlers fx:* (NFS-e emitir/cancelar)
├── Persistence/
│   ├── AppDbContext.cs   # DbContext + IUnitOfWork (despacha eventos e outbox no CommitAsync)
│   ├── Configurations/   # Fluent API por entidade (43 arquivos)
│   ├── Repositories/     # Implementações concretas
│   └── Seeders/          # DataSeeder — conta admin + planos no startup (Dev)
├── Logging/              # ErrorLogDbSinkProvider (dreno de logs de erro p/ error_logs)
└── Services/
    ├── JwtService.cs / RefreshTokenService.cs / SessaoConfig.cs
    ├── BcryptPasswordHasher.cs / PwnedPasswordsService.cs (HIBP)
    ├── MfaSecretProtector.cs / OtpNetTotpService.cs / RecoveryCodeGenerator.cs
    ├── ResendEmailService.cs / NullEmailService.cs
    ├── StripeService.cs (Connect, Pix/Cartão PaymentIntent, webhook, reconciliação)
    ├── EmissorNfseNacionalService.cs / NullEmissorNfseService.cs (NFS-e SEFIN)
    ├── OutboxProcessor.cs
    └── DomainEventDispatcher.cs

forzion.tech.Tests/
├── Api/                  # Endpoints via WebApplicationFactory + Snapshots (Verify.Xunit)
├── Application/          # Handlers (unit) por domínio
├── Domain/               # Entidades, value objects; Domain/Properties/ (property-based CsCheck)
├── Architecture/         # Arch tests (NetArchTest): direção de dependência, convenções
├── Builders/             # Test data builders determinísticos
├── Infrastructure/       # JwtService, MFA, email/whatsapp handlers, dispatch (re-entrância),
│                         # Repositories/ [Integration] (Testcontainers.PostgreSql)
└── E2E/                  # Pipeline real: WebApplicationFactory + Postgres real + migrations + seed [Integration]
```

---

### Modelo de Domínio

43 entidades. Padrão DDD: factory `Criar`, máquinas de estado com transições guardadas por `Result`, domain events despachados no `CommitAsync`.

| Entidade | Descrição |
|----------|-----------|
| `Conta` | Raiz de auth. `Email` VO + `PasswordHash` (BCrypt). `TipoConta`: `SystemAdmin`/`Treinador`/`Aluno`. `EmailVerificado`/`VerificadoEm`. `SessoesInvalidasAntesDeUtc` (epoch — access tokens com `nbf` anterior são rejeitados). Emite `ContaRegistradaEvent`; `Anonimizar()` (LGPD). |
| `SystemUser` | Perfil admin de uma `Conta` `SystemAdmin`. `Role` (`SystemRole`: SuperAdmin/Support/Operator), `Status`. |
| `Treinador` | Perfil de treinador. Status: `AguardandoPagamento → AguardandoAprovacao → Ativo/Inativo`. Embute VO **`DadosFiscais?`** (CNPJ/CPF + endereço, para NFS-e). `PlanoPlataformaId`, `PlanoCortesiaId?` (plano de cortesia do admin), `AlunosAcimaDoCapDesde?` (carimbo da janela de graça), `ModoPagamentoAluno` (`Plataforma`/`Externo`, cooldown 90d). Stripe Connect vive em `ContaRecebimento`. |
| `Aluno` | Perfil de aluno. `Email` VO, `Telefone`, campos de anamnese (consentimento LGPD). Máquina: `AguardandoAprovacao → Ativo ⇌ Inativo`. `Anonimizar()`. |
| `VinculoTreinadorAluno` | Relação treinador↔aluno. `PacoteId`, `PreservarNoLimite` (protege da apara automática, dentro do cap). Status: `AguardandoAprovacao → Ativo → Inativo`. Emite `VinculoPendenteCriado`/`VinculoAprovado`. |
| `PlanoPlataforma` | Plano global (admin). `Tier` (`TierPlano`: Free/Basic/Pro/ProPlus/Elite), `MaxAlunos`, `Preco`, `Descricao?`, `IsAtivo`. |
| `Pacote` | Pacote do treinador (nome, descrição, `Preco`, `IsAtivo`). Sem limite de fichas. |
| `Treino` | Ficha (nome, objetivo, dificuldade, datas) + lista de `TreinoExercicio`. `Duplicar`/`DuplicarPara`; edição bloqueada se já executada. |
| `TreinoExercicio` | Slot de exercício na ficha: `Ordem`, lista de `SerieConfig`. Referencia `Exercicio` por ID (sem nav prop). |
| `SerieConfig` | Série de um `TreinoExercicio` (qtd, reps mín/máx, carga, descanso, ordem). Filho owned. |
| `TreinoAluno` | Atribuição ficha × aluno. Status: `Ativo/Inativo`. Emite `TreinoDisponibilizadoEvent`. |
| `Exercicio` | Global (`TreinadorId = null`) ou privado. FK `GrupoMuscularId`. Extrai `VideoId` via VO `YouTubeVideoId`; `ComoExecutar`. |
| `GrupoMuscular` | Catálogo global (admin), fonte da verdade referenciada por FK em `Exercicio`. |
| `ExecucaoTreino` | Sessão realizada pelo aluno + lista de `ExecucaoExercicio`. `IdempotencyKey` opcional (dedupe). Emite `ExecucaoRegistradaEvent`. |
| `ExecucaoExercicio` | Item filho de `ExecucaoTreino` (séries/reps/carga executadas). |
| `LogAprovacao` | Auditoria (`TipoAcaoAprovacao`). `EntidadeId` sem FK — sobrevive a hard deletes. Imutável. |
| `ContaRecebimento` | Conta Stripe Connect do treinador (`StripeConnectAccountId`, `OnboardingCompleto`). Contexto Billing. |
| `AssinaturaAluno` | Cobrança recorrente mensal do aluno (`VinculoId`/`PacoteId`). Status: `Pendente → Ativa ⇌ Inadimplente → Cancelada`. Limite de 3 falhas consecutivas → `Inadimplente`; chargeback pula direto. |
| `AssinaturaTreinador` | Assinatura treinador→plataforma (`PlanoPlataforma`). Mesma máquina de `AssinaturaAluno`. `DataProximaCobranca`, `PlanoPlataformaIdAgendado` (troca agendada); downgrade p/ Free encerra. |
| `Pagamento` | Cobrança de `AssinaturaAluno`. `MetodoPagamento`. Status: `Pendente → Pago/Falhou/Expirado`, `Pago → Estornado/EmDisputa`. Armazena `StripePaymentIntentId`, QR Pix ou `ClientSecret`; limpa dados sensíveis em transição terminal. |
| `PagamentoTreinador` | Cobrança treinador→plataforma. `Finalidade` (`Cadastro`/`Renovacao`/`TrocaPlano`/`Contratacao`). Mesma máquina de `Pagamento`. `MarcarPago` emite `PagamentoTreinadorPagoEvent`. |
| `Assinante` | Projeção read-side de billing do aluno (nome/email). Sincronizada por eventos. Unique por `AlunoId`. |
| `ContaMfa` | Config MFA TOTP por conta (1:1). `TotpSecretCifrado` (AES-256-GCM, **cifrado** não hasheado), `Habilitado`, `UltimoTimeStep` (anti-replay). |
| `MfaChallenge` | Desafio MFA de uso único (OTP e-mail). `CodigoHash`, `Proposito` (`LoginFallback`/`StepUp`), lockout em 5 tentativas, expiração. |
| `MfaRecoveryCode` | Backup code MFA. `CodigoHash`, single-use, sem expiração. |
| `TrustedDevice` | Cookie "lembrar dispositivo" p/ pular MFA. `TokenHash`, 30 dias, `RevogadoEm`/`UltimoUsoEm`. |
| `RefreshToken` | Refresh token rotativo de uso único de uma `RefreshTokenFamily`. `TokenHash` (SHA-256). Rotação encadeada; reuso = sinal de roubo. |
| `RefreshTokenFamily` | Sessão/dispositivo — agrupa a cadeia de `RefreshToken`. `AbsolutoExpiraEm` (teto), `RevogadaEm`/`MotivoRevogacaoFamilia`, `Rotulo`. |
| `TokenRevogado` | Blocklist de JWT (jti + `ExpiraEm`). Checado em todo request. Limpo ao expirar. |
| `EmailVerificationToken` | Verificação de e-mail. `TokenHash` (SHA-256), expiração 24h, single-use. |
| `PasswordResetToken` | Reset de senha. `TokenHash`, expiração 1h, single-use. |
| `TrocaEmailToken` | Troca de e-mail. `NovoEmail` em claro (alvo) + `TokenHash`, single-use. |
| `RedefinicaoSenhaSegundoFator` | Lockout do 2º fator no reset de senha (janela 15min, 5 tentativas). |
| `MensagemSuporte` | Ticket de suporte (`Categoria`, `Assunto`, `Descricao`). Emite `MensagemSuporteCriadaEvent`. Imutável. |
| `Notificacao` | Notificação in-app (`Tipo`, `Titulo`, `Corpo`, `LinkRelativo?`, `DiaReferencia?` p/ dedupe, `Lida`). |
| `OutboxEfeito` | Transactional outbox. `Tipo` (`evt:<CLR>` re-dispatch durável / `fx:<nome>` efeito nomeado), `ChaveIdempotencia` única, Status `Pendente → Processando → Concluido/Falhou`, retry com backoff. |
| `ReconciliacaoStripeEstado` | Cursor singleton (`UltimoEventoReconciliadoUtc`, avanço monotônico). |
| `EmailDeliveryLog` | Auditoria de entrega de e-mail (webhook Resend/Svix). `RecipientEmailHash` (**hasheado**, LGPD). |
| `WhatsAppDeliveryLog` | Auditoria de entrega WhatsApp (webhook Meta). `RecipientPhoneHash` (**hasheado**). |
| `HealthReportConfig` | Config (singleton) do relatório de saúde: destinatários, flags `Incluir*`. |
| `HealthSnapshot` | Snapshot histórico de saúde (`StatusSaude`, `PayloadJson`). Imutável. |
| `ErrorLogEntry` | Log de erro estruturado (`Nivel`/`Origem`/`Mensagem`). Sem FK. |

**Value Objects**: `Email` (normaliza + valida, regex ReDoS-safe), `DadosFiscais` (CPF/CNPJ com dígito verificador + `EnderecoFiscal`), `EnderecoFiscal` (UF/IBGE/CEP validados), `YouTubeVideoId` (extrai ID de URL).

---

### Domain Events

33 eventos concretos (a tabela abaixo agrupa famílias correlatas). `Conta`, `Treinador`, `Vinculo`, `Aluno`, `Treino`/`TreinoAluno`, `ExecucaoTreino`, `AssinaturaAluno`/`Pagamento`, `AssinaturaTreinador`/`PagamentoTreinador`, `MensagemSuporte` implementam `IHasDomainEvents`. Despacho via `IDomainEventDispatcher` (interface genérica tipada, sem reflection). Eventos best-effort são consumidos em paralelo por e-mail + WhatsApp + in-app (gate por tier via `PlanoNotificationPolicy`); eventos que **não podem ser perdidos** (criação de assinatura, e-mail crítico, submissão de evidência de disputa Stripe) passam pelo **outbox** (durável).

| Evento | Levantado em | Consumido por |
|--------|-------------|---------------|
| `ContaRegistradaEvent` | `Conta.Criar()` | e-mail de verificação |
| `ContaAnonimizadaEvent` | `Conta.Anonimizar()` | — |
| `EmailCriticoSolicitadoEvent` | `EmailCriticoDispatcher` (MFA/reset/troca-email) | e-mail crítico (payload cifrado, durável) |
| `TreinadorAprovadoEvent` / `TreinadorReprovadoEvent` / `TreinadorInativadoEvent` | `Treinador.Aprovar/Reprovar/Inativar()` | e-mail + WhatsApp |
| `VinculoPendenteCriadoEvent` | `Vinculo.Criar()` | e-mail + WhatsApp ao treinador |
| `VinculoAprovadoEvent` | `Vinculo.Aprovar()` | e-mail + WhatsApp + **cria `AssinaturaAluno`** |
| `AlunoRegistradoEvent` | `Aluno.Criar()` | e-mail + WhatsApp + sincroniza `Assinante` |
| `AlunoAtualizadoEvent` | `Aluno.Atualizar()` | sincroniza `Assinante` |
| `AlunoInativadoEvent` | `Aluno.Inativar()` | e-mail + WhatsApp |
| `TreinoDisponibilizadoEvent` | `TreinoAluno.Criar()` | in-app + e-mail + WhatsApp (engajamento) |
| `ExecucaoRegistradaEvent` | `ExecucaoTreino.Criar()` | in-app ao treinador |
| `AssinaturaAlunoCriadaEvent` / `...Cancelada` / `...Reativada` / `...MarcadaInadimplente` | `AssinaturaAluno.*` | e-mail + WhatsApp (cancelamento: aluno **e** treinador) |
| `PagamentoCriadoEvent` | `Pagamento.Criar()` | e-mail + WhatsApp ao aluno |
| `PagamentoFalhouEvent` | `AssinaturaAluno.RegistrarPagamentoFalho()` | e-mail + WhatsApp |
| `PagamentoEstornadoEvent` | `Pagamento.MarcarEstornado()` | e-mail + WhatsApp |
| `PagamentoEmDisputaEvent` | `Pagamento.MarcarEmDisputa()` | e-mail + WhatsApp + alerta ao treinador |
| `AssinaturaTreinadorCriada/Cancelada/Reativada/PlanoTrocado` | `AssinaturaTreinador.*` | — (sem consumidor) |
| `AssinaturaTreinadorMarcadaInadimplenteEvent` / `...PagamentoFalhouEvent` | `AssinaturaTreinador.*` | e-mail ao treinador |
| `CobrancaProximaAlunoEvent` / `CobrancaProximaTreinadorEvent` | job de pré-aviso (3d antes) | e-mail |
| `PagamentoTreinadorPagoEvent` | `PagamentoTreinador.MarcarPago()` | ativa/atualiza assinatura |
| `PagamentoTreinadorEstornadoEvent` / `...EmDisputaEvent` | `PagamentoTreinador.*` | — (sem consumidor) |
| `MensagemSuporteCriadaEvent` | `MensagemSuporte.Criar()` | e-mail ao suporte (durável) |

Eventos são despachados **após** `SaveChangesAsync`; a entidade tem os eventos limpos (snapshot + `ClearDomainEvents`) antes do dispatch, evitando re-entrância (handler que chama `CommitAsync` não re-despacha). Handlers in-process compartilham o `AppDbContext` do escopo.

---

### Autenticação e Autorização

Dois formatos de token, ambos HMAC-SHA256 (`MapInboundClaims = false`, `ClockSkew = Zero`):
**access token** (sessão) e **scope token** (`mfa_pending` / `step_up`, sem `tipo_conta`/`perfil_id`).

| Claim | Conteúdo |
|-------|----------|
| `sub` / `conta_id` | ID da `Conta` (`conta_id` legado; `sub` também particiona rate-limit) |
| `tipo_conta` | `SystemAdmin`/`Treinador`/`Aluno` (só access) |
| `perfil_id` | ID do perfil (só access) |
| `nome` | Nome do perfil (só access) |
| `jti` | UUID por token — checado contra `tokens_revogados` em todo request |
| `fam` | ID da `RefreshTokenFamily` (sessão/device), quando presente |
| `scope` | `mfa_pending` ou `step_up` (só scope token) |
| `nbf` / `exp` | `nbf` comparado contra `Conta.SessoesInvalidasAntesDeUtc` |

Políticas (`RequireAuthorization`):

| Política | Exigência |
|----------|-----------|
| *(default)* | JWT autenticado **e** que NÃO carregue claim `scope` (fecha rotas de negócio a scope tokens) |
| `SystemAdmin` / `Treinador` / `Aluno` | `tipo_conta` correspondente |
| `MfaPendente` | `scope == "mfa_pending"` — usada em `/auth/mfa/*` |
| `MfaStepUp` | `scope == "step_up"` — checada pelo `RequerStepUpFilter` (header `X-Step-Up-Token`) |

**Sessão e refresh**: access token curto (`SystemAdmin` 10min, demais 15min por default; configurável em `Auth:Sessao:{Tipo}`). O refresh (32 bytes, SHA-256 no DB) vive numa `RefreshTokenFamily` por dispositivo, com janelas idle + absoluta por papel. `RefreshTokenService.RotacionarAtomicoAsync` rotaciona atomicamente; reuso de token já usado → revoga a família inteira (`ReuseDetectado`).

**Revogação**: `OnTokenValidated` consulta `tokens_revogados` (jti) e o epoch `SessoesInvalidasAntesDeUtc`. Troca de senha e desabilitar MFA revogam todas as famílias da conta; logout revoga só a família + jti correntes. `LimparTokensRevogadosService` purga expirados a cada hora (roda no boot).

**Filtros**: `PerfilIdRequiredFilter` (403 se `perfil_id` ausente), `PaginacaoFilter` (400 se `pagina < 1` ou `tamanhoPagina ∉ [1,100]`), `RequireAssinaturaAtivaFilter` / `RequireAssinaturaTreinadorAtivaFilter` (gate de assinatura em ações que exigem plano ativo).

---

### MFA e Step-up

MFA opcional por conta, com step-up para ações sensíveis.

- **Enroll TOTP**: `POST /conta/mfa/totp/iniciar` gera segredo (Otp.NET), cifra (AES-256-GCM, chave `Mfa:EncryptionKey`) e persiste `ContaMfa` `Habilitado=false`. `POST /conta/mfa/totp/confirmar` valida o código, habilita e gera **10 recovery codes**.
- **Recovery codes**: 16 hex chars, hash SHA-256, comparação em tempo constante iterando todos (anti-timing). Regeneráveis (step-up obrigatório).
- **Trusted devices**: cookie `trusted_device` (hash SHA-256, 30 dias) pula o 2º fator no próximo login.
- **Login com MFA**: `POST /auth/login` → se MFA habilitado, retorna scope token `mfa_pending`; `POST /auth/mfa/verificar` (TOTP/recovery, ou `/auth/mfa/email/enviar` p/ OTP por e-mail) completa e emite a sessão.
- **Step-up**: `POST /auth/step-up/iniciar` (TOTP se habilitado, senão OTP e-mail) → `POST /auth/step-up/verificar` emite scope token `step_up` de 5min. Endpoints com `RequerStepUpFilter` (desabilitar MFA, regenerar recovery, trocar senha, trocar e-mail, onboarding Stripe, aprovar/reprovar/inativar treinador) exigem o header `X-Step-Up-Token`.
- **Lockout**: `MfaChallenge` bloqueia após 5 tentativas; TOTP tem anti-replay via `UltimoTimeStep`.

---

### Rate Limiting

Fixed Window, `QueueLimit=0` (rejeita imediatamente com **429**). Em ambiente `Test` viram no-op.

| Política | Limite | Janela | Partição | Aplicado em |
|----------|--------|--------|----------|-------------|
| `auth` | 10 req | 1 min | IP | `/auth/login`, `/auth/refresh`, `/auth/register/*`, `/auth/forgot-password`, `/auth/reset-password`, `/auth/verify-email`, `/auth/resend-verification`, `/auth/planos`, `/auth/treinadores*`, `/conta/mfa/*` |
| `mfa` | 5 req | 1 min | IP ou `sub` | `/auth/mfa/*`, `/auth/step-up/*` |
| `write` | 60 req | 1 min | IP ou `sub` | mutações autenticadas (`/alunos`, `/treinos`, `/treinador/*`, `/aluno/*`, `/conta/*`, `/admin/*`, `/suporte/*`) |
| `read` | 120 req | 1 min | IP ou `sub` | leituras + `/health`, `/health/ready`, `/aluno/pagamentos/*`, dashboards |
| `internal` | 5 req | 1 min | IP | `/internal/*` |
| `webhook` | 300 req | 1 min | IP | `/webhooks/*` |

Rejeições em `auth`/`mfa` são logadas (`RateLimit.AuthAbuse`).

---

### Segurança (Backend)

| Mecanismo | Detalhe |
|-----------|---------|
| Senhas | BCrypt (salt automático) + verificação contra HIBP (`IPwnedPasswordsService`, k-anonymity) |
| Anti-enumeração | Login inexistente verifica hash BCrypt dummy fixo (tempo constante) |
| Tokens | HMAC-SHA256; revogação por jti + epoch de sessão; refresh rotativo com detecção de reuso |
| JWT secret | Mínimo 32 bytes, validado no startup — falha explícita se fraco |
| DataProtection | Chaves persistidas no DB (`data_protection_keys`) e cifradas com AES-256-GCM próprio (`DataProtection:EncryptionKey`) — sem DPAPI/cert local |
| MFA secret | Cifrado com AES-256-GCM (`Mfa:EncryptionKey`, chave independente) |
| Cookies (BFF Next.js) | `token`/`refresh`/`session_guard`/`mfa_pending`/`trusted_device` httpOnly, `SameSite=Strict`, `Secure` em produção |
| Step-up | `RequerStepUpFilter` exige scope token `step_up` recente para ações sensíveis |
| Comparações timing-safe | recovery codes, OTP e-mail, `X-Internal-Key`, WhatsApp verify token (`CryptographicOperations.FixedTimeEquals`) |
| Security headers | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Permissions-Policy: camera=(), microphone=(), geolocation=()`, `HSTS` (produção) |
| CORS | `Cors:AllowedOrigins` (sem wildcard, `Uri` absoluta), `AllowCredentials`, header `X-Step-Up-Token` liberado |
| Forwarded headers | `X-Forwarded-For/Proto` só em Production/Homolog, `ForwardLimit=1` |
| Isolamento | `TreinadorId` em todas as queries — sem multi-tenant, sem RLS |
| Erro sem vazamento | `GlobalExceptionHandler` nunca expõe stack trace (500 genérico) |
| Stripe webhook | Assinatura `Stripe-Signature` via `ConstructEvent` antes de processar |
| Body size limit | `LimitedStream` limita webhooks a 64 KB (anti-DoS) |
| Open redirect | URLs de retorno do onboarding validadas contra `Stripe:UrlBase` |
| Idempotência | `stripe_payment_intent_id` UNIQUE (DB); `Idempotency-Key` nas Create requests Stripe; partial unique de pagamento pendente por assinatura |

---

### Notificações

Dois canais externos + in-app. Cada integração segue o padrão **real/null**: sem config, usa no-op sem falhar no startup — **exceto e-mail em Production** (chave `Resend:ApiKey` vazia em prod falha no boot, nunca cai silenciosamente no Null).

#### E-mail — Resend

`ResendEmailService` (ativo com `Resend:ApiKey`) / `NullEmailService`. Sempre embrulhado por `EnvironmentEmailDecorator`: fora de produção prefixa assunto, injeta banner de teste e redireciona destinatários (allowlist). ~30 templates em `EmailTemplates.cs`.

Disparos por domain event: ciclo do treinador, vínculo, assinatura (criada/cancelada/reativada/inadimplente), boas-vindas/inativação do aluno, pagamentos (criado/falhou/estornado/disputa), billing do plano do treinador, NFS-e (emitida/bloqueada), suporte, pré-aviso de renovação. Fluxos sob demanda (não-evento): verificação de e-mail, reset de senha, troca de e-mail (OTP ao **novo** endereço), OTP de login/step-up, nudge de engajamento, digest diário do treinador, relatório de saúde. Login bloqueia com **403 `EMAIL_NAO_VERIFICADO`** até verificar.

E-mails de segurança (verificação/reset/MFA) e do ciclo do treinador são **ungated**; os operacionais respeitam o gate por tier. Opt-out de engajamento via `PATCH /conta/preferencias-notificacao` (só suprime engajamento). Entrega: `POST /webhooks/resend` (assinatura Svix) grava `delivered/bounced/complained/spam_complaint` em `email_delivery_logs` (destinatário **hasheado**).

#### WhatsApp — Meta Cloud API

`MetaWhatsAppCloudNotifier` (ativo com `WhatsApp:PhoneNumberId` **e** `WhatsApp:AccessToken`) / `NullWhatsAppNotifier`. Embrulhado por `EnvironmentWhatsAppDecorator` (redirect/allowlist fora de prod). 17 handlers em paridade com o e-mail, usando mensagens `type:template` (`WhatsAppTemplates`). Todos gated (WhatsApp é cobrado por template pela Meta). `PhoneNumberNormalizer` normaliza para E.164 (DDI BR 55 default, sem `+`).

Endpoint: `POST https://graph.facebook.com/{ApiVersion}/{PhoneNumberId}/messages`. Webhook: `GET /webhooks/whatsapp` (handshake `hub.verify_token`) + `POST` (assinatura `X-Hub-Signature-256`) → `whatsapp_delivery_logs` (telefone hasheado, idempotente por `meta_message_id`).

> **Ativar WhatsApp**: Meta Business Manager → app no Meta for Developers com produto WhatsApp → número → `PhoneNumberId` → token **permanente** (System User Token; o de teste expira em 24h). Free tier (sandbox): só até 5 números verificados até a Meta revisar o app.

#### In-app — `Notificacao`

Feed próprio por conta. Criadas por `TreinoDisponibilizadoEvent` (todos os tiers, ao contrário do e-mail/WhatsApp gemeados), `ExecucaoRegistradaEvent` (ao treinador) e jobs de engajamento (reforço/lembrete/streak/digest, deduplicados por índice parcial `(conta, tipo, dia)`). Endpoints `/notificacoes` (feed, contador de não-lidas, marcar lida). Purgadas após 90 dias.

#### Gate por tier — `PlanoNotificationPolicy`

`ResolverPorTreinadorAsync`/`ResolverPorAlunoAsync` → `CanaisNotificacao(Email, WhatsApp)` a partir do `Tier` do plano. Regra: **e-mail ≥ Pro**, **WhatsApp ≥ ProPlus**. Sem plano → nenhum canal. E-mails de segurança/conta ignoram o gate.

---

### Outbox e Reconciliação

**Transactional Outbox** (`OutboxEfeito`): efeitos que não podem ser perdidos (criação de assinatura no vínculo, e-mail crítico, submissão de evidência de disputa Stripe) são gravados no **mesmo commit** do agregado de origem. `Tipo` = `evt:<CLR>` (re-dispatch durável de domain event) ou `fx:<nome>` (efeito nomeado, enfileirado via `IOutboxEnfileirador`). O `OutboxProcessor` faz lease de lote (`FOR UPDATE SKIP LOCKED`), despacha e avança status atomicamente; retry com backoff exponencial (falha permanente vs transitória), 5 tentativas. Hospedado por `OutboxProcessorService` (poll ~10s); `OutboxLimpezaService` purga concluídos (>7d).

**Reconciliação Stripe** (`ReconciliacaoStripeEstado`): cursor high-water-mark. `POST /internal/reconciliar-pagamentos` lista eventos Stripe desde o cursor (ou now−7d), reprocessa cada um pelo mesmo núcleo do webhook (idempotente), persiste o cursor a cada 100 eventos e também sonda contas Connect pendentes. Se a lista atingir o cap (1000), retorna **503** deliberado — o cron `billing-reconciliation.yml` trata como falha e abre issue, sinalizando backlog não processado.

---

### Fiscal — Dados do Treinador

Emissão de NFS-e foi **removida** deste backend (feature `remocao-emissao-nfse`) — a emissão em si passou a ser feita por software fiscal terceiro, fora deste repositório. O que resta aqui é só a **coleta** dos dados fiscais do treinador (CPF/CNPJ + endereço), retida por obrigação legal: `PUT/GET /treinador/dados-fiscais`, autofill `GET /treinador/cep/{cep}`. VO `Treinador.DadosFiscais` (colunas `dados_fiscais_*` na tabela `treinadores`). Canônico: [`specs/specification-fiscal.md`](specs/specification-fiscal.md) (arquivada, documenta o estado real pós-remoção).

---

### Endpoints

Auth por grupo indicada no cabeçalho. Endpoints paginados validam `pagina`/`tamanhoPagina` via `PaginacaoFilter`. `[step-up]` = exige `X-Step-Up-Token`.

#### Auth — `/auth` (público · rate `auth`)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `POST` | `/auth/login` | `{ email, senha }` | `200` sessão, ou scope token `mfa_pending` se MFA |
| `POST` | `/auth/refresh` | — (cookie `refresh`) | `200` renova (rotaciona) · `401` genérico |
| `POST` | `/auth/register/treinador` | `{ nome, email, senha, planoPlataformaId, modoPagamentoAluno, telefone? }` | `201 TreinadorResponse` |
| `POST` | `/auth/register/aluno` | `{ nome, email, senha, treinadorId, pacoteId, telefone?, anamnese*, consentimento* }` | `201 AlunoResponse` |
| `POST` | `/auth/treinador/{id}/pagamento` | `{ metodo }` | `200` (1ª cobrança do plano no cadastro) |
| `POST` | `/auth/verify-email` | `{ token }` | `200` · `400` |
| `POST` | `/auth/resend-verification` | `{ email }` | `200` (não vaza existência) |
| `POST` | `/auth/forgot-password` | `{ email }` | `200` (não vaza existência) |
| `POST` | `/auth/reset-password` | `{ token, novaSenha, codigoTotp? }` | `200` · `400` |
| `GET` | `/auth/planos` | — | `[PlanoPlataformaResponse]` |
| `GET` | `/auth/treinadores` · `/auth/treinadores/{id}/pacotes` | — | listas públicas p/ cadastro |

#### MFA / Step-up — `/auth/mfa`, `/auth/step-up` (rate `mfa`)

| Método | Rota | Escopo | Resposta |
|--------|------|--------|----------|
| `POST` | `/auth/mfa/verificar` | `MfaPendente` | `200` completa login (TOTP/recovery, `lembrarDispositivo?`) |
| `POST` | `/auth/mfa/email/enviar` | `MfaPendente` | `200` envia OTP de login por e-mail |
| `POST` | `/auth/step-up/iniciar` | JWT | `200` (TOTP se habilitado, senão OTP e-mail) |
| `POST` | `/auth/step-up/verificar` | JWT | `200` emite scope token `step_up` (5min) |

#### Conta — `/conta` (JWT · rate `write`)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `GET`/`PATCH` | `/conta/perfil` | `{ nome }` | `200`/`204` |
| `PATCH` | `/conta/preferencias-notificacao` | `{ emailEngajamentoOptOut }` | `204` |
| `POST` | `/conta/senha` | `{ senhaAtual, novaSenha }` | `204` `[step-up]` |
| `POST` | `/conta/email/trocar` | `{ novoEmail }` | `202` `[step-up]` (OTP ao novo e-mail) |
| `POST` | `/conta/email/confirmar` | `{ codigo }` | `204` · `400` |
| `POST` | `/conta/logout` | — | `204` (revoga família + jti) |
| `GET` | `/conta/lgpd/exportar` | `?formato=xlsx?` | `200` JSON ou `.xlsx` |
| `DELETE` | `/conta/lgpd` | `{ senha }` | `204` anonimização self-service |

#### MFA da conta — `/conta/mfa` (JWT · rate `auth`)

`POST /conta/mfa/totp/iniciar` · `POST /conta/mfa/totp/confirmar` · `GET /conta/mfa/status` · `POST /conta/mfa/desabilitar` `[step-up]` · `POST /conta/mfa/recovery/regenerar` `[step-up]`.

#### Admin — `/admin` (política `SystemAdmin`)

Treinadores: `GET /admin/treinadores`, `GET /admin/treinadores/{id}`, `POST .../aprovar|reprovar|inativar` `[step-up]`, `DELETE /admin/treinadores/{id}` (só Inativo; hard delete), `PATCH .../plano` (define/remove **cortesia** de plano — `{ planoId? }`, `null` remove). Planos: `GET/POST /admin/planos`, `PATCH/DELETE /admin/planos/{id}`. Grupos musculares e exercícios globais: CRUD sob `/admin/grupos-musculares` e `/admin/exercicios`. Visibilidade (read-only): `/admin/alunos*`, `/admin/fichas/{id}`, `/admin/treinadores/{id}/{alunos|vinculos|treinos|pacotes}`, `/admin/treinos/{id}`. Dashboards: `GET /admin/stats/dashboard`, `GET /admin/dashboard`. Health report: `GET/PUT /admin/health-report/config`, `GET /admin/health-report/snapshots`, `POST /admin/health-report/run`. LGPD: `GET /admin/contas/{id}/lgpd/exportar`, `DELETE /admin/contas/{id}/lgpd` (sem senha). `/admin/test-data/*` existe apenas fora de Production.

#### Treinador — `/treinador` (política `Treinador`)

Vínculos: `GET /treinador/vinculos`, `POST .../{id}/aprovar` `{ pacoteId, trarFichas? }`, `POST .../{id}/desvincular`, `POST /treinador/alunos/{id}/reativar`, `PATCH /treinador/alunos/{vinculoId}/preservar` `{ preservar }` (protege da apara). Alunos/fichas/progressão: `GET /treinador/alunos*`, atribuir/remover ficha. Exercícios (próprios + globais) e pacotes: CRUD + `POST /treinador/exercicios/{id}/copiar`. Dashboard: `GET /treinador/dashboard`. Modo de pagamento: `POST /treinador/modo-pagamento`, `GET .../preview`. Stripe: `POST /treinador/onboarding` `[step-up]`, `GET /treinador/onboarding/status`, `POST /treinador/pagamentos/cobrar/{assinaturaId}`, `GET /treinador/pagamentos/recebimentos`. Plano (billing): `GET /treinador/plano/assinatura`, `GET /treinador/plano/pagamento/{id}`, `POST /treinador/plano/{contratar|trocar|cobrar|cancelar}`. Fiscal: `GET/PUT /treinador/dados-fiscais`, `GET /treinador/cep/{cep}` (autofill).

#### Treinos — `/treinos` (política `Treinador`)

`POST /treinos`, `GET/PATCH/DELETE /treinos/{id}` (delete proibido se executado), `GET /treinos/{id}/alunos`, `POST /treinos/{id}/vincular-aluno`, `POST/PUT/DELETE` de exercícios da ficha, `PATCH .../observacao`, `POST /treinos/{id}/duplicar`. Criar/vincular/duplicar exigem assinatura de treinador ativa.

#### Área do Aluno — `/aluno` (política `Aluno`)

`GET /aluno/assinatura`, `POST /aluno/assinatura/cancelar`, `GET /aluno/vinculo`, `GET /aluno/dashboard`, `POST /aluno/troca-treinador`, `GET /aluno/fichas*`, `GET /aluno/execucoes`, `POST /aluno/execucoes` (header `Idempotency-Key`; exige assinatura ativa), `GET /aluno/progressao`, `PUT /aluno/anamnese`. Pagamentos: `GET /aluno/pagamentos/{id}`, `GET /aluno/pagamentos/assinatura/{id}` (rate `read`).

#### Alunos / Exercícios (JWT)

`/alunos` (`GET` lista/detalhe/treinos, `PATCH /alunos/{id}`, `PATCH /alunos/{id}/status` só SystemAdmin). `/exercicios` (`GET`/`POST` — legado, subconjunto de `/treinador/exercicios`).

#### Notificações — `/notificacoes` (JWT)

`GET /notificacoes` · `GET /notificacoes/nao-lidas/contador` · `PATCH /notificacoes/{id}/lida`.

#### Suporte — `/suporte` (JWT)

`POST /suporte/mensagens` `{ categoria, assunto, descricao }` → `202` (identidade vem do token).

#### Webhooks (público — verificação por assinatura · rate `webhook` · body ≤64 KB)

`POST /webhooks/stripe` (`Stripe-Signature`) · `POST /webhooks/resend` (Svix) · `GET /webhooks/whatsapp` (handshake Meta) · `POST /webhooks/whatsapp` (`X-Hub-Signature-256`).

#### Internal — `X-Internal-Key` (rate `internal`)

`POST /internal/processar-renovacoes` · `/internal/processar-renovacoes-treinador` · `/internal/processar-pre-avisos` · `/internal/processar-pre-avisos-treinador` · `/internal/processar-engajamento` · `/internal/reconciliar-pagamentos` (503 se truncado) · `/internal/gerar-nfse-comissao` · `/internal/reconciliar-nfse` · `/internal/processar-limite-alunos` (graça/apara) · `/internal/lgpd/contas-elegiveis` · `DELETE /internal/lgpd/contas/{id}`.

#### Infra

`GET /health` (liveness) · `GET /health/ready` (readiness — db + schema + integrações).

---

### Regras de Negócio

#### Fluxo de Aprovação

```
Treinador:  Cadastro → [plano pago: AguardandoPagamento → (1ª cobrança paga)] → AguardandoAprovacao
                     → (admin aprova) → Ativo
                     → (admin reprova) → sem perfil (conta permanece)
                     → (admin inativa) → Inativo

Aluno+Vínculo: Cadastro → AguardandoAprovacao → (treinador aprova vínculo) → Ativo
                                              → (treinador desvíncula) → Inativo
```

Toda aprovação/reprovação/inativação registra um `LogAprovacao` (`EntidadeId` sem FK — sobrevive a hard deletes).

#### Limites de Plano (tier efetivo, cortesia, graça)

O cap de alunos vale sobre o **tier efetivo**, não o plano contratado. `PlanoEfetivoResolver` resolve o plano vigente = o **mais caro** entre a assinatura paga Ativa e uma **cortesia** administrativa (`Treinador.PlanoCortesiaId`), com fallback fail-closed para o plano Free canônico (`cap=0` se nem Free existir). Cortesia é concedida pelo admin (`PATCH /admin/treinadores/{id}/plano`, `planoId=null` remove) e **nunca rebaixa** quem já paga um tier superior.

`LimiteTreinadorService` valida `MaxAlunos` (do tier efetivo) vs alunos ativos ao aprovar vínculo → `LimiteAlunosAtingidoException` → 422. Mas um treinador pode **passar** do cap sem nova aprovação — por downgrade, inadimplência que derruba a assinatura, ou remoção de cortesia. Nesse caso entra a **graça de limite** (janela de 3 meses):

1. `ProcessarLimiteAlunosHandler` (job diário) recomputa `excedente = max(0, ativos − cap)` ao vivo. Excedente novo → carimba `AlunosAcimaDoCapDesde`, notifica in-app (`LimiteAlunosExcedido`) + e-mail com prazo (`agora + 3 meses`). Nada é desativado.
2. Dentro da janela: lembretes nos marcos D-30/D-7/D-1 (`LimiteAlunosLembrete` + e-mail).
3. No fim da janela, se ainda excedido: **apara** — inativa os vínculos excedentes por antiguidade, respeitando os marcados `PreservarNoLimite` (proteção só vale dentro do cap); notifica `LimiteAlunosAplicado` + e-mail. Regularizar (upgrade, reativar assinatura, nova cortesia) a qualquer momento limpa o carimbo e cancela a apara.

**Race-guard**: cada commit é envolto por `CommitOuIgnorarConcorrenciaAsync` — conflito de concorrência otimista (regularização concorrente durante os `await`s) descarta as alterações e loga em Debug; a próxima execução reprocessa. O job é `POST /internal/processar-limite-alunos` (cron diário `limite-alunos.yml`).

`Pacote` não tem limite de fichas (campo removido; controle via `Descricao` livre).

#### Billing do Treinador (treinador → plataforma)

1. **Cadastro em plano pago** (`Preco > 0`): treinador nasce `AguardandoPagamento` + `AssinaturaTreinador` `Pendente`; a 1ª cobrança (`Finalidade=Cadastro`) confirmada via webhook **finaliza o cadastro inline**. Elite bloqueado no cadastro; Free segue sem assinatura.
2. **Renovação mensal**: `POST /internal/processar-renovacoes-treinador` gera cobranças vencidas; pagamento confirmado (`Renovacao`) reativa e agenda a próxima.
3. **Troca de plano** (`POST /treinador/plano/trocar`): upgrade/downgrade/regularização. Downgrade p/ Free **encerra** sem cobrança; troca efetiva aplicada no pagamento confirmado (`TrocaPlano`); `PlanoPlataformaIdAgendado` guarda a pendente.
4. **Inadimplência**: falha de cobrança recorrente marca `Inadimplente` + e-mail.

`Treinador.ModoPagamentoAluno` (`Plataforma`/`Externo`) define se o aluno paga via plataforma (Stripe Connect) ou direto ao treinador. Troca tem cooldown de 90 dias.

#### Cascata de Inativação

| Ação | Efeito em cascata |
|------|-------------------|
| Inativar `Treinador` | Inativa vínculos ativos → inativa `TreinoAluno` dos pares afetados |
| Desvincular `Vinculo` | Inativa `TreinoAluno` do par (treinador × aluno) |

#### Hard Delete de Treinador

Só se `Status == Inativo`. Remove em cascata (transação única): execuções → treino-alunos → treino-exercícios → treinos → exercícios → pacotes → vínculos → treinador → conta. `LogAprovacao` preservado (sem FK).

#### Troca de Treinador

`POST /aluno/troca-treinador` cria **apenas** um novo vínculo `AguardandoAprovacao` (o atual permanece ativo). Na aprovação (`POST /treinador/vinculos/{id}/aprovar`), o vínculo anterior é inativado e, se `trarFichas = true`, as fichas ativas são duplicadas para o novo treinador.

#### Isolamento de Dados

Sem `TenantId`. Isolamento por `TreinadorId`. Handlers validam `IUserContext.PerfilId` contra o dono do recurso → `AcessoNegadoException` → 403.

---

### Tratamento de Erros

Caminho **primário**: `Result<T>` — handlers retornam falha tipada e os endpoints convertem via `ResultExtensions.ToProblemResult()` → **RFC 7807** (`NotFound→404`, `Conflict→409`, `Validation→400`, default `Business→422`, com `code` no corpo). O `GlobalExceptionHandler` é o caminho **residual**, para exceções de infra/auth:

| Exceção | Status | Cenário |
|---------|--------|---------|
| `CredenciaisInvalidasException` | 401 | Login com senha errada |
| `*NaoEncontradoException` | 404 | Recurso não existe / não pertence ao usuário |
| `AcessoNegadoException` | 403 | Acesso a recurso de outro treinador |
| `*InativoException` | 403 | Operação em entidade inativa |
| `EmailNaoVerificadoException` | 403 | Login com e-mail não verificado (`code = EMAIL_NAO_VERIFICADO`) |
| `DomainException` (e subclasses) | 422 | Violação de regra de negócio |
| `ValidationException` (FluentValidation) | 400 | Payload inválido (erros por campo) |
| `Qualquer outra` | 500 | Mensagem interna nunca exposta |

O core do pattern: `enum ErrorType { Business, Validation, NotFound, Conflict, ExternalService }`, `record Error(Code, Message, Type)`, `Result`/`Result<T>` em `Domain/Shared`. 33 agregados `*Errors.cs` (um `Error` por caso de falha).

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Unprocessable Content",
  "status": 422,
  "detail": "O limite de alunos do plano foi atingido."
}
```

---

### Configuração e Secrets

`appsettings.json` (base, sem segredos) traz `Logging`, `AllowedHosts`, `Auth` (`JwtSecret`/`JwtIssuer`/`JwtAudience` + `Sessao:{SystemAdmin|Treinador|Aluno}:{AccessMinutes|RefreshIdleMinutes|AbsolutoMinutes}`), `Mfa:EncryptionKey`, `Cors:AllowedOrigins`, `ConnectionStrings:AppConnection`, `App:FrontendBaseUrl`, `Resend`, `Email`, `Stripe` (`PublishableKey`/`TaxaPlataformaPercent`/`UrlBase`), `ViaCep`, `Hibp`. Overrides por ambiente em `appsettings.{Development,Homolog,Production}.json`.

Chaves **só via user-secrets / env** (nunca no appsettings versionado):

```bash
# Núcleo (obrigatório para rodar)
dotnet user-secrets set "Auth:JwtSecret"              "<min-32-bytes>"   --project forzion.tech.Api  # openssl rand -base64 64
dotnet user-secrets set "Mfa:EncryptionKey"           "<base64-32>"      --project forzion.tech.Api  # openssl rand -base64 32
dotnet user-secrets set "DataProtection:EncryptionKey" "<base64-32>"     --project forzion.tech.Api
dotnet user-secrets set "ConnectionStrings:AppConnection" "<conn-string>" --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminEmail"             "admin@forzion.tech" --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminPassword"          "<senha>"          --project forzion.tech.Api
dotnet user-secrets set "Internal:ApiKey"             "<chave-interna>"  --project forzion.tech.Api

# Stripe (módulo de pagamentos)
dotnet user-secrets set "Stripe:SecretKey"            "sk_test_..."      --project forzion.tech.Api
dotnet user-secrets set "Stripe:WebhookSecret"        "whsec_..."        --project forzion.tech.Api

# E-mail Resend (omitir = NullEmailService; obrigatório em Production)
dotnet user-secrets set "Resend:ApiKey"               "re_..."           --project forzion.tech.Api

# WhatsApp Meta (omitir = NullWhatsAppNotifier)
dotnet user-secrets set "WhatsApp:PhoneNumberId"      "<phone-number-id>" --project forzion.tech.Api
dotnet user-secrets set "WhatsApp:AccessToken"        "<token-permanente>" --project forzion.tech.Api
```

User Secrets ID: `forzion-prod`. Seções adicionais suportadas via env: `Outbox:*` (tuning do processor), `DeliveryLog:RecipientHashKey`, `DomainEvents:MaxConcorrenciaBestEffort`, `RateLimiting:DesabilitarParaTeste`. O `.env.example` (docker-compose) cobre `JWT_SECRET`, `MFA_ENCRYPTION_KEY`, `DATA_PROTECTION_KEY`, `STRIPE_*`, `SEED_*`.

| Ambiente | Schema | Scalar (UI) | Seeder | Modo |
|----------|--------|---------|--------|------|
| `Development` | `homolog` (via search_path) | ✅ | ✅ (auto-migrate + seed no boot) | `dotnet run` |
| `Homolog` | `homolog` | ❌ | migrate one-shot | `ASPNETCORE_ENVIRONMENT=Homolog` |
| `Production` | `public` | ❌ | migrate one-shot | Container Docker |
| `Test` | in-memory (Infra não registrada) | ❌ | ❌ | `dotnet test` |

**Hosted services** (fora de `Test`): `LimparTokensRevogadosService` (purga tokens/famílias/MFA/logs expirados, 1h), `OutboxProcessorService` (~10s), `OutboxLimpezaService` (1h), `RelatorioSaudeDiarioService` (poll 15min, envia 1x/dia), `ErrorLogDbSinkDrenoService` (dreno de logs de erro).

---

### Stripe CLI (webhook local)

Para validar webhooks Stripe em dev sem expor a porta, use o [Stripe CLI](https://docs.stripe.com/stripe-cli) — encaminha eventos do Test Mode para `localhost` via tunnel HTTPS e injeta o `whsec_*` certo.

```bash
# Instalação — Windows (Scoop)
scoop bucket add stripe https://github.com/stripe/scoop-stripe-cli.git
scoop install stripe
# macOS: brew install stripe/stripe-cli/stripe

stripe login
stripe listen --forward-to localhost:8080/webhooks/stripe   # imprime o whsec_* efêmero
```

Copie o `whsec_*` efêmero para `Stripe:WebhookSecret` enquanto o tunnel roda. Disparar eventos: `stripe trigger payment_intent.succeeded | payment_intent.payment_failed | charge.refunded | charge.dispute.created`. `ProcessarWebhookStripeHandler` deve marcar `Pagamento.Status` consistente (Pago / Falhou+inadimplência / Estornado / EmDisputa).

> **Atenção**: o `whsec_*` do `stripe listen` difere do painel hmg/prod. Restaurar o secret de hmg em `user-secrets` ao terminar.

---

### Migrations

55 migrations, schema-agnostic. As primeiras 29 montam o domínio; da 30 em diante entram MFA, NFS-e, refresh tokens, outbox, notificações e endurecimentos.

| # | Migration | O que faz |
|---|-----------|-----------|
| 1 | `InicioDominio` | Schema inicial completo (**contém `DROP TABLE`**) |
| 2 | `AdicionarGruposMusculares` | Tabela `grupos_musculares` + FK em `exercicios` |
| 3 | `UniqueConstraintTreinoAlunoPorFicha` | Unique em `treino_alunos (treino_id, aluno_id)` |
| 4 | `RemoverMaxFichasAdicionarDescricaoPacote` | Remove `max_fichas`; adiciona `descricao` em `pacotes` |
| 5 | `AdicionarDadosFisicosAluno` | Campos físicos/anamnese em `alunos` |
| 6 | `SeriesConfiguraveisExercicio` | `series`/`repeticoes` configuráveis em `treino_exercicios` |
| 7 | `AdicionarDificuldadeEDatasNaTreino` | `dificuldade` + datas em `treinos` |
| 8 | `AdicionarObservacaoTreinoExercicio` | `observacao` em `treino_exercicios` |
| 9 | `AdicionarTelefoneNaTabelaTreinadores` | `telefone` em `treinadores` |
| 10 | `AdicionarTokenRevogado` | Tabela `tokens_revogados` (jti PK) + índice |
| 11 | `AdicionarIndicesPerformance` | Índices em `treinadores(status)`, `vinculos(treinador_id, status)` |
| 12 | `AdicionarPagamentos` | Stripe em `treinadores`; tabelas `assinaturas`/`pagamentos` |
| 13 | `SegurancaPagamentos` | Partial unique de pagamento pendente + unique `stripe_payment_intent_id` |
| 14 | `CartaoPagamento` | `client_secret` + `metodo_pagamento` em `pagamentos` |
| 15 | `AdicionarTierPlanoTreinador` | `tier` em planos + seed Free/Basic/Pro/ProPlus/Elite |
| 16 | `AdicionarDescricaoPlanoTreinador` | `descricao` em planos |
| 17 | `AdicionarIndiceAlunoStatus` | Índice parcial em `alunos(status)` |
| 18 | `AddAssinanteBillingProjection` | Tabela `assinantes` (projeção read-side) |
| 19 | `MoverStripeParaContaRecebimento` | Extrai Stripe Connect para `contas_recebimento` |
| 20 | `ExercicioGrupoMuscularFk` | FK `grupo_muscular_id` em `exercicios` + backfill |
| 21 | `RenomearPlanoPacoteAssinatura` | Rename data-preserving (planos_plataforma/pacotes/assinaturas_aluno) |
| 22 | `AdicionarPasswordResetToken` | Tabela `password_reset_tokens` |
| 23 | `AdicionarEmailDeliveryLogs` | Tabela `email_delivery_logs` |
| 24 | `AdicionarVerificacaoEmail` | Tabela `email_verification_tokens` + colunas em `contas` |
| 25 | `AdicionarHealthReport` | Tabelas `health_snapshots` + `error_log` |
| 26 | `AdicionarTentativasFalhasConsecutivas` | Contador de inadimplência em `assinaturas_aluno` |
| 27 | `AdicionarWhatsAppDeliveryLogs` | Tabela `whatsapp_delivery_logs` |
| 28 | `AdicionarAnonimizadaEmContas` | Colunas de anonimização em `contas` (LGPD) |
| 29 | `AdicionarBillingTreinadorEModoPagamento` | Tabelas billing treinador + `modo_pagamento_aluno` |
| 30 | `AdicionarModoPagamentoAlteradoEm` | Timestamp de cooldown de troca de modo de pagamento |
| 31 | `AdicionarConcurrencyTokenTreinador` | Token de concorrência otimista em `treinadores` |
| 32 | `AdicionarOutboxEfeitos` | Tabela `outbox_efeitos` (transactional outbox) + índices |
| 33 | `UniqueDeliveryLogIdempotencia` | Índices únicos de idempotência nos delivery logs |
| 34 | `AdicionarAnonimizadoEmAlunosETreinadores` | Flags `anonimizado` em `alunos`/`treinadores` |
| 35 | `AdicionarMensagemSuporte` | Tabela `mensagens_suporte` |
| 36 | `AdicionarRefreshTokens` | Tabelas `refresh_tokens` + `refresh_token_families` |
| 37 | `AdicionarTokenEpochConta` | `sessoes_invalidas_antes_de_utc` em `contas` (epoch de sessão) |
| 38 | `DeliveryLogPseudonimizarRecipient` | Substitui e-mail/telefone crus por hash nos delivery logs (LGPD) |
| 39 | `AdicionarCheckConstraintsIntegridade` | CHECK constraints de integridade de domínio |
| 40 | `AdicionarTabelasMfa` | Tabelas `conta_mfa`, `mfa_challenges`, `mfa_recovery_codes`, `trusted_devices` |
| 41 | `CriarNotasFiscaisEDadosFiscaisTreinador` | Tabela `notas_fiscais` + colunas `dados_fiscais_*` em `treinadores` |
| 42 | `AdicionarTrocaEmailToken` | Tabela `troca_email_tokens` |
| 43 | `AdicionarCancelamentoPendentePreEmissaoNfse` | Colunas de cancelamento pré-emissão em `notas_fiscais` |
| 44 | `AdicionarDataProtectionKeys` | Tabela `data_protection_keys` (chaves DataProtection cifradas) |
| 45 | `AdicionarIdempotencyKeyExecucao` | `idempotency_key` + índice em `execucoes_treino` |
| 46 | `ExercicioOrientacao` | `como_executar` + vídeo em `exercicios` |
| 47 | `AdicionarUniqueParcialResetTokenPendente` | Único parcial: um reset token pendente por conta |
| 48 | `InativarPlanoEliteEAtualizarDescricoesPlanos` | Data migration: inativa Elite + atualiza descrições |
| 49 | `AdicionarConcurrencyTokenPagamentos` | Token de concorrência otimista em pagamentos |
| 50 | `CriarReconciliacaoStripeEstado` | Tabela `reconciliacao_stripe_estado` (cursor) |
| 51 | `AdicionarIndiceExecucaoAlunoData` | Índices em `execucoes_treino(aluno_id, data)` |
| 52 | `RedefinicaoSenhaSegundoFatorLockout` | Tabela `redefinicao_senha_segundo_fator` (2FA lockout no reset) |
| 53 | `AdicionarUniqueParcialAssinaturaTreinadorNaoCancelada` | Único parcial: uma assinatura treinador não-cancelada por treinador |
| 54 | `AdicionarNotificacoes` | Tabela `notificacoes` + índice de dedup + opt-out em `contas` |
| 55 | `AdicionarCortesiaEGracaLimiteAlunos` | `treinadores.plano_cortesia_id` (FK RESTRICT) + `alunos_acima_do_cap_desde`; `vinculos.preservar_no_limite` |
| 56 | `AdicionarEmailEnviadoHealthSnapshot` | Coluna `email_enviado` em `health_snapshots` |
| 57 | `RemoverNotasFiscais` | Drop da tabela `notas_fiscais` (NFS-e removida do backend) |
| 58 | `AdicionarAiTokenUsage` | Tabela `ai_token_usage` (telemetria, fora do domain model) |

> A `__EFMigrationsHistory` em runtime é pinada no schema do `search_path`; o design-time fica sem schema para scripts portáveis. Ver [`specs/specification-db.md`](specs/specification-db.md).

---

### Testes e CI

```
3.378 testes unit (rápidos, sem Docker, 0 falhas) + integração via Testcontainers (Postgres real, no CI)

Domain/                  → entidades, VOs, domain events, exceções, máquinas de estado
Domain/Properties/       → property-based (CsCheck): Email VO, Result<T>, invariantes
Application/             → handlers (unit), lógica temporal (FakeTimeProvider)
Architecture/            → arch tests (NetArchTest): direção de dependência, convenções
Api/Snapshots/           → snapshot/contract (Verify.Xunit): DTOs + mapa exceção→ProblemDetails
Api/Endpoints/           → WebApplicationFactory (auth, status codes, isolamento, paginação)
Infrastructure/          → JwtService, MFA, email/whatsapp handlers, dispatch (re-entrância)
Infrastructure/Repositories/ → Testcontainers.PostgreSql (banco real)  [Integration]
E2E/                     → pipeline real: WAF + Postgres + migrations + seed, só Stripe fake  [Integration]
```

**Determinismo**: `TimeProvider` injetado; testes usam `FakeTimeProvider` (sem `DateTime.UtcNow` nas factories). **Split**: testes com Docker levam `[Trait("Category","Integration")]`.

**CI/CD** (`.github/workflows/ci.yml`, "CI / CD — Homolog") roda com path-filter (mudança só-frontend pula jobs backend e vice-versa):

- `test-backend-unit` — build (Release) + `dotnet format --verify-no-changes` + testes unit + cobertura. Gates: Domain **branch 86 / line 93 / method 95**, Application **81 / 90 / 93**, Api **77 / 88 / 81**.
- `test-backend-integration` (PR) — suíte completa (Testcontainers). Gates: global **branch 80**, Infrastructure **76 / 88 / 87**.
- `test-frontend` — lint + `tsc --noEmit` + vitest + cobertura. `build-frontend` — build Next + Storybook.
- `gitleaks` (árvore inteira), `security-backend` (vuln NuGet + SBOM CycloneDX), `security` (osv + npm audit + license + SBOM), `lint-migrations` (migration arriscada), `zap-baseline` (DAST passivo), `commitlint` (PR).
- `gate` agrega os required; `deploy-homolog` deploya via Tailscale + SSH (ver Deploy). Pós-deploy: `pact-publish` + `pact-provider-verify`.

Workflows dedicados: `mutation.yml` (Stryker), `openapi-drift.yml`, `contract.yml` (Pact file-source PR gate), `semgrep.yml` (SAST), `lighthouse.yml`, `smoke.yml`, `db-backup.yml`, `lgpd-purge.yml`, `billing-{renewal,renewal-treinador,prenotification,reconciliation}.yml`, `gerar-nfse-comissao.yml`, `reconciliar-nfse.yml`, `limite-alunos.yml` (graça/apara diária), `release-images.yml`, `deploy-prod.yml`, `hygiene.yml`, `zap.yml`, `pact-provider.yml`.

---

## Frontend

Ver [`frontend/README.md`](frontend/README.md) para detalhes.

**Stack**: Next.js 16 (App Router) · React 19 · MUI v9 · TypeScript · React Hook Form + Zod · Axios · @tanstack/react-query (estado servidor). O frontend atua como **BFF** — os cookies de sessão (`token`/`refresh`/`mfa_pending`/`trusted_device`, httpOnly/SameSite=Strict) são setados server-side, e o proxy Next.js repassa para o backend.

```bash
cd frontend
npm install
npm run dev       # http://localhost:3000
npm test          # Vitest (unit + integration + api)
npm run e2e       # Playwright (pós-deploy, contra homolog)
npm run build     # build de produção (standalone)
npm run storybook # component workshop
```

---

## Deploy

### Docker local

```bash
cp .env.example .env   # preencher JWT_SECRET, MFA_ENCRYPTION_KEY, DATA_PROTECTION_KEY, STRIPE_*, SEED_*
docker compose up --build
# Backend :8080 · Frontend :3001 · Scalar :8080/scalar (só Development)
```

### Produção — VPS Hostinger + Supabase

- **VPS Hostinger** (Ubuntu) com Docker Compose
- **Supabase** como banco (PostgreSQL managed) — schema `homolog` (staging) / `public` (produção)
- **Nginx** como borda ÚNICA (`docker-compose.edge.yml`) — um só reverse proxy com TLS (Let's Encrypt via Certbot) serve homolog + produção, roteando por `server_name`; `infra/fail2ban` protege o endpoint de auth

> **Homologação** (`homologacao.forzion.tech`, schema `homolog`) tem deploy automatizado no push para `homolog`. **Produção** (`forzion.tech`/`app.forzion.tech`, schema `public`) tem pipeline de imagem e deploy prontos — o merge `homolog → main` dispara `release-images` (build + push das imagens para o GHCR) que, ao concluir, aciona `deploy-prod`. O deploy real é **gated por `PROD_DEPLOY_ENABLED`**. Referência: [`specs/specification-infrastructure.md`](specs/specification-infrastructure.md).

#### Deploy de homologação (automatizado)

Push em `homolog` → workflow `CI / CD — Homolog`. Passados os gates, o job `Deploy → homolog` entra no tailnet (Tailscale) e conecta na VPS via SSH pelo IP privado (a borda pública dropa o IP rotativo do runner):

```bash
cd /opt/forzion/app
git pull origin homolog
DC="docker compose -f docker-compose.homolog.yml --env-file /opt/forzion/.env"
$DC build
bash scripts/migrate-dryrun.sh          # Gate A: dry-run do migrate contra cópia do schema
$DC run --rm --no-deps backend migrate  # Gate B: migrate real (one-shot, antes do up)
$DC up -d --remove-orphans
# Gate C: health-gate pós-deploy (/health + /health/ready por dentro do container);
#         reprovou → rollback para a imagem anterior (tag guardada antes do build)
bash scripts/reload-edge.sh             # valida + recarrega o nginx de borda (edge)
```

As imagens de homologação são **buildadas na própria VPS**. As de produção vêm do **GHCR** (`release-images` → `docker-compose.server.yml`).

#### Provisionar a VPS (uma vez)

```bash
ssh ubuntu@<IP> 'bash -s' < scripts/setup-vm.sh          # Docker + /opt/forzion + .env template
bash scripts/init-ssl.sh seu@email.com homologacao.forzion.tech pact.homologacao.forzion.tech   # cert SSL inicial (multi-domínio)
```

Preencher `/opt/forzion/.env` (não versionado) com os secrets reais (`DB_CONNECTION`, `JWT_SECRET`, `MFA_ENCRYPTION_KEY`, `DATA_PROTECTION_KEY`, `STRIPE_*`, `RESEND_*`, `INTERNAL_*`, etc.). Lista completa em [`specs/specification-infrastructure.md`](specs/specification-infrastructure.md).

#### Arquitetura (runtime)

```
Cliente
  └── Nginx (80/443, TLS Let's Encrypt)
        ├── /webhooks/*  → backend:8080            (headers crus — Stripe/Resend/WhatsApp)
        └── /*           → frontend:3000           (Next.js BFF)
              └── /api/backend/*, /api/auth/*       → backend:8080  (proxy server-side)
                    └── PostgreSQL Supabase (schema do ambiente)
```

### DNS e E-mail — Hostinger + Resend

DNS gerenciado no **Hostinger** (hPanel). E-mail transacional via **Resend** (domínio `forzion.tech` verificado, DKIM + SPF no subdomínio `send.`). Detalhes em [`specs/specification-email.md`](specs/specification-email.md).

```
forzion.tech (DNS no Hostinger)
  ├── A     homologacao → IP da VPS Hostinger
  ├── (send.forzion.tech)  MX + TXT SPF → Resend (Amazon SES)
  └── TXT   resend._domainkey → DKIM Resend
```

- Webhook de entrega do Resend: `POST /webhooks/resend` (roteado pelo Nginx direto ao backend)
