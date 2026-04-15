# forzion.tech — Backend API

ASP.NET Core 8.0 Web API para a plataforma forzion.tech, uma solução de gestão de treinos para personal trainers.

## Stack

- **Framework**: ASP.NET Core 8.0 / C# com nullable reference types
- **Banco**: PostgreSQL via Supabase
- **ORM**: Entity Framework Core 8.0
- **Auth**: Supabase Auth (JWT via middleware)
- **Docs**: Swagger/OpenAPI em `/swagger` (Homolog apenas)
- **Arquitetura**: Clean Architecture (Api, Application, Domain, Infrastructure)

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
├── forzion.tech.Api/              # Endpoints, middlewares, configuração
├── forzion.tech.Application/      # Casos de uso, interfaces, DTOs
├── forzion.tech.Domain/           # Entidades, VOs, enums, exceções
├── forzion.tech.Infrastructure/   # EF Core, repositórios, migrações
├── forzion.tech.Tests/            # Testes (157, cobertura ~97%)
└── forzion.tech.slnx
```

## Endpoints

| Método | Rota | Descrição | Auth |
|--------|------|-----------|------|
| `POST` | `/usuarios/registrar` | Registra perfil pós-auth Supabase. Cria tenant com plano Free. | JWT |
| `GET` | `/usuarios/me` | Retorna dados do usuário autenticado. | JWT |
| `PATCH` | `/usuarios/me` | Atualiza nome, fotoUrl (http/https), bio. | JWT |
| `PATCH` | `/usuarios/{id}/status` | Altera status de usuário. Somente Admin. | JWT + Admin |

### PATCH /usuarios/me — body (todos opcionais)
```json
{ "nome": "Arthur", "fotoUrl": "https://...", "bio": "..." }
```

### PATCH /usuarios/{id}/status — body
```json
{ "status": "Inativo" }
```

### POST /usuarios/registrar — body
```json
{ "nome": "Arthur", "email": "arthur@exemplo.com", "tenantNome": "Minha Academia" }
```

## Arquitetura

```
Api → Application → Domain
Infrastructure → Application / Domain
```

- **Domain**: entidades, VOs, enums, exceções — sem dependências externas
- **Application**: casos de uso, interfaces de repositório — sem acesso direto ao banco
- **Infrastructure**: EF Core, repositórios, interceptors
- **Api**: endpoints finos, validação de entrada, GlobalExceptionHandler

## Segurança

- JWT validado via Supabase Auth (`ValidateIssuerSigningKey=true`)
- RLS ativo no banco (homolog e produção) — isolamento por `tenant_id`
- `AllowedHosts` restrito ao domínio em produção
- `fotoUrl` valida scheme http/https antes de persistir
- Alteração de status restrita a usuários com Role=Admin (verificado via DB)
- Swagger desabilitado em produção

## Multi-tenancy

Isolamento em duas camadas:
1. **App**: queries filtradas por `tenant_id` do JWT claim
2. **Banco**: RLS policies em `usuarios` e `tenants` via `app.current_tenant_id`

## Branches

| Branch | Descrição |
|--------|-----------|
| `main` | Base / produção |
| `backend` | Desenvolvimento |

## Observações

- Credenciais em User Secrets (nunca commitadas) — ver `.claude/docs/secrets-e-configuracao.md`
- Toda migration deve ser aplicada em homolog **e** produção — ver `.claude/docs/banco-de-dados.md`
- Novas tabelas com `tenant_id` devem ter RLS policy no momento da criação
