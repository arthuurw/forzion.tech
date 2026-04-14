# forzion.tech — Backend API

ASP.NET Core 8.0 Web API para a plataforma forzion.tech, uma solução de gestão de treinos para personal trainers.

## Stack

- **Framework**: ASP.NET Core 8.0 / C# com nullable reference types
- **Banco de Dados**: PostgreSQL via Supabase
- **ORM**: Entity Framework Core
- **Autenticação**: Supabase Auth (validação de JWT via middleware)
- **Documentação**: Swagger/OpenAPI em `/swagger` (apenas em desenvolvimento)
- **Arquitetura**: Clean Architecture (Api, Application, Domain, Infrastructure)

## Comandos

```bash
# Executar a API (HTTP: 5230 | HTTPS: 7220)
dotnet run --project forzion.tech.Api

# Build
dotnet build

# Testes
dotnet test

# Aplicar migrations
dotnet ef database update --project forzion.tech.Infrastructure --startup-project forzion.tech.Api
```

## Estrutura do Projeto

```
forzion.tech/
├── forzion.tech.Api/              # Endpoints, middlewares e configuração
├── forzion.tech.Application/      # Casos de uso, interfaces e DTOs
├── forzion.tech.Domain/           # Entidades, enums e regras de negócio
├── forzion.tech.Infrastructure/   # EF Core, repositórios e persistência
└── forzion.tech.slnx              # Solution file
```

## Funcionalidades Implementadas

### Registro de Usuário — `POST /usuarios/registrar`

Registra o perfil do usuário no sistema após autenticação no Supabase. Cria automaticamente um tenant vinculado ao usuário com o plano Free.

**Requer**: Bearer token JWT (Supabase)

**Body**:
```json
{
  "nome": "Arthur",
  "email": "arthur@exemplo.com",
  "tenantNome": "Minha Academia"
}
```

**Respostas**:
- `201 Created` — Usuário e tenant criados com sucesso
- `400 Bad Request` — Dados inválidos
- `401 Unauthorized` — Token ausente ou inválido
- `409 Conflict` — Usuário já registrado
- `422 Unprocessable Entity` — Erro de domínio (ex: slug não pôde ser gerado)

## Arquitetura

O projeto segue **Clean Architecture** com fluxo de dependências:

```
Api → Application → Domain
Infrastructure → Application / Domain
```

- **Domain** não depende de nenhuma camada
- **Application** define interfaces; não acessa infraestrutura diretamente
- **Infrastructure** implementa repositórios e o contexto EF Core
- **Api** é fina: delega toda lógica para a camada Application

## Branches

| Branch    | Descrição                          |
|-----------|------------------------------------|
| `main`    | Branch base / produção             |
| `backend` | Desenvolvimento do backend         |

## Observações

- Credenciais de banco estão em `anotacoes.txt` (excluído do git — não commitar)
- Multi-tenancy implementado via `tenantId` em todas as entidades
- Logs estruturados com `ILogger` em operações críticas
