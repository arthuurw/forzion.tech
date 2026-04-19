# Project Context

## Current Branch

- `frontend`

## Repository Shape

- Backend: ASP.NET Core 8 Minimal API with Clean Architecture split into:
  - `forzion.tech.Api`
  - `forzion.tech.Application`
  - `forzion.tech.Domain`
  - `forzion.tech.Infrastructure`
  - `forzion.tech.Tests`
- Frontend: Next.js app in `frontend/` using App Router, React 19, Next 16, MUI 9, React Hook Form, Zod, Axios, Zustand.

## Backend Overview

- Entry point: `forzion.tech.Api/Program.cs`
- DI and pipeline:
  - `forzion.tech.Api/Extensions/DependencyInjectionExtensions.cs`
  - `forzion.tech.Api/Extensions/RouteBuilderExtensions.cs`
- Auth:
  - Custom JWT with claims `conta_id`, `tipo_conta`, `perfil_id`
  - `JwtService` in `forzion.tech.Infrastructure/Services/JwtService.cs`
  - `HttpUserContext` reads JWT claims in `forzion.tech.Api/Context/HttpUserContext.cs`
- Persistence:
  - EF Core + PostgreSQL in `forzion.tech.Infrastructure/Persistence/AppDbContext.cs`
  - Schema controlled by `Database:Schema`
- Domain centers on:
  - `Conta`, `Treinador`, `Aluno`, `VinculoTreinadorAluno`
  - `PlanoTreinador`, `PacoteAluno`
  - `Treino`, `TreinoAluno`, `TreinoExercicio`
  - `ExecucaoTreino`, `ExecucaoExercicio`
  - `LogAprovacao`

## Backend Functional Areas

- Public auth:
  - `POST /auth/login`
  - `POST /auth/register/treinador`
  - `POST /auth/register/aluno`
  - `GET /auth/planos`
  - `GET /auth/treinadores/{id}/pacotes`
- Admin:
  - trainer approval/inactivation
  - trainer plan assignment
  - list/create plans
  - list trainers
- Trainer:
  - approve/unlink student links
  - list students
  - list workouts
  - assign workout to student
  - manage exercises
  - manage packages
- Student area:
  - list active workouts
  - list executions
  - register workout execution

## Frontend Overview

- Security headers and API base URL config:
  - `frontend/next.config.ts`
- Auth and route protection:
  - `frontend/src/proxy.ts`
  - `frontend/src/middleware.ts`
  - `frontend/src/lib/auth/context.tsx`
  - `frontend/src/lib/auth/session.ts`
- API access:
  - browser axios client: `frontend/src/lib/api/client.ts`
  - server axios client: `frontend/src/lib/api/server.ts`
  - feature clients: `frontend/src/lib/api/*.ts`
- Route groups already scaffolded:
  - public
  - admin
  - treinador
  - aluno

## Important Integration Findings

- The frontend is not yet aligned with the backend contract.
- Confirmed mismatches:
  - Backend login response is `Token`, `TipoConta`, `ContaId`; frontend expects `token`, `tipoConta`, `contaId`, `perfilId`.
  - Frontend proxy route `frontend/src/app/api/auth/treinadores/route.ts` calls `GET /auth/treinadores`, but this endpoint does not exist in the backend.
  - Student registration page sends `pacoteId`, but backend `POST /auth/register/aluno` accepts `nome`, `email`, `senha`, `treinadorId`, `telefone`.
  - Frontend API client references endpoints not exposed by the backend today, including examples such as:
    - `/treinador/vinculos`
    - `/treinador/alunos/{alunoId}`
    - `/treinador/alunos/{alunoId}/fichas`
    - `/aluno/fichas/{treinoAlunoId}`
    - `/conta/perfil`
    - `/conta/senha`
- This indicates the frontend is partly ahead of the backend surface or built from an older/different contract.

## Validation Results

- Backend tests executed with:
  - `dotnet test forzion.tech.Tests/forzion.tech.Tests.csproj`
- Current result:
  - 329 passed
  - 0 failed
- Backend fixes applied:
  - explicit `[FromServices]` and `[FromBody]` where needed in Minimal API endpoints
  - `TreinoResponseExtensions.ToResponse(...)` no longer crashes when `TreinoExercicio.Exercicio` navigation is not loaded

- Frontend build executed with:
  - `npm run build`
- Result:
  - build failed
- Main frontend failure:
  - Next 16 detects both `src/middleware.ts` and `src/proxy.ts`
  - framework requires keeping only the proxy entry pattern

## Immediate Implementation Priorities

- Align frontend contracts with backend before building new features.
- Decide whether frontend should adapt to current backend or backend should grow to match the frontend route model.

## Working Assumption For Future Tasks

- Treat the backend as the current source of truth for business rules.
- Treat the frontend as partially implemented and structurally useful, but not yet contract-safe.
