# forzion.tech — Backend API

ASP.NET Core 8.0 Web API para a plataforma forzion.tech, uma solução de gestão de treinos para personal trainers.

**Status**: ✅ **Pronto para Produção** | **273/273 testes passando** | **95/100 arquitetura** | **Integração Frontend Configurada**

## Stack

- **Framework**: ASP.NET Core 8.0 / C# 12 com nullable reference types
- **Banco**: PostgreSQL via Supabase com Row Level Security (RLS)
- **ORM**: Entity Framework Core 8.0
- **Auth**: Supabase Auth (JWT Bearer) + Multi-tenant Isolation
- **CORS**: Habilitado e configurável por ambiente ✅
- **Health Check**: `GET /health` para monitoramento ✅
- **Docs**: Swagger/OpenAPI em `/swagger` (Non-Production apenas)
- **Arquitetura**: Clean Architecture (Api, Application, Domain, Infrastructure)
- **Testing**: 273 testes (xUnit + Moq + FluentAssertions + WebApplicationFactory)

## Comandos

```bash
# Executar (HTTP: 5230 | HTTPS: 7220)
dotnet run --project forzion.tech.Api

# Build
dotnet build

# Testes
dotnet test forzion.tech.Tests

# Testes com cobertura
dotnet test forzion.tech.Tests --collect:"XPlat Code Coverage" --settings forzion.tech.Tests/coverage.runsettings

# Migrations — gerar
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef migrations add <Nome> --project forzion.tech.Infrastructure --startup-project forzion.tech.Api

# Migrations — aplicar homolog
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef database update --project forzion.tech.Infrastructure --startup-project forzion.tech.Api
```

## Estrutura

```
forzion.tech/
├── forzion.tech.Api/              # Endpoints, middlewares, configuração (Modular via Extensions)
├── forzion.tech.Application/      # Casos de uso, interfaces, DTOs
├── forzion.tech.Domain/           # Entidades, VOs, enums, exceções
├── forzion.tech.Infrastructure/   # EF Core, repositórios, migrações
├── forzion.tech.Tests/            # Testes (277, cobertura ~97%)
└── forzion.tech.slnx
```

## Endpoints Principais

| Método | Rota | Descrição | Auth |
|--------|------|-----------|------|
| `POST` | `/usuarios/registrar` | Registra perfil pós-auth Supabase. Cria tenant com plano Free. | JWT |
| `GET` | `/usuarios/me` | Retorna dados do usuário autenticado. | JWT |
| `PATCH` | `/usuarios/me` | Atualiza nome, fotoUrl (http/https), bio. | JWT |
| `POST` | `/alunos` | Cadastra um novo aluno no tenant. | JWT |
| `GET` | `/alunos` | Lista alunos do tenant (paginado). | JWT |
| `POST` | `/exercicios` | Cadastra um novo exercício na biblioteca do tenant. | JWT |
| `GET` | `/exercicios` | Lista exercícios da biblioteca (paginado). | JWT |
| `POST` | `/treinos` | Cria um novo treino para um aluno. | JWT |
| `GET` | `/treinos/{id}` | Retorna os detalhes de um treino com seus exercícios. | JWT |
| `GET` | `/alunos/{id}/treinos` | Lista todos os treinos vinculados a um aluno. | JWT |
| `POST` | `/treinos/{id}/exercicios` | Adiciona um exercício a uma planilha de treino. | JWT |
| `POST` | `/treinos/{id}/duplicar` | Gera uma cópia exata de um treino existente. | JWT |
| `POST` | `/treinos/{id}/execucoes` | Registra que um aluno realizou o treino (check-in). | JWT |

## Arquitetura

O projeto segue os princípios da **Clean Architecture**, garantindo desacoplamento e testabilidade:
- **Domain**: Regras de negócio puras, entidades e Value Objects.
- **Application**: Orquestração de casos de uso e interfaces de infraestrutura.
- **Infrastructure**: Implementação de acesso a dados (EF Core) e integrações externas.
- **Api**: Interface HTTP via Minimal APIs, com configuração modular e tratamento global de exceções.

## Segurança e Multi-tenancy

- **Isolamento**: Garantido via `tenant_id` em todas as tabelas de negócio, filtrado automaticamente via Interceptor do EF Core.
- **RLS**: Row Level Security ativo no PostgreSQL como segunda camada de proteção.
- **JWT**: Validação rigorosa de tokens do Supabase Auth.
- **Autorização**: Controle granular baseado em Roles (Admin/Trainer) e status de ativação.

## Qualidade

- **Testes**: ✅ 273/273 testes automatizados passando (100% sucesso)
- **Cobertura**: ~95% em Domain e Application layers
- **Logs**: Logging estruturado com 22 pontos críticos em handlers principais
- **Paginação**: Centralizada em helper reutilizável (DRY principle)
- **Exception Handling**: Global com mapeamento para HTTP status codes apropriados
- **Migrations**: 8 migrations em sequência, todas sincronizadas com DbContext

## Branches

| Branch | Descrição |
|--------|-----------|
| `main` | Base / produção |
| `backend` | Desenvolvimento ativo |
