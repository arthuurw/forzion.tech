# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## ⚠️ STATUS: REFATORAÇÃO DE DOMÍNIO EM ANDAMENTO

**O modelo anterior estava incorreto.** O domínio foi construído com `Tenant/Academia` como unidade de isolamento, o que não reflete o produto real. Todo o domínio está sendo refatorado.

**Antes de qualquer implementação, leia:** `.claude/PLANO_REFATORACAO_DOMINIO.md`

### O que mudou
- `Tenant` → **removido**. Isolamento é feito pelo `TreinadorId`.
- `Usuario` → **substituído** por `Conta` (auth unificada) + `Treinador`
- `Supabase Auth` → **substituído** por JWT próprio (BCrypt + HMAC-SHA256)
- `TenantRole` → **removido**
- `PlanoTreinador` (global, admin) + `PacoteAluno` (por treinador) substituem o modelo de planos anterior
- Toda implementação nova deve seguir o plano em `PLANO_REFATORACAO_DOMINIO.md`

### O que está em andamento
Fase 1 — Auth: `Conta`, `JwtService`, `POST /auth/login`, middleware, `IUserContext`

---

## Project Overview

ASP.NET Core 8.0 Web API backend for forzion.tech — Training Management Platform.
- **Database**: PostgreSQL on Supabase (schemas: `homolog` dev / `public` prod)
- **Auth**: JWT próprio (BCrypt + HMAC-SHA256) — sem Supabase Auth
- **Isolamento**: por `TreinadorId` (sem multi-tenant)

## Commands

```bash
# Run API (HTTP: 5230 | HTTPS: 7220)
dotnet run --project forzion.tech.Api

# Run with specific environment
ASPNETCORE_ENVIRONMENT=Development dotnet run --project forzion.tech.Api

# Build
dotnet build

# Run all tests
dotnet test forzion.tech.Tests

# Run tests with coverage report
dotnet test forzion.tech.Tests --collect:"XPlat Code Coverage" --settings forzion.tech.Tests/coverage.runsettings

# EF Migrations - create new
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef migrations add <MigrationName> --project forzion.tech.Infrastructure --startup-project forzion.tech.Api

# EF Migrations - apply to database
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef database update --project forzion.tech.Infrastructure --startup-project forzion.tech.Api
```

**Swagger/OpenAPI**: Available at `/swagger` (Development and Homolog only)
**Health Check**: Available at `GET /health` (all environments)

## Architecture

- **Pattern**: Clean Architecture
  - `forzion.tech.Api` → HTTP endpoints, middleware, configuration
  - `forzion.tech.Application` → Use cases, handlers, validators, DTOs
  - `forzion.tech.Domain` → Entities, Value Objects, enums, domain exceptions
  - `forzion.tech.Infrastructure` → EF Core, repositories, migrations
  - `forzion.tech.Tests` → Unit + Integration tests (xUnit + Moq + WebApplicationFactory)

- **Framework**: ASP.NET Core 8.0 Minimal APIs, C# 12
- **Database**: EF Core 8.0 + PostgreSQL via Npgsql
- **Features mantidas**:
  - ✅ CORS configurável por ambiente
  - ✅ Health check endpoint
  - ✅ Global exception handling (ProblemDetails RFC 7807)
  - ✅ Structured logging
  - ✅ Centralized pagination helper
  - ✅ FluentValidation

## Configuration

**appsettings Structure**:
- `appsettings.json` → Base configuration (committed)
- `appsettings.Development.json` → Dev overrides with localhost origins
- `appsettings.Homolog.json` → Local dev environment (not committed)
- `appsettings.Production.json` → Production environment (not committed)

**Secrets** (via User Secrets or environment variables):
- `Auth:JwtSecret` → chave HMAC para assinar JWT
- `Auth:JwtIssuer` → issuer do token
- `Auth:JwtAudience` → audience do token
- `ConnectionStrings:AppConnection` → PostgreSQL connection string
- `Database:Schema` → "public" (prod) or "homolog" (dev)

User Secrets ID: `049d65fb-2c12-483c-b56e-cb753632d11f` (in forzion.tech.Api.csproj)

## Novo Modelo — Resumo de Entidades

| Entidade | Status | Descrição |
|---|---|---|
| `Conta` | 🆕 novo | Auth unificada (email + BCrypt). TipoConta: SystemAdmin\|Treinador\|Aluno |
| `SystemUser` | ♻️ refatorado | Vinculado a Conta. SystemRole: SuperAdmin\|Support\|Operator |
| `Treinador` | 🆕 substitui Usuario | ContaId, PlanoTreinadorId, Status com aprovação |
| `Aluno` | ♻️ refatorado | ContaId, sem TenantId |
| `VinculoTreinadorAluno` | 🆕 novo | Aprovação pelo treinador, cascade na inativação |
| `PlanoTreinador` | 🆕 novo | Global (admin). Define MaxAlunos por treinador |
| `PacoteAluno` | 🆕 novo | Por treinador (livre). Define MaxFichas por aluno |
| `LogAprovacao` | 🆕 novo | Auditoria de aprovações e inativações |
| `Exercicio` | ♻️ refatorado | TreinadorId nullable (null = global) |
| `Treino` | ♻️ refatorado | Remove TenantId, mantém TreinadorId |
| `ExecucaoTreino` | ♻️ refatorado | Remove TenantId |
| `Tenant` | ❌ removido | — |
| `Usuario` | ❌ removido | — |

## Autorização por TipoConta

| Grupo de endpoints | SystemAdmin | Treinador | Aluno |
|---|---|---|---|
| `/admin/*` | ✅ | ❌ | ❌ |
| `/treinador/*` | ❌ | ✅ (próprios dados) | ❌ |
| `/aluno/*` | ❌ | ❌ | ✅ (próprios dados) |
| `/auth/*` | público | público | público |

## Key Implementation Details

### Auth própria
- `Conta` armazena `PasswordHash` (BCrypt, cost factor 12)
- `JwtService` gera token HMAC-SHA256 com claims: `conta_id`, `tipo_conta`, `perfil_id`
- `IUserContext` extrai esses claims do token em cada requisição
- Sem Supabase Auth, sem SupabaseId nas entidades

### Isolamento de dados
- Sem `tenant_id` — dados são isolados por `TreinadorId`
- Treinador só acessa seus próprios Treinos, Exercícios, Alunos
- Aluno só acessa suas próprias Fichas e Execuções

### Fluxo de aprovação
- Treinador: cadastro → AguardandoAprovacao → admin aprova → Ativo
- Aluno+Vínculo: cadastro → AguardandoAprovacao → treinador aprova → Ativo
- Toda aprovação grava `LogAprovacao` (quem aprovou, quando)

### Limites de plano
- `PlanoTreinador.MaxAlunos` → hard limit, validado ao aprovar vínculo aluno-treinador
- `PacoteAluno.MaxFichas` → hard limit, validado ao vincular ficha ao aluno
- Sem limite na quantidade de `PacoteAluno` por treinador

### Cascade de inativação
- Inativar `Treinador` → inativa todos `VinculoTreinadorAluno` ativos
- Inativar `VinculoTreinadorAluno` → inativa todos `TreinoAluno` do par

### Testing Strategy
- xUnit + Moq + FluentAssertions + WebApplicationFactory
- `HandleAsync` deve ser `virtual` para Moq
- `It.IsAny<CancellationToken>()` em todos os setups

## Key Files by Purpose

| File | Purpose |
|------|---------|
| `Program.cs` | Application entry point |
| `DependencyInjectionExtensions.cs` | Service registration + DI |
| `RouteBuilderExtensions.cs` | Endpoint group mapping |
| `GlobalExceptionHandler.cs` | Centralized exception handling (RFC 7807) |
| `AppDbContext.cs` | EF Core context |
| `PaginationExtensions.cs` | Reusable pagination helper |
| `.claude/PLANO_REFATORACAO_DOMINIO.md` | **Plano completo da refatoração — ler antes de implementar** |

## Notes

- Branch: `backend`; `main` é a base
- Sem integrações externas de pagamento ou e-mail no MVP
- Docker e CI/CD são melhorias futuras
