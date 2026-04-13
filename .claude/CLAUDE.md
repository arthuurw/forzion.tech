# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ASP.NET Core 8.0 Web API backend for forzion.tech. Currently in early scaffolding stage — no business logic implemented yet. The database is PostgreSQL hosted on Supabase.

## Commands

```bash
# Run the API (HTTP on port 5230, HTTPS on port 7220)
dotnet run --project forzion.tech.Api

# Build
dotnet build

# Run tests (no tests yet)
dotnet test
```

Swagger UI is available at `/swagger` in development mode.

## Architecture

- **Pattern**: Clean Architecture (Api, Application, Domain, Infrastructure)
- **Framework**: ASP.NET Core 8.0, C# with nullable reference types
- **ORM**: Entity Framework Core + PostgreSQL via Supabase
- **Auth**: Supabase Auth (JWT validation in middleware)
- **Docs**: Swashbuckle/OpenAPI at `/swagger` (dev only)

The solution file is `forzion.tech.slnx`.

## Notes

- Database connection details are in `anotacoes.txt` (excluded from git — do not commit credentials)
- The project is on the `backend` branch; `main` is the base branch for PRs

## Skills e Regras do Projeto

@skill_regra-de-ouro.md
@skill_regras-arquitetura.md
@skill_engenharia.md
@skill_requisitos-funcionais.md
@skill_code-review.md
@skill_pull-request.md
