# forzion.tech

Plataforma de gestão de treinos para personal trainers e alunos.

**Backend**: ASP.NET Core 8.0 · **Frontend**: Next.js 16 + MUI v9 · **Banco**: PostgreSQL (Supabase)

**Status**: ✅ 1067 testes backend (999 unit + 68 integração) + suíte frontend (Vitest + Playwright) | Clean Architecture | DDD tático + contextos Billing/GrupoMuscular | JWT próprio | Isolamento por TreinadorId | Stripe Connect | Harness de testes completo (arch tests, property-based, mutation, snapshot, E2E real) | Auditoria de segurança OWASP

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
  - [Rate Limiting](#rate-limiting)
  - [Segurança](#segurança-backend)
  - [Endpoints](#endpoints)
  - [Regras de Negócio](#regras-de-negócio)
  - [Tratamento de Erros](#tratamento-de-erros)
  - [Configuração e Secrets](#configuração-e-secrets)
  - [Migrations](#migrations)
  - [Testes](#testes)
- [Frontend](#frontend)
- [Deploy](#deploy)
  - [Docker local](#docker-local)
  - [Produção — OCI VM + Supabase](#produção--oci-vm--supabase)

---

## Pré-requisitos

| Ferramenta | Versão mínima |
|-----------|--------------|
| .NET SDK | 8.0 |
| Node.js | 20 LTS |
| Docker + Docker Compose plugin | 24+ |
| EF Core CLI | `dotnet tool install -g dotnet-ef` |
| PostgreSQL | 15+ (via Supabase ou local) |

---

## Estrutura do Repositório

```
forzion.tech/
├── forzion.tech.Api/          # HTTP — Minimal APIs, middleware, filtros
├── forzion.tech.Application/  # Use cases, handlers, validações, DTOs
├── forzion.tech.Domain/       # Entidades, Value Objects, Events, exceções
├── forzion.tech.Infrastructure/ # EF Core, repositórios, migrations, serviços
├── forzion.tech.Tests/        # xUnit + Moq + FluentAssertions + WebApplicationFactory
├── frontend/                  # Next.js 16 — ver frontend/README.md
├── nginx/                     # nginx.conf + nginx-init.conf (HTTPS + proxy)
├── scripts/                   # setup-vm.sh, init-ssl.sh
├── docker-compose.yml         # Stack completa para desenvolvimento local
├── docker-compose.server.yml  # Stack de servidor (sem banco — usa Supabase)
├── .dockerignore              # Exclui bin/, obj/, node_modules/ do build Docker
├── .env.example               # Variáveis necessárias para docker-compose
└── docs/                      # Documentação (gitignored, exceto docs/api/)
    └── api/swagger.v1.json    # Contrato OpenAPI versionado (baseline do gate openapi-drift)
```

> `docs/` (planos, notas de design gerados por agente) é ignorado pelo git via `.gitignore`, **exceto** `docs/api/` — o `swagger.v1.json` ali é o baseline do check de drift de contrato no CI.

---

## Setup Local

### Opção A — Docker Compose (mais rápido)

```bash
# 1. Copiar e preencher variáveis
cp .env.example .env
# editar .env com JWT_SECRET etc.

# 2. Subir tudo (backend + frontend + postgres local)
docker compose up --build

# Backend:  http://localhost:8080
# Frontend: http://localhost:3001
# Swagger:  http://localhost:8080/swagger
```

### Opção B — Manual

```bash
# 1. Backend — configurar secrets
dotnet user-secrets set "Auth:JwtSecret"   "<chave-hmac-32chars>"   --project forzion.tech.Api
dotnet user-secrets set "Auth:JwtIssuer"   "forzion.tech"            --project forzion.tech.Api
dotnet user-secrets set "Auth:JwtAudience" "forzion.tech"            --project forzion.tech.Api
dotnet user-secrets set "ConnectionStrings:AppConnection" "<conn-pg>" --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminEmail"    "admin@forzion.tech"    --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminPassword" "<senha>"               --project forzion.tech.Api

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

> No primeiro start em Development/Homolog, o `DataSeeder` cria automaticamente a conta admin definida em `Seed:AdminEmail` + `Seed:AdminPassword`, além de popular os 5 planos padrão da plataforma (Free, Basic, Pro, Pro Plus, Elite) de forma idempotente.

---

## Backend

### Stack

| Camada | Tecnologia |
|--------|------------|
| Framework | ASP.NET Core 8.0 / C# 12 / Minimal APIs |
| Banco de dados | PostgreSQL (Supabase) |
| ORM | Entity Framework Core 8.0 + Npgsql |
| Auth | JWT HMAC-SHA256 + BCrypt (sem Supabase Auth) |
| Validação | FluentValidation |
| Testes | xUnit + Moq + FluentAssertions + WebApplicationFactory |
| Documentação | Swagger/OpenAPI (`/swagger`) — non-production |

### Comandos

```bash
# Executar a API (HTTP :5230 | HTTPS :7220)
dotnet run --project forzion.tech.Api

# Executar em ambiente específico
ASPNETCORE_ENVIRONMENT=Homolog dotnet run --project forzion.tech.Api

# Build completo
dotnet build

# Todos os testes
dotnet test forzion.tech.Tests

# Teste único
dotnet test forzion.tech.Tests --filter "FullyQualifiedName~NomeDoTeste"

# Testes com cobertura
dotnet test forzion.tech.Tests --settings forzion.tech.Tests/coverage.runsettings

# Gerar migration (SEMPRE com Homolog)
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef migrations add <Nome> \
  --project forzion.tech.Infrastructure \
  --startup-project forzion.tech.Api

# Aplicar migration em homolog
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef database update \
  --project forzion.tech.Infrastructure \
  --startup-project forzion.tech.Api

# Gerar script SQL para produção (schema public)
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef migrations script --idempotent \
  --project forzion.tech.Infrastructure \
  --startup-project forzion.tech.Api \
  --output migration_public_schema.sql
```

> **Migrations**: sempre geradas com `Homolog`. Produção não suporta `database update` direto — gere o script, substitua `"homolog"` por `"public"` e aplique via Supabase SQL Editor.
>
> **Atenção**: `InicioDominio.Up()` contém `DROP TABLE` — nunca reaplicar em ambiente com dados.

---

### Estrutura do Projeto (Backend)

```
forzion.tech.Api/
├── Configuration/        # JWT, CORS, Swagger, Rate Limiting
├── Context/              # HttpUserContext — extrai claims do JWT
├── Endpoints/            # Minimal API por grupo de recurso
│   ├── Admin/            # /admin — SystemAdmin
│   ├── AlunoArea/        # /aluno — Aluno
│   ├── Alunos/           # /alunos — Treinador + Admin
│   ├── Auth/             # /auth — público
│   ├── Conta/            # /conta — perfil
│   ├── Exercicios/       # /exercicios — JWT obrigatório (GET + POST)
│   ├── Pagamentos/       # /aluno/pagamentos, /treinador/pagamentos, /internal, /webhooks/stripe
│   ├── Treinador/        # /treinador — Treinador
│   └── Treinos/          # /treinos
├── Extensions/
│   └── ResultExtensions.cs  # ToProblemResult() → RFC 7807 422
├── Filters/
│   ├── PerfilIdRequiredFilter.cs  # Garante claim perfil_id
│   └── PaginacaoFilter.cs         # Valida pagina >= 1, tamanhoPagina 1-100
├── Middleware/
│   └── GlobalExceptionHandler.cs  # Exceções → HTTP (RFC 7807)
└── Services/
    └── LimparTokensRevogadosService.cs  # BackgroundService: limpa tokens expirados a cada 1h

forzion.tech.Application/
├── Interfaces/
│   ├── IUserContext.cs            # ContaId, TipoConta, PerfilId
│   ├── IJwtService.cs
│   ├── IPasswordHasher.cs
│   ├── IUnitOfWork.cs
│   ├── IDomainEventDispatcher.cs
│   └── Repositories/              # Interface por entidade
├── Results/
│   └── Result<T>, Result          # Padrão Result para operações fallíveis
├── Services/
│   └── LimiteTreinadorService.cs  # Valida MaxAlunos ao aprovar vínculo
└── UseCases/                      # Handler CQRS-like por domínio
    ├── Admin/
    ├── Alunos/
    ├── AssinaturaAlunos/          # CriarAssinaturaAluno, CancelarAssinaturaAluno, ObterAssinaturaAluno
    ├── Auth/
    ├── Conta/
    ├── Exercicios/
    ├── Pacotes/
    ├── Pagamentos/                # GerarCobrancaMensal, ObterStatusPagamento,
    │                              # ListarPagamentosAssinatura, ProcessarWebhookStripe
    ├── Planos/
    ├── Treinadores/               # inclui IniciarOnboarding, VerificarOnboarding
    ├── Treinos/
    └── Vinculos/

forzion.tech.Domain/
├── Entities/             # Conta, Treinador, Aluno, VinculoTreinadorAluno,
│                         # Treino, TreinoExercicio, SerieConfig, TreinoAluno, Exercicio,
│                         # ExecucaoTreino, ExecucaoExercicio, LogAprovacao,
│                         # PlanoPlataforma, Pacote, GrupoMuscular, ContaRecebimento,
│                         # AssinaturaAluno, Assinante, Pagamento, SystemUser, TokenRevogado
├── Enums/                # TipoConta, TreinadorStatus, AlunoStatus,
│                         # VinculoStatus, ObjetivoTreino, DificuldadeTreino,
│                         # TipoGrupoMuscular, status de assinatura/pagamento, MetodoPagamento,
│                         # TierPlano (Free, Basic, Pro, ProPlus, Elite)
├── Events/               # IDomainEvent, IHasDomainEvents,
│                         # TreinadorAprovado/Reprovado/Inativado, VinculoAprovado,
│                         # AlunoRegistrado, AlunoAtualizado, AlunoInativado,
│                         # AssinaturaAlunoCriada
├── Exceptions/           # Exceções de domínio tipadas (DomainException base)
└── ValueObjects/         # Email

forzion.tech.Infrastructure/
├── DependencyInjection/
│   └── InfrastructureExtensions.cs
├── Migrations/           # EF Core migrations (21 total, schema homolog)
├── Handlers/             # Handlers de domain events em Infra: cria AssinaturaAluno no
│                         # VinculoAprovado; sincroniza projeção Assinante (billing)
├── Notifications/
│   ├── Email/            # EmailTemplates + 5 handlers de eventos de domínio (Resend)
│   └── WhatsApp/         # EvolutionApiWhatsAppNotifier + NullWhatsAppNotifier
├── Persistence/
│   ├── AppDbContext.cs   # DbContext + IUnitOfWork
│   ├── Configurations/   # Fluent API por entidade (18 arquivos)
│   ├── Repositories/     # Implementações concretas
│   └── Seeders/          # DataSeeder — conta admin no startup (Dev/Homolog)
└── Services/
    ├── JwtService.cs
    ├── BcryptPasswordHasher.cs
    ├── ResendEmailService.cs   # Envia via REST api.resend.com; ativo se Resend:ApiKey configurado
    ├── NullEmailService.cs     # No-op; usado sem chave configurada
    ├── StripeService.cs        # Stripe Connect: onboarding, Pix PaymentIntent, Card PaymentIntent, webhook
    └── DomainEventDispatcher.cs

forzion.tech.Tests/
├── Api/
│   ├── Endpoints/        # WebApplicationFactory: Admin, Aluno, Exercicio, Treino, Public
│   └── GlobalExceptionHandlerTests.cs
├── Application/          # Handlers (unit) por domínio
├── Domain/               # Entidades, value objects
├── Infrastructure/       # JwtService + email handlers + Repositories/ (Testcontainers.PostgreSql)
└── Integration/          # FluxoCompletoTests
```

---

### Modelo de Domínio

| Entidade | Descrição |
|----------|-----------|
| `Conta` | Auth unificada. E-mail + PasswordHash (BCrypt). `TipoConta`: `SystemAdmin`, `Treinador`, `Aluno`. |
| `SystemUser` | Perfil de admin vinculado a uma `Conta` do tipo `SystemAdmin`. |
| `Treinador` | Perfil de treinador. Possui `PlanoPlataformaId`. Status: `AguardandoAprovacao → Ativo → Inativo`. Dados de Stripe Connect foram extraídos para `ContaRecebimento` (contexto Billing). |
| `Aluno` | Perfil de aluno vinculado a uma `Conta`. Email armazenado como `Email` VO. Máquina de estados: `AguardandoAprovacao → Ativo ⇌ Inativo` via `Ativar()`/`Inativar()`. |
| `VinculoTreinadorAluno` | Relação entre treinador e aluno. Carrega `PacoteId`. Status: `AguardandoAprovacao → Ativo → Inativo`. |
| `PlanoPlataforma` | Plano global (gerido pelo admin; ex-`PlanoTreinador`). Define `Tier` (Free/Basic/Pro/ProPlus/Elite), `MaxAlunos` por treinador e `Descricao` opcional com as funcionalidades incluídas. |
| `Pacote` | Pacote criado pelo treinador (ex-`PacoteAluno`). Define nome, descrição e preço. Sem limite de fichas. |
| `Treino` | Ficha de treino com nome, objetivo, dificuldade e lista de `TreinoExercicio`. |
| `TreinoExercicio` | Item de exercício em uma ficha: configurações de série (`SerieConfig`), carga, descanso, ordem, observação. Referencia `Exercicio` por ID (sem nav prop — DDD). |
| `SerieConfig` | Configuração de série de um `TreinoExercicio` (qtd séries, reps mín/máx). Owned/value-style por ficha. |
| `TreinoAluno` | Vínculo ficha × aluno. Status: `Ativo / Inativo`. |
| `Exercicio` | Global (`TreinadorId = null`) ou privado do treinador. FK para `GrupoMuscular` (fonte da verdade). |
| `GrupoMuscular` | Grupo muscular global (gerido pelo admin) — entidade fonte da verdade, referenciada por FK em `Exercicio`. |
| `ExecucaoTreino` | Registro de sessão realizada pelo aluno. Contém lista de `ExecucaoExercicio`. |
| `LogAprovacao` | Auditoria de aprovações e inativações. `EntidadeId` sem FK — sobrevive a hard deletes. |
| `TokenRevogado` | JTI de tokens revogados (logout). Sem RLS. Limpo automaticamente ao expirar. |
| `ContaRecebimento` | Conta de recebimentos do treinador no Stripe Connect (`StripeConnectAccountId`, `OnboardingCompleto`). Extraída de `Treinador` (contexto Billing). |
| `AssinaturaAluno` | Cobrança recorrente mensal de um aluno (ex-`Assinatura`). Vinculada ao `VinculoTreinadorAluno` e ao `Pacote`. Status: `Pendente → Ativa → Inadimplente → Cancelada`. |
| `Assinante` | Projeção de billing read-side do aluno (nome/email/alunoId). Sincronizada por eventos de domínio (`AlunoRegistrado`/`AlunoAtualizado`). Unique por `AlunoId`. |
| `Pagamento` | Tentativa de cobrança individual. Armazena `StripePaymentIntentId`, QR Code Pix ou `ClientSecret` para cartão. Status: `Pendente → Pago / Expirado / Falhou`. |

---

### Domain Events

`Treinador`, `VinculoTreinadorAluno` e `Aluno` implementam `IHasDomainEvents` e disparam eventos em operações de negócio via `IDomainEventDispatcher` (sem reflection — interface genérica tipada):

| Evento | Levantado em | Consumido por |
|--------|-------------|---------------|
| `TreinadorAprovadoEvent` | `Treinador.Aprovar()` | e-mail (Resend) |
| `TreinadorReprovadoEvent` | `Treinador.Reprovar()` | e-mail |
| `TreinadorInativadoEvent` | `Treinador.Inativar()` | e-mail |
| `VinculoAprovadoEvent` | `VinculoTreinadorAluno.Aprovar()` | e-mail + cria `AssinaturaAluno` (se treinador com onboarding Stripe completo) |
| `AlunoInativadoEvent` | `Aluno.Inativar()` | — |
| `AlunoRegistradoEvent` | `Aluno.Criar()` | sincroniza projeção `Assinante` (billing) |
| `AlunoAtualizadoEvent` | `Aluno` (atualização de perfil) | sincroniza projeção `Assinante` |
| `AssinaturaAlunoCriadaEvent` | `AssinaturaAluno.Criar()` | e-mail |

Eventos são despachados **após** `SaveChangesAsync` e **antes** disso os eventos da entidade são limpos (snapshot + `ClearDomainEvents` antes do dispatch). Isso evita re-entrância: um handler que chama `CommitAsync` de novo (ex.: a projeção `Assinante`) não re-despacha os mesmos eventos. Handlers são in-process, resolvidos no mesmo escopo de DI (compartilham o `AppDbContext`).

---

### Autenticação e Autorização

O `JwtService` gera tokens HMAC-SHA256 com os claims:

| Claim | Conteúdo |
|-------|----------|
| `conta_id` | ID da `Conta` autenticada |
| `tipo_conta` | `SystemAdmin`, `Treinador` ou `Aluno` |
| `perfil_id` | ID do perfil (`SystemUser.Id`, `Treinador.Id` ou `Aluno.Id`) |
| `email` | E-mail da conta |
| `jti` | UUID único por token (usado para revogação) |
| `exp` | Expiração |

Políticas de autorização (`RequireAuthorization("NomeDaPolitica")`):

| Política | Claim exigido |
|----------|---------------|
| `SystemAdmin` | `tipo_conta = "SystemAdmin"` |
| `Treinador` | `tipo_conta = "Treinador"` |
| `Aluno` | `tipo_conta = "Aluno"` |
| *(sem política)* | Qualquer JWT válido não expirado e não revogado |

**Revogação de tokens**: `OnTokenValidated` consulta `tokens_revogados` a cada request autenticado. `LimparTokensRevogadosService` (BackgroundService) remove tokens expirados a cada hora.

**`PerfilIdRequiredFilter`**: garante que o claim `perfil_id` está presente — endpoints protegidos retornam 403 se ausente.

**`PaginacaoFilter`**: valida parâmetros de paginação em todos os grupos de endpoints. Retorna 400 com `detail` descritivo se `pagina < 1` ou `tamanhoPagina` fora de [1, 100].

---

### Rate Limiting

Fixed Window por IP:

| Política | Limite | Janela | Aplicado em |
|----------|--------|--------|-------------|
| `auth` | 10 req | 1 min | `/auth/login`, `/auth/register/*`, `/auth/planos`, `/auth/treinadores`, `/auth/treinadores/{id}/pacotes` |
| `write` | 60 req | 1 min | `/alunos/*`, `/treinos/*`, `/treinador/*`, `/aluno/*`, `/conta/*`, `/admin/*` |
| `read` | 120 req | 1 min | `/aluno/pagamentos/*` |
| `internal` | 5 req | 1 min | `/internal/processar-renovacoes` |
| `webhook` | 300 req | 1 min | `/webhooks/stripe` |

Exceder retorna **429 Too Many Requests**.

---

### Segurança (Backend)

| Mecanismo | Detalhe |
|-----------|---------|
| Senhas | BCrypt com salt automático |
| Tokens | HMAC-SHA256, expiração configurável, revogação por JTI |
| JWT secret | Mínimo 32 bytes validado no startup — falha explícita com instrução se fraco |
| Security headers | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `HSTS` (produção) |
| Isolamento | `TreinadorId` em todas as queries — sem multi-tenant, sem RLS |
| Validação de entrada | FluentValidation + `PaginacaoFilter` em todos os grupos |
| Erro sem vazamento | `GlobalExceptionHandler` nunca expõe stack trace ou mensagem interna (500 genérico) |
| Schema separado | `homolog` para dev/staging, `public` para produção |
| Stripe webhook | Verificação de assinatura `Stripe-Signature` com `StripeClient.ConstructEvent` antes de processar |
| Body size limit | `LimitedStream` limita payload do webhook a 64 KB — previne DoS por payload gigante |
| Open redirect | URLs de retorno do onboarding validadas contra domínio configurado (`Stripe:UrlBase`) |
| Idempotência | `stripe_payment_intent_id` UNIQUE em `pagamentos` — previne cobrança duplicada |
| Pagamento pendente | Partial unique index `status='Pendente'` — só um pagamento pendente por assinatura |
| Timing attack | Chave `X-Internal-Key` comparada com `CryptographicOperations.FixedTimeEquals` |

---

### Endpoints

#### Auth — `/auth` (público)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `POST` | `/auth/login` | `{ email, senha }` | `{ token, tipoConta, contaId, perfilId }` |
| `POST` | `/auth/register/treinador` | `{ nome, email, senha, telefone? }` | `201 TreinadorResponse` |
| `POST` | `/auth/register/aluno` | `{ nome, email, senha, treinadorId, pacoteId, telefone?, ... }` | `201 AlunoResponse` |
| `GET` | `/auth/planos` | — | `[PlanoPlataformaResponse]` · rate: `auth` |
| `GET` | `/auth/treinadores` | — | `[TreinadorPublicoResponse]` (apenas ativos) · rate: `auth` |
| `GET` | `/auth/treinadores/{id}/pacotes` | — | `[PacoteResponse]` · rate: `auth` |

#### Admin — `/admin` (política `SystemAdmin`)

Todos os endpoints paginados validam `pagina` e `tamanhoPagina` via `PaginacaoFilter`.

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `GET` | `/admin/treinadores` | `?status=&pagina=&tamanhoPagina=` | `ListarTreinadoresResponse` |
| `GET` | `/admin/treinadores/{id}` | — | `TreinadorResponse` |
| `POST` | `/admin/treinadores/{id}/aprovar` | `{ observacao? }` | `200 TreinadorResponse` |
| `POST` | `/admin/treinadores/{id}/reprovar` | `{ observacao? }` | `204` |
| `POST` | `/admin/treinadores/{id}/inativar` | `{ observacao? }` | `204` |
| `DELETE` | `/admin/treinadores/{id}` | — | `204` (só Inativo; hard delete em cascata) |
| `PATCH` | `/admin/treinadores/{id}/plano` | `{ planoId }` | `200 TreinadorResponse` |
| `GET` | `/admin/planos` | — | `[PlanoPlataformaResponse]` |
| `POST` | `/admin/planos` | `{ nome, tier, maxAlunos, preco, descricao? }` | `201 PlanoPlataformaResponse` |
| `PATCH` | `/admin/planos/{id}` | `{ nome?, tier?, maxAlunos?, preco?, descricao? }` | `200 PlanoPlataformaResponse` |
| `DELETE` | `/admin/planos/{id}` | — | `204` |
| `GET` | `/admin/grupos-musculares` | — | `[GrupoMuscularResponse]` |
| `POST` | `/admin/grupos-musculares` | `{ nome }` | `201 GrupoMuscularResponse` |
| `PATCH` | `/admin/grupos-musculares/{id}` | `{ nome }` | `200 GrupoMuscularResponse` |
| `DELETE` | `/admin/grupos-musculares/{id}` | — | `204` |
| `GET` | `/admin/exercicios` | `?grupoMuscularId=&pagina=&tamanhoPagina=` | `[ExercicioResponse]` |
| `POST` | `/admin/exercicios` | `{ nome, descricao?, grupoMuscularId }` | `201 ExercicioResponse` |
| `PATCH` | `/admin/exercicios/{id}` | `{ nome?, descricao?, grupoMuscularId? }` | `200 ExercicioResponse` |
| `DELETE` | `/admin/exercicios/{id}` | — | `204` |
| **Visibilidade (read-only)** | | | |
| `GET` | `/admin/alunos` | `?nome=&status=&pagina=&tamanhoPagina=` | `PaginatedResponse<AlunoResponse>` |
| `GET` | `/admin/alunos/{id}` | — | `AlunoResponse` |
| `GET` | `/admin/alunos/{id}/vinculo` | — | `MeuVinculoResponse` (ativo + pendente) |
| `GET` | `/admin/alunos/{id}/fichas` | `?pagina=&tamanhoPagina=` | `PaginatedResponse<FichaAlunoResponse>` |
| `GET` | `/admin/fichas/{treinoAlunoId}` | — | `FichaAlunoResponse` (detalhe com exercícios) |
| `GET` | `/admin/alunos/{id}/execucoes` | `?pagina=&tamanhoPagina=` | `PaginatedResponse<ExecucaoTreinoResponse>` |
| `GET` | `/admin/alunos/{id}/progressao` | `?de=&ate=` | `ProgressaoAlunoResponse` |
| `GET` | `/admin/treinadores/{id}/alunos` | `?status=&pagina=&tamanhoPagina=` | `PaginatedResponse<AlunoResponse>` |
| `GET` | `/admin/treinadores/{id}/vinculos` | `?status=` | `PaginatedResponse<VinculoDetalheResponse>` |
| `GET` | `/admin/treinadores/{id}/treinos` | `?nome=&objetivo=&pagina=&tamanhoPagina=` | `PaginatedResponse<TreinoResponse>` |
| `GET` | `/admin/treinos/{id}` | — | `TreinoResponse` |
| `GET` | `/admin/treinadores/{id}/pacotes` | — | `PacoteResponse[]` |

#### Treinador — `/treinador` (política `Treinador`)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `GET` | `/treinador/vinculos` | `?status=&pagina=&tamanhoPagina=` | `ListarVinculosResponse` |
| `POST` | `/treinador/vinculos/{id}/aprovar` | `{ pacoteId, trarFichas? }` | `200 VinculoResponse` |
| `POST` | `/treinador/vinculos/{id}/desvincular` | `{ observacao? }` | `204` |
| `POST` | `/treinador/alunos/{alunoId}/reativar` | `{ pacoteId }` | `200 VinculoResponse` |
| `GET` | `/treinador/alunos` | `?pagina=&tamanhoPagina=` | `ListarAlunosResponse` |
| `GET` | `/treinador/alunos/{alunoId}` | — | `AlunoDetalheResponse` |
| `PATCH` | `/treinador/alunos/{alunoId}` | `{ nome?, telefone?, ... }` | `200 AlunoResponse` |
| `GET` | `/treinador/treinos` | `?pagina=&tamanhoPagina=&objetivo=` | `ListarTreinosResponse` |
| `GET` | `/treinador/exercicios` | `?pagina=&tamanhoPagina=` | `[ExercicioResponse]` |
| `POST` | `/treinador/exercicios` | `{ nome, descricao?, grupoMuscularId }` | `201 ExercicioResponse` |
| `PATCH` | `/treinador/exercicios/{id}` | `{ nome?, descricao?, grupoMuscularId? }` | `200 ExercicioResponse` |
| `DELETE` | `/treinador/exercicios/{id}` | — | `204` |
| `POST` | `/treinador/exercicios/{id}/copiar` | — | `201 ExercicioResponse` |
| `GET` | `/treinador/pacotes` | — | `[PacoteResponse]` |
| `POST` | `/treinador/pacotes` | `{ nome, descricao?, preco }` | `201 PacoteResponse` |
| `PATCH` | `/treinador/pacotes/{id}` | `{ nome?, descricao?, preco? }` | `200 PacoteResponse` |
| **Stripe** | | | |
| `POST` | `/treinador/onboarding` | `{ urlRetorno, urlCancelamento }` | `200 { url }` |
| `GET` | `/treinador/onboarding/status` | — | `OnboardingStatusResponse` |
| `POST` | `/treinador/pagamentos/cobrar/{assinaturaId}` | `?metodo=Pix\|Cartao` | `200 PagamentoResponse` · rate: `write` |

#### Pagamentos Aluno — `/aluno/pagamentos` (política `Aluno`)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `GET` | `/aluno/pagamentos/{pagamentoId}` | — | `PagamentoResponse` (inclui `clientSecret` para cartão) · rate: `read` |
| `GET` | `/aluno/pagamentos/assinatura/{assinaturaId}` | — | `PagamentoResponse[]` · rate: `read` |

#### Webhooks (público — verificação por assinatura Stripe)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `POST` | `/webhooks/stripe` | Evento Stripe | `200` · body limitado a 64 KB · rate: `webhook` |

#### Internal (autenticação por `X-Internal-Key`)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `POST` | `/internal/processar-renovacoes` | — | `{ processadas, falhas }` · rate: `internal` |

#### Treinos — `/treinos` (JWT obrigatório)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `POST` | `/treinos` | `{ nome, objetivo, dificuldade?, exercicios[] }` | `201 TreinoResponse` |
| `GET` | `/treinos/{id}` | — | `TreinoResponse` |
| `PATCH` | `/treinos/{id}` | `{ nome?, objetivo?, dificuldade? }` | `200 TreinoResponse` |
| `DELETE` | `/treinos/{id}` | — | `204` (proibido se já executado) |
| `GET` | `/treinos/{id}/alunos` | — | alunos vinculados à ficha |
| `POST` | `/treinos/{id}/vincular-aluno` | `{ alunoId }` | `201 TreinoAlunoResponse` |
| `POST` | `/treinos/{id}/exercicios` | `{ exercicioId, series, repeticoes, ... }` | `201` |
| `PUT` | `/treinos/{id}/exercicios/{treinoExercicioId}` | `{ series?, repeticoes?, ... }` | `200` |
| `PATCH` | `/treinos/{id}/exercicios/{treinoExercicioId}/observacao` | `{ observacao }` | `200` |
| `DELETE` | `/treinos/{id}/exercicios/{treinoExercicioId}` | — | `204` (proibido se já executado) |
| `POST` | `/treinos/{id}/duplicar` | `{ novoNome? }` | `201 TreinoResponse` |
| `POST` | `/treinos/{id}/execucoes` | `{ dataExecucao, observacao?, exercicios[] }` | `201 ExecucaoTreinoResponse` |

#### Alunos — `/alunos` (JWT obrigatório)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `GET` | `/alunos` | `?pagina=&tamanhoPagina=` | `ListarAlunosResponse` |
| `GET` | `/alunos/{id}` | — | `AlunoResponse` |
| `GET` | `/alunos/{id}/treinos` | — | `[TreinoAlunoResponse]` |
| `PATCH` | `/alunos/{id}` | `{ nome?, telefone?, ... }` | `200 AlunoResponse` |
| `PATCH` | `/alunos/{id}/status` | `{ status }` | `200 AlunoResponse` (SystemAdmin only) |

#### Área do Aluno — `/aluno` (política `Aluno`)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `GET` | `/aluno/fichas` | — | `[TreinoAlunoResponse]` |
| `GET` | `/aluno/fichas/{treinoAlunoId}` | — | `TreinoResponse` (com exercícios) |
| `GET` | `/aluno/execucoes` | `?pagina=&tamanhoPagina=` | `ListarExecucoesResponse` |
| `POST` | `/aluno/execucoes` | `{ treinoId, dataExecucao, observacao?, exercicios[] }` | `201` |
| `GET` | `/aluno/progressao` | `?de=&ate=` | `ProgressaoAlunoResponse` |
| `GET` | `/aluno/vinculo` | — | `MeuVinculoResponse` (ativo + pendente) |
| `GET` | `/aluno/assinatura` | — | `AssinaturaAlunoResponse` (assinatura ativa do aluno) |
| `POST` | `/aluno/troca-treinador` | `{ novoTreinadorId, trarFichas }` | `201 VinculoResponse` |

#### Conta — `/conta` (JWT obrigatório)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `GET` | `/conta/perfil` | — | `PerfilResponse` |
| `PATCH` | `/conta/perfil` | `{ nome }` | `204` |
| `POST` | `/conta/senha` | `{ senhaAtual, novaSenha }` | `204` |
| `POST` | `/conta/logout` | — | `204` (revoga JTI) |

#### Exercícios — `/exercicios` (JWT obrigatório)

Endpoint genérico acessível a qualquer JWT válido (sem policy `Treinador`). Subconjunto de `/treinador/exercicios` — sem PATCH, DELETE nem copiar.

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `GET` | `/exercicios` | `?pagina=&tamanhoPagina=` | `ListarExerciciosResponse` |
| `POST` | `/exercicios` | `{ nome, grupoMuscular, descricao? }` | `201 ExercicioResponse` |

#### Infra

| Método | Rota | Auth | Resposta |
|--------|------|------|----------|
| `GET` | `/health` | nenhuma | `200 Healthy` |

---

### Regras de Negócio

#### Fluxo de Aprovação

```
Treinador:  Cadastro → AguardandoAprovacao → (admin aprova) → Ativo
                                           → (admin reprova) → sem perfil (conta permanece)
                                           → (admin inativa) → Inativo

Aluno+Vínculo: Cadastro → AguardandoAprovacao → (treinador aprova vínculo) → Ativo
                                              → (treinador desvíncula) → Inativo
```

Toda aprovação, reprovação e inativação registra um `LogAprovacao` com `EntidadeId` sem FK — sobrevive a hard deletes.

#### Limites de Plano

| Serviço | O que valida | Quando | Exceção |
|---------|-------------|--------|---------|
| `LimiteTreinadorService` | `PlanoPlataforma.MaxAlunos` vs alunos ativos do treinador | Ao aprovar vínculo | `LimiteAlunosAtingidoException` → 422 |

`Pacote` não tem mais limite de fichas — campo `MaxFichas` foi removido. Controle é feito via `Descricao` livre.

#### Cascata de Inativação

| Ação | Efeito em cascata |
|------|-------------------|
| Inativar `Treinador` | Inativa todos os `VinculoTreinadorAluno` ativos → inativa todos os `TreinoAluno` dos pares afetados |
| Desvincular `VinculoTreinadorAluno` | Inativa todos os `TreinoAluno` do par (treinador × aluno) |

#### Hard Delete de Treinador

Só permitido se `Treinador.Status == Inativo`. Remove em cascata (transação única):

1. `ExecucoesTreino` dos treinos do treinador
2. `TreinoAlunos` dos treinos do treinador
3. `TreinoExercicios` (cascade do `Treino`)
4. `Treinos` do treinador
5. `Exercicios` do treinador
6. `PacotesAluno` do treinador
7. `VinculosTreinadorAluno` do treinador
8. `Treinador`
9. `Conta`

`LogAprovacao` é preservado (sem FK).

#### Troca de Treinador

1. Aluno solicita troca via `POST /aluno/troca-treinador`
2. Vínculo atual é inativado; novo vínculo criado com `AguardandoAprovacao`
3. Se `trarFichas = true`, fichas ativas do aluno são migradas para o novo treinador
4. Novo treinador aprova via `POST /treinador/vinculos/{id}/aprovar`

#### Isolamento de Dados

Sem `TenantId`. Isolamento por `TreinadorId`. Handlers validam `IUserContext.PerfilId` contra o dono do recurso — `AcessoNegadoException` → 403.

---

### Tratamento de Erros

O `GlobalExceptionHandler` implementa **RFC 7807** (`ProblemDetails`):

| Exceção | Status | Cenário |
|---------|--------|---------|
| `CredenciaisInvalidasException` | 401 | Login com senha errada |
| `*NaoEncontradoException` | 404 | Recurso não existe ou não pertence ao usuário |
| `AcessoNegadoException` | 403 | Tentativa de acesso a recurso de outro treinador |
| `*InativoException` | 403 | Operação em entidade inativa |
| `DomainException` (e subclasses) | 422 | Violação de regra de negócio |
| `ValidationException` (FluentValidation) | 400 | Payload inválido — inclui erros por campo |
| `Qualquer outra` | 500 | Mensagem interna nunca exposta |

Exemplo de resposta 422:

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

```jsonc
// appsettings.json — base versionado (sem segredos)
{
  "Auth": {
    "JwtSecret": "",       // via user secrets ou variável de ambiente
    "JwtIssuer": "",
    "JwtAudience": ""
  },
  "ConnectionStrings": {
    "AppConnection": ""    // via user secrets ou variável de ambiente
  },
  "Database": {
    "Schema": "homolog"    // "public" em produção
  },
  "Stripe": {
    "SecretKey": "",       // sk_live_... ou sk_test_... — via user secrets
    "WebhookSecret": "",   // whsec_... — via user secrets
    "PublishableKey": "",  // pk_live_... ou pk_test_...
    "TaxaPlataformaPercent": "10",  // % retida pela plataforma em cada cobrança
    "UrlBase": ""          // domínio permitido para URLs de retorno do onboarding
  }
}
```

```bash
# Configurar via User Secrets (desenvolvimento local)
dotnet user-secrets set "Auth:JwtSecret"              "<chave-hmac-min-32bytes>" --project forzion.tech.Api
# Gerar chave com entropia adequada:
# openssl rand -base64 64
dotnet user-secrets set "Auth:JwtIssuer"              "forzion.tech"         --project forzion.tech.Api
dotnet user-secrets set "Auth:JwtAudience"            "forzion.tech"         --project forzion.tech.Api
dotnet user-secrets set "ConnectionStrings:AppConnection" "<conn-string>"    --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminEmail"             "admin@forzion.tech"   --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminPassword"          "<senha>"              --project forzion.tech.Api

# Opcional — e-mail transacional via Resend (omitir = NullEmailService)
dotnet user-secrets set "Resend:ApiKey"               "re_..."               --project forzion.tech.Api

# Opcional — notificações WhatsApp via Evolution API (omitir = NullWhatsAppNotifier)
dotnet user-secrets set "WhatsApp:BaseUrl"            "https://..."          --project forzion.tech.Api
dotnet user-secrets set "WhatsApp:Instance"           "<instance>"           --project forzion.tech.Api
dotnet user-secrets set "WhatsApp:ApiKey"             "<apikey>"             --project forzion.tech.Api

# Stripe (necessário para módulo de pagamentos)
dotnet user-secrets set "Stripe:SecretKey"            "sk_test_..."          --project forzion.tech.Api
dotnet user-secrets set "Stripe:WebhookSecret"        "whsec_..."            --project forzion.tech.Api
dotnet user-secrets set "Stripe:PublishableKey"       "pk_test_..."          --project forzion.tech.Api
dotnet user-secrets set "Stripe:TaxaPlataformaPercent" "10"                  --project forzion.tech.Api
dotnet user-secrets set "Stripe:UrlBase"              "https://localhost:3000" --project forzion.tech.Api
```

User Secrets ID: `049d65fb-2c12-483c-b56e-cb753632d11f`

| Ambiente | Schema | Swagger | Seeder | Modo |
|----------|--------|---------|--------|------|
| `Development` | `homolog` | ✅ | ✅ | `dotnet run` |
| `Homolog` | `homolog` | ✅ | ✅ | `ASPNETCORE_ENVIRONMENT=Homolog dotnet run` |
| `Production` | `public` | ❌ | ❌ | Container Docker |
| `Test` | mock / em memória | ❌ | ❌ | `dotnet test` |

---

### Migrations

| Migration | O que faz |
|-----------|-----------|
| `InicioDominio` | Schema completo inicial — todas as tabelas e constraints |
| `AdicionarGruposMusculares` | Tabela `grupos_musculares` + FK em `exercicios` |
| `UniqueConstraintTreinoAlunoPorFicha` | Unique constraint em `treino_alunos (treino_id, aluno_id)` |
| `RemoverMaxFichasAdicionarDescricaoPacote` | Remove `max_fichas`; adiciona `descricao` (nullable, max 500) em `pacotes_aluno` |
| `AdicionarDadosFisicosAluno` | Campos físicos em `alunos` (peso, altura, etc.) e dados de perfil |
| `SeriesConfiguraveisExercicio` | Torna `series` e `repeticoes` em `treino_exercicios` configuráveis |
| `AdicionarDificuldadeEDatasNaTreino` | Coluna `dificuldade` + datas em `treinos` |
| `AdicionarObservacaoTreinoExercicio` | Coluna `observacao` em `treino_exercicios` |
| `AdicionarTelefoneNaTabelaTreinadores` | Coluna `telefone` em `treinadores` |
| `AdicionarTokenRevogado` | Tabela `tokens_revogados (jti PK, expira_em)` + índice em `expira_em` |
| `AdicionarIndicesPerformance` | Índices: `treinadores(status)`, `vinculos_treinador_aluno(treinador_id, status)` |
| `AdicionarPagamentos` | Colunas Stripe em `treinadores`; tabelas `assinaturas` e `pagamentos` com índices de performance |
| `SegurancaPagamentos` | Partial unique index `pagamentos(assinatura_id)` onde `status='Pendente'`; unique em `stripe_payment_intent_id` |
| `CartaoPagamento` | Colunas `client_secret varchar(500)` e `metodo_pagamento text DEFAULT 'Pix'` em `pagamentos` |
| `AdicionarTierPlanoTreinador` | Coluna `tier varchar(20) NOT NULL` em `planos_treinador`; seed popula Free/Basic/Pro/ProPlus/Elite |
| `AdicionarDescricaoPlanoTreinador` | Coluna `descricao varchar(200)` nullable em `planos_treinador` com descrição das funcionalidades por tier |
| `AdicionarIndiceAlunoStatus` | Índice parcial em `alunos(status)` para acelerar filtros por status |
| `AddAssinanteBillingProjection` | Tabela `assinantes` (projeção billing read-side) com unique em `aluno_id` |
| `MoverStripeParaContaRecebimento` | Extrai dados de Stripe Connect de `treinadores` para a nova tabela `contas_recebimento` (contexto Billing) |
| `ExercicioGrupoMuscularFk` | FK `grupo_muscular_id` em `exercicios` + backfill a partir do nome do enum (data-preserving) |
| `RenomearPlanoPacoteAssinatura` | Rename data-preserving: `planos_treinador → planos_plataforma`, `pacotes_aluno → pacotes`, `assinaturas → assinaturas_aluno` (linguagem plataforma/aluno) |

> **Migrations fixam o schema `homolog`** (ToTable/SQL com schema hardcoded). Rodar `MigrateAsync` exige o mesmo schema no `DbContext` (`Database:Schema=homolog`); a 1ª migration é schema-agnostic e usa `current_schema()`, então o schema precisa existir no `search_path` antes.

---

### Testes

```
1067 testes | 0 falhas  (999 unit/rápidos + 68 integração com Docker)

Domain/                  → entidades, value objects, domain events, exceções, máquina de estados
Domain/Properties/       → property-based (CsCheck): Email VO, Result<T>, invariantes de entidade
Application/             → handlers (unit), services de limite, lógica temporal (FakeTimeProvider)
Architecture/            → arch tests (NetArchTest): direção de dependência entre camadas, convenções
Api/Snapshots/           → snapshot/contract de saída (Verify.Xunit): response DTOs + mapa exceção→ProblemDetails
Api/Endpoints/           → endpoints via WebApplicationFactory (auth, status codes, isolamento, paginação)
Builders/                → test data builders determinísticos
Infrastructure/          → JwtService, email handlers, dispatch de domain events (regressão de re-entrância)
Infrastructure/Repositories/ → 62 testes de repositório com Testcontainers.PostgreSql (banco real)  [Integration]
E2E/                     → pipeline real: WebApplicationFactory + Postgres real + migrations + seed,
                           handlers reais, só Stripe fake  [Integration]
```

**Determinismo**: `TimeProvider` (BCL .NET 8) injetado no domínio; testes usam `FakeTimeProvider`. Sem `DateTime.UtcNow` nas factories.

**Split unit vs integração**: testes que precisam de Docker (Testcontainers) são marcados `[Trait("Category","Integration")]`. O CI roda dois jobs: `test-backend-unit` (`--filter "Category!=Integration"`, sem Docker, rápido) e `test-backend-integration` (suíte completa com Docker). Gates de cobertura: Domain/Application branch 75 + line/method 85 e Api line 85/method 70 no job unit; global 50 + Infra 35 no job de integração.

**Outras fases do harness em CI**: mutation testing (Stryker), endurecimento de cobertura (line/method + ReportGenerator), drift de contrato OpenAPI (`docs/api/swagger.v1.json`), supply-chain NuGet (vuln + SBOM + Renovate), pre-commit backend, Pact provider verification.

Comandos úteis:

```bash
dotnet test forzion.tech.Tests --filter "Category!=Integration"   # rápido, sem Docker (999)
dotnet test forzion.tech.Tests                                    # suíte inteira, exige Docker (1067)
```

Padrões adotados:

- `HandleAsync` declarado como `virtual` em todos os handlers para mock via Moq
- `ArgumentNullException.ThrowIfNull(command)` em todo handler que recebe command
- `It.IsAny<CancellationToken>()` em todos os setups de repositório
- Auth em testes de endpoint: `TestAuthHandler` substitui JWT
- Handlers mockados via `RemoveAll + AddSingleton` no `WebApplicationFactory`
- `TreinadorId` injetado via reflection quando necessário
- Testcontainers: `InfrastructureTestFixture` + `[Collection(InfrastructureTestCollection.Name)]`. `CreateContext()` requer `.UseSnakeCaseNamingConvention()` — sem ele, índices parciais `HasFilter("status = 'Ativo'")` falham com `42703`
- `VinculoTreinadorAluno.Aprovar(treinadorId, pacoteId)` exige `PacoteId` real no banco — sempre usar `SeedPacoteAsync` nos testes de repositório
- `BeInAscendingOrder` não aceita method calls — enums armazenados como string ordenam alfabeticamente: usar `.Select(e => e.Prop.ToString()).Should().BeInAscendingOrder()`

---

## Frontend

Ver [`frontend/README.md`](frontend/README.md) para detalhes completos.

**Stack resumida**: Next.js 16 · React 19 · MUI v9 · TypeScript · React Hook Form + Zod · Axios · Zustand

```bash
cd frontend
npm install
npm run dev     # http://localhost:3000
npm test        # Vitest (unit + integration + api projects)
npm run e2e     # Playwright (pós-deploy, contra homolog)
npm run build   # build de produção (standalone)
```

---

## Deploy

### Docker local

```bash
# Copiar e preencher variáveis (JWT_SECRET etc.)
cp .env.example .env

# Subir backend + frontend + PostgreSQL local
docker compose up --build

# Backend:  http://localhost:8080  |  Frontend: http://localhost:3001
# Swagger:  http://localhost:8080/swagger  (modo Development)
```

### Produção — OCI VM + Supabase

A stack de produção usa:

- **OCI VM** (Ubuntu) com Docker Compose
- **Supabase** como banco (PostgreSQL managed) — schema `public`
- **Nginx** como reverse proxy com TLS (Let's Encrypt via Certbot)
- **OCIR** como container registry

#### 1. Preparar a VM (executar uma vez)

```bash
ssh ubuntu@<IP> 'bash -s' < scripts/setup-vm.sh
```

Instala Docker, cria estrutura de diretórios em `/opt/forzion/` e gera `.env` template.

#### 2. Preencher `/opt/forzion/.env` na VM

```bash
REGISTRY=<regiao>.ocir.io/<namespace>
TAG=latest
APP_ENV=Homolog                  # ou Production
DB_CONNECTION=Host=...           # Supabase connection string
DB_SCHEMA=homolog                # ou public
JWT_SECRET=<minimo-32-chars>
JWT_ISSUER=forzion.tech
JWT_AUDIENCE=forzion.tech
CORS_ORIGINS=https://homolog.forzion.tech
```

#### 3. Configurar DNS

Apontar `homolog.forzion.tech` (ou `forzion.tech`) para o IP da VM.

#### 4. Obter certificado SSL (executar uma vez após DNS propagado)

```bash
bash scripts/init-ssl.sh homolog.forzion.tech seu@email.com
```

#### 5. Build e push das imagens

```bash
# Backend
docker build -t $REGISTRY/forzion/backend:$TAG -f forzion.tech.Api/Dockerfile .
docker push $REGISTRY/forzion/backend:$TAG

# Frontend
docker build -t $REGISTRY/forzion/frontend:$TAG -f frontend/Dockerfile ./frontend
docker push $REGISTRY/forzion/frontend:$TAG
```

#### 6. Deploy

```bash
# Na VM
cd /opt/forzion
docker compose -f docker-compose.server.yml pull
docker compose -f docker-compose.server.yml up -d
```

#### 7. Migrations em produção

```bash
# Gerar script idempotente (localmente)
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef migrations script --idempotent \
  --project forzion.tech.Infrastructure \
  --startup-project forzion.tech.Api \
  --output migration_public_schema.sql

# Substituir schema: sed 's/"homolog"/"public"/g'
# Aplicar via Supabase SQL Editor (conta com permissão de DDL)
```

#### Arquitetura de produção

```
Cliente
  └── Nginx (80/443, TLS Let's Encrypt)
        ├── /api/backend/* → backend:8080
        └── /* → frontend:3000
              └── /api/backend/* → backend:8080  (proxy server-side Next.js)
                    └── PostgreSQL Supabase (schema public)
```
