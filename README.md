# forzion.tech

Plataforma de gestão de treinos para personal trainers e alunos.

**Backend**: ASP.NET Core 8.0 · **Frontend**: Next.js 16 + MUI v9 · **Banco**: PostgreSQL (Supabase)

**Status**: ✅ 597 testes backend + 142 testes frontend | Clean Architecture | JWT próprio | Isolamento por TreinadorId | Auditoria de segurança OWASP | DDD tático aplicado

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
└── .env.example               # Variáveis necessárias para docker-compose
```

---

## Setup Local

### Opção A — Docker Compose (mais rápido)

```bash
# 1. Copiar e preencher variáveis
cp .env.example .env
# editar .env com JWT_SECRET etc.

# 2. Subir tudo (backend + frontend + postgres local)
docker compose up --build

# Backend: http://localhost:8080
# Frontend: http://localhost:3000
# Swagger: http://localhost:8080/swagger
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

> No primeiro start em Development/Homolog, o `DataSeeder` cria automaticamente a conta admin definida em `Seed:AdminEmail` + `Seed:AdminPassword`.

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
│   ├── Exercicios/       # /exercicios
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
    ├── Auth/
    ├── Conta/
    ├── Exercicios/
    ├── Pacotes/
    ├── Planos/
    ├── Treinadores/
    ├── Treinos/
    └── Vinculos/

forzion.tech.Domain/
├── Entities/             # Conta, Treinador, Aluno, VinculoTreinadorAluno,
│                         # Treino, TreinoExercicio, TreinoAluno, Exercicio,
│                         # ExecucaoTreino, ExecucaoExercicio, LogAprovacao,
│                         # PlanoTreinador, PacoteAluno, GrupoMuscular,
│                         # SystemUser, TokenRevogado
├── Enums/                # TipoConta, TreinadorStatus, AlunoStatus, VinculoStatus,
│                         # ObjetivoTreino, DificuldadeTreino, GrupoMuscularEnum
├── Events/               # IDomainEvent, IHasDomainEvents,
│                         # TreinadorAprovadoEvent, TreinadorReprovadoEvent,
│                         # TreinadorInativadoEvent, VinculoAprovadoEvent,
│                         # AlunoInativadoEvent
├── Exceptions/           # Exceções de domínio tipadas (DomainException base)
└── ValueObjects/         # Email

forzion.tech.Infrastructure/
├── DependencyInjection/
│   └── InfrastructureExtensions.cs
├── Migrations/           # EF Core migrations (11 total)
├── Notifications/
│   ├── Email/            # EmailTemplates + 4 handlers de eventos de domínio (Resend)
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
    └── DomainEventDispatcher.cs

forzion.tech.Tests/
├── Api/
│   ├── Endpoints/        # WebApplicationFactory: Admin, Aluno, Exercicio, Treino, Public
│   └── GlobalExceptionHandlerTests.cs
├── Application/          # Handlers (unit) por domínio
├── Domain/               # Entidades, value objects
├── Infrastructure/       # JwtService
└── Integration/          # FluxoCompletoTests
```

---

### Modelo de Domínio

| Entidade | Descrição |
|----------|-----------|
| `Conta` | Auth unificada. E-mail + PasswordHash (BCrypt). `TipoConta`: `SystemAdmin`, `Treinador`, `Aluno`. |
| `SystemUser` | Perfil de admin vinculado a uma `Conta` do tipo `SystemAdmin`. |
| `Treinador` | Perfil de treinador. Possui `PlanoTreinadorId`. Status: `AguardandoAprovacao → Ativo → Inativo`. |
| `Aluno` | Perfil de aluno vinculado a uma `Conta`. Email armazenado como `Email` VO. Máquina de estados: `AguardandoAprovacao → Ativo ⇌ Inativo` via `Ativar()`/`Inativar()`. |
| `VinculoTreinadorAluno` | Relação entre treinador e aluno. Carrega `PacoteAlunoId`. Status: `AguardandoAprovacao → Ativo → Inativo`. |
| `PlanoTreinador` | Plano global (gerido pelo admin). Define `MaxAlunos` por treinador. |
| `PacoteAluno` | Pacote criado pelo treinador. Define nome, descrição e preço. Sem limite de fichas. |
| `Treino` | Ficha de treino com nome, objetivo, dificuldade e lista de `TreinoExercicio`. |
| `TreinoExercicio` | Item de exercício em uma ficha: séries, repetições, carga, descanso, ordem, observação. Referencia `Exercicio` por ID (sem nav prop — DDD). |
| `TreinoAluno` | Vínculo ficha × aluno. Status: `Ativo / Inativo`. |
| `Exercicio` | Global (`TreinadorId = null`) ou privado do treinador. Possui `GrupoMuscular`. |
| `GrupoMuscular` | Grupo muscular global (gerido pelo admin). |
| `ExecucaoTreino` | Registro de sessão realizada pelo aluno. Contém lista de `ExecucaoExercicio`. |
| `LogAprovacao` | Auditoria de aprovações e inativações. `EntidadeId` sem FK — sobrevive a hard deletes. |
| `TokenRevogado` | JTI de tokens revogados (logout). Sem RLS. Limpo automaticamente ao expirar. |

---

### Domain Events

`Treinador`, `VinculoTreinadorAluno` e `Aluno` implementam `IHasDomainEvents` e disparam eventos em operações de negócio via `IDomainEventDispatcher` (sem reflection — interface genérica tipada):

| Evento | Levantado em |
|--------|-------------|
| `TreinadorAprovadoEvent` | `Treinador.Aprovar()` |
| `TreinadorReprovadoEvent` | `Treinador.Reprovar()` |
| `TreinadorInativadoEvent` | `Treinador.Inativar()` |
| `VinculoAprovadoEvent` | `VinculoTreinadorAluno.Aprovar()` |
| `AlunoInativadoEvent` | `Aluno.Inativar()` |

Eventos são despachados sem persistência — handlers de notificação os consomem in-process.

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

---

### Endpoints

#### Auth — `/auth` (público)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `POST` | `/auth/login` | `{ email, senha }` | `{ token, tipoConta, contaId, perfilId }` |
| `POST` | `/auth/register/treinador` | `{ nome, email, senha, telefone?, planoId? }` | `201 { treinadorId }` |
| `POST` | `/auth/register/aluno` | `{ nome, email, senha, treinadorId, pacoteId?, ... }` | `201 { alunoId, vinculoId }` |
| `GET` | `/auth/planos` | — | `[PlanoTreinadorResponse]` · rate: `auth` |
| `GET` | `/auth/treinadores` | — | `[TreinadorPublicoResponse]` (apenas ativos) · rate: `auth` |
| `GET` | `/auth/treinadores/{id}/pacotes` | — | `[PacoteAlunoResponse]` · rate: `auth` |

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
| `GET` | `/admin/planos` | — | `[PlanoTreinadorResponse]` |
| `POST` | `/admin/planos` | `{ nome, maxAlunos, preco, ativo }` | `201 PlanoTreinadorResponse` |
| `PATCH` | `/admin/planos/{id}` | `{ nome?, maxAlunos?, preco?, ativo? }` | `200 PlanoTreinadorResponse` |
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
| `GET` | `/admin/treinadores/{id}/pacotes` | — | `PacoteAlunoResponse[]` |

#### Treinador — `/treinador` (política `Treinador`)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `GET` | `/treinador/vinculos` | `?status=&pagina=&tamanhoPagina=` | `ListarVinculosResponse` |
| `POST` | `/treinador/vinculos/{id}/aprovar` | `{ pacoteAlunoId? }` | `200 VinculoResponse` |
| `POST` | `/treinador/vinculos/{id}/desvincular` | `{ observacao? }` | `204` |
| `POST` | `/treinador/vinculos/{id}/reativar` | — | `200 VinculoResponse` |
| `GET` | `/treinador/alunos` | `?pagina=&tamanhoPagina=` | `ListarAlunosResponse` |
| `GET` | `/treinador/alunos/{alunoId}` | — | `AlunoDetalheResponse` |
| `PATCH` | `/treinador/alunos/{alunoId}` | `{ nome?, telefone?, ... }` | `200 AlunoResponse` |
| `GET` | `/treinador/treinos` | `?pagina=&tamanhoPagina=&objetivo=` | `ListarTreinosResponse` |
| `GET` | `/treinador/exercicios` | `?pagina=&tamanhoPagina=` | `[ExercicioResponse]` |
| `POST` | `/treinador/exercicios` | `{ nome, descricao?, grupoMuscularId }` | `201 ExercicioResponse` |
| `PATCH` | `/treinador/exercicios/{id}` | `{ nome?, descricao?, grupoMuscularId? }` | `200 ExercicioResponse` |
| `DELETE` | `/treinador/exercicios/{id}` | — | `204` |
| `POST` | `/treinador/exercicios/{id}/copiar` | — | `201 ExercicioResponse` |
| `GET` | `/treinador/pacotes` | — | `[PacoteAlunoResponse]` |
| `POST` | `/treinador/pacotes` | `{ nome, descricao?, preco }` | `201 PacoteAlunoResponse` |
| `PATCH` | `/treinador/pacotes/{id}` | `{ nome?, descricao?, preco? }` | `200 PacoteAlunoResponse` |

#### Treinos — `/treinos` (JWT obrigatório)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `POST` | `/treinos` | `{ nome, objetivo, dificuldade?, exercicios[] }` | `201 TreinoResponse` |
| `GET` | `/treinos/{id}` | — | `TreinoResponse` |
| `PATCH` | `/treinos/{id}` | `{ nome?, objetivo?, dificuldade? }` | `200 TreinoResponse` |
| `DELETE` | `/treinos/{id}` | — | `204` (proibido se já executado) |
| `POST` | `/treinos/{id}/vincular-aluno` | `{ alunoId }` | `201 TreinoAlunoResponse` |
| `POST` | `/treinos/{id}/exercicios` | `{ exercicioId, series, repeticoes, ... }` | `201` |
| `PATCH` | `/treinos/{id}/exercicios/{exercicioId}` | `{ series?, repeticoes?, ... }` | `200` |
| `DELETE` | `/treinos/{id}/exercicios/{exercicioId}` | — | `204` (proibido se já executado) |
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
| `GET` | `/aluno/fichas/{id}` | — | `TreinoResponse` (com exercícios) |
| `GET` | `/aluno/execucoes` | `?pagina=&tamanhoPagina=` | `ListarExecucoesResponse` |
| `POST` | `/aluno/execucoes` | `{ treinoId, dataExecucao, observacao?, exercicios[] }` | `201` |
| `GET` | `/aluno/vinculo` | — | `VinculoResponse` |
| `POST` | `/aluno/vinculo/trocar-treinador` | `{ novoTreinadorId, trarFichas }` | `201 VinculoResponse` |

#### Conta — `/conta` (JWT obrigatório)

| Método | Rota | Body | Resposta |
|--------|------|------|----------|
| `GET` | `/conta/perfil` | — | `PerfilResponse` |
| `PATCH` | `/conta/perfil` | `{ nome }` | `204` |
| `POST` | `/conta/senha` | `{ senhaAtual, novaSenha }` | `204` |
| `POST` | `/conta/logout` | — | `204` (revoga JTI) |

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
| `LimiteTreinadorService` | `PlanoTreinador.MaxAlunos` vs alunos ativos do treinador | Ao aprovar vínculo | `LimiteAlunosAtingidoException` → 422 |

`PacoteAluno` não tem mais limite de fichas — campo `MaxFichas` foi removido. Controle é feito via `Descricao` livre.

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

1. Aluno solicita troca via `POST /aluno/vinculo/trocar-treinador`
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

---

### Testes

```
597 testes | 0 falhas

Domain/          → entidades, value objects, domain events, exceções, máquina de estados
Application/     → handlers (unit), services de limite
Infrastructure/  → JwtService, email handlers (TreinadorAprovado, Reprovado, Inativado, VinculoAprovado)
Api/Endpoints/   → endpoints via WebApplicationFactory (auth, status codes, isolamento, paginação, admin visibilidade)
Integration/     → fluxo completo
```

Padrões adotados:

- `HandleAsync` declarado como `virtual` em todos os handlers para mock via Moq
- `ArgumentNullException.ThrowIfNull(command)` em todo handler que recebe command
- `It.IsAny<CancellationToken>()` em todos os setups de repositório
- Auth em testes de endpoint: `TestAuthHandler` substitui JWT
- Handlers mockados via `RemoveAll + AddSingleton` no `WebApplicationFactory`
- `TreinadorId` injetado via reflection quando necessário

---

## Frontend

Ver [`frontend/README.md`](frontend/README.md) para detalhes completos.

**Stack resumida**: Next.js 16 · React 19 · MUI v9 · TypeScript · React Hook Form + Zod · Axios · Zustand

```bash
cd frontend
npm install
npm run dev     # http://localhost:3000
npm run test    # Vitest (142 testes)
npm run build   # build de produção
```

---

## Deploy

### Docker local

```bash
# Copiar e preencher variáveis (JWT_SECRET etc.)
cp .env.example .env

# Subir backend + frontend + PostgreSQL local
docker compose up --build

# Backend: http://localhost:8080  |  Frontend: http://localhost:3000
# Swagger: http://localhost:8080/swagger  (modo Development)
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
