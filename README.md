# forzion.tech

Plataforma de gestão de treinos para personal trainers e alunos.

**Backend**: ASP.NET Core 8.0 · **Frontend**: Next.js 16 + MUI v9 · **Banco**: PostgreSQL (Supabase)

**Status**: ✅ 324 testes passando | Clean Architecture | JWT próprio | Isolamento por TreinadorId

---

## Repositório

```
forzion.tech/
├── forzion.tech.Api/          # HTTP — Minimal APIs, middleware, filtros
├── forzion.tech.Application/  # Use cases, handlers, validações, DTOs
├── forzion.tech.Domain/       # Entidades, Value Objects, Events, exceções
├── forzion.tech.Infrastructure/ # EF Core, repositórios, migrations, serviços
├── forzion.tech.Tests/        # xUnit + Moq + FluentAssertions + WebApplicationFactory
└── frontend/                  # Next.js 16 — ver frontend/README.md
```

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

# Testes com cobertura
dotnet test forzion.tech.Tests --collect:"XPlat Code Coverage" --settings forzion.tech.Tests/coverage.runsettings

# Gerar migration (sempre com Homolog)
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

> **Migrations**: sempre geradas com `Homolog`. Produção não suporta `database update` direto — gere o script e aplique via Supabase SQL Editor substituindo `homolog` por `public`.

---

### Estrutura do Projeto (Backend)

```
forzion.tech.Api/
├── Configuration/        # JWT, CORS, Swagger
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
├── Filters/              # PerfilIdRequiredFilter
└── Middleware/           # GlobalExceptionHandler (RFC 7807)

forzion.tech.Application/
├── Interfaces/           # IUserContext, IJwtService, IPasswordHasher,
│                         # IUnitOfWork, IDomainEventDispatcher, IRepository<T>
├── Services/             # LimiteTreinadorService, LimiteFichasService
└── UseCases/             # Handlers CQRS-like por domínio

forzion.tech.Domain/
├── Entities/             # Conta, Treinador, Aluno, VinculoTreinadorAluno,
│                         # Treino, Exercicio, ExecucaoTreino, LogAprovacao, …
├── Enums/                # TipoConta, TreinadorStatus, AlunoStatus, VinculoStatus, …
├── Events/               # IDomainEvent, IHasDomainEvents, TreinadorAprovadoEvent,
│                         # TreinadorInativadoEvent, VinculoAprovadoEvent
├── Exceptions/           # Exceções de domínio tipadas
└── ValueObjects/         # Email

forzion.tech.Infrastructure/
├── Migrations/
├── Persistence/
│   ├── AppDbContext.cs   # DbContext + IUnitOfWork
│   ├── Configurations/   # Fluent API por entidade
│   ├── Repositories/     # Implementações
│   └── Seeders/          # DataSeeder (conta admin padrão)
└── Services/             # JwtService, BcryptPasswordHasher

forzion.tech.Tests/
├── Api/                  # Testes de endpoint (WebApplicationFactory)
├── Application/          # Handlers e services (unit)
├── Domain/               # Entidades e value objects
├── Infrastructure/       # JwtService
└── Integration/          # Fluxo completo
```

---

### Modelo de Domínio

| Entidade | Descrição |
|----------|-----------|
| `Conta` | Auth unificada. E-mail + PasswordHash (BCrypt). `TipoConta`: `SystemAdmin`, `Treinador`, `Aluno`. |
| `SystemUser` | Perfil de admin vinculado a uma `Conta` do tipo `SystemAdmin`. |
| `Treinador` | Perfil de treinador. Possui `PlanoTreinadorId`. Status: `AguardandoAprovacao → Ativo → Inativo`. |
| `Aluno` | Perfil de aluno vinculado a uma `Conta` do tipo `Aluno`. |
| `VinculoTreinadorAluno` | Relação entre treinador e aluno. Carrega `PacoteAlunoId`. Status: `AguardandoAprovacao → Ativo → Inativo`. |
| `PlanoTreinador` | Plano global (gerido pelo admin). Define `MaxAlunos` por treinador. |
| `PacoteAluno` | Pacote criado pelo treinador. Define `MaxFichas` por aluno vinculado. |
| `Treino` | Ficha de treino com lista de `TreinoExercicio`. |
| `TreinoAluno` | Vínculo ficha × aluno. Status: `Ativo / Inativo`. |
| `Exercicio` | Global (`TreinadorId = null`) ou privado do treinador. |
| `ExecucaoTreino` | Registro de sessão realizada pelo aluno. |
| `LogAprovacao` | Auditoria de aprovações e inativações. |

### Domain Events

`Treinador` e `VinculoTreinadorAluno` implementam `IHasDomainEvents` e levantam eventos em operações de negócio:

| Evento | Levantado em |
|--------|-------------|
| `TreinadorAprovadoEvent` | `Treinador.Aprovar()` |
| `TreinadorInativadoEvent` | `Treinador.Inativar()` |
| `VinculoAprovadoEvent` | `VinculoTreinadorAluno.Aprovar()` |

O `IDomainEventDispatcher` (Application) está pronto para consumo futuro por handlers de notificação ou auditoria reativa.

---

### Autenticação e Autorização

O `JwtService` gera tokens HMAC-SHA256 com os claims:

| Claim | Conteúdo |
|-------|----------|
| `conta_id` | ID da `Conta` autenticada |
| `tipo_conta` | `SystemAdmin`, `Treinador` ou `Aluno` |
| `perfil_id` | ID do perfil correspondente (`SystemUser.Id`, `Treinador.Id` ou `Aluno.Id`) |

Políticas de autorização:

| Política | `TipoConta` exigido |
|----------|---------------------|
| `SystemAdmin` | `SystemAdmin` |
| `Treinador` | `Treinador` |
| `Aluno` | `Aluno` |
| *(sem política)* | Qualquer JWT válido |

O `PerfilIdRequiredFilter` garante que o claim `perfil_id` está presente em todos os endpoints de grupos protegidos.

---

### Endpoints

#### Auth — `/auth` (público)

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/auth/login` | Autentica e retorna JWT + `TipoConta` + `ContaId` + `PerfilId`. |
| `POST` | `/auth/register/treinador` | Cadastra treinador. Status inicial: `AguardandoAprovacao`. |
| `POST` | `/auth/register/aluno` | Cadastra aluno + vínculo com treinador. Vínculo inicia `AguardandoAprovacao`. |
| `GET` | `/auth/planos` | Lista planos disponíveis (para exibição no cadastro). |
| `GET` | `/auth/treinadores/{id}/pacotes` | Lista pacotes de fichas de um treinador. |

#### Admin — `/admin` (política `SystemAdmin`)

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/admin/treinadores` | Lista treinadores com filtro opcional por status. |
| `POST` | `/admin/treinadores/{id}/aprovar` | Aprova treinador → `Ativo`. Registra `LogAprovacao`. |
| `POST` | `/admin/treinadores/{id}/inativar` | Inativa treinador em cascata (vínculos + fichas). Registra log. |
| `PATCH` | `/admin/treinadores/{id}/plano` | Atribui ou troca o `PlanoTreinador` do treinador. |
| `GET` | `/admin/planos` | Lista todos os planos globais. |
| `POST` | `/admin/planos` | Cria novo plano global com `MaxAlunos`. |

#### Treinador — `/treinador` (política `Treinador`)

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/treinador/vinculos` | Lista vínculos do treinador com paginação. |
| `POST` | `/treinador/vinculos/{id}/aprovar` | Aprova vínculo de aluno e define `PacoteAlunoId`. Valida limite de alunos. |
| `POST` | `/treinador/vinculos/{id}/desvincular` | Inativa vínculo em cascata com fichas do par. |
| `GET` | `/treinador/alunos` | Lista alunos vinculados. |
| `GET` | `/treinador/alunos/{alunoId}` | Obtém dados de um aluno vinculado. |
| `GET` | `/treinador/alunos/{alunoId}/fichas` | Lista fichas ativas de um aluno. |
| `POST` | `/treinador/alunos/{alunoId}/fichas/{treinoId}` | Vincula ficha a aluno. Valida limite de fichas do pacote. |
| `GET` | `/treinador/treinos` | Lista fichas criadas pelo treinador. |
| `GET` | `/treinador/exercicios` | Lista exercícios disponíveis (globais + privados). |
| `POST` | `/treinador/exercicios` | Cria exercício privado. |
| `POST` | `/treinador/exercicios/{id}/copiar` | Copia exercício global para a biblioteca privada. |
| `GET` | `/treinador/pacotes` | Lista pacotes criados pelo treinador. |
| `POST` | `/treinador/pacotes` | Cria novo pacote de fichas (`MaxFichas` + preço). |

#### Treinos — `/treinos` (JWT obrigatório)

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/treinos` | Cria nova ficha de treino. |
| `GET` | `/treinos/{id}` | Obtém ficha. |
| `POST` | `/treinos/{id}/vincular-aluno` | Vincula ficha a aluno (atalho alternativo). |
| `POST` | `/treinos/{id}/exercicios` | Adiciona exercício à ficha. |
| `DELETE` | `/treinos/{id}/exercicios/{exercicioId}` | Remove exercício da ficha. |
| `POST` | `/treinos/{id}/duplicar` | Duplica ficha de treino. **201 Created**. |
| `POST` | `/treinos/{id}/execucoes` | Registra execução de treino. **201 Created**. |

#### Alunos — `/alunos` (JWT obrigatório)

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/alunos` | Lista alunos do treinador autenticado. |
| `GET` | `/alunos/{id}` | Obtém dados de um aluno. |
| `GET` | `/alunos/{alunoId}/treinos` | Lista fichas vinculadas a um aluno. |
| `PATCH` | `/alunos/{id}` | Atualiza dados do aluno. |
| `PATCH` | `/alunos/{id}/status` | Altera status do aluno. Exclusivo `SystemAdmin`. |

#### Área do Aluno — `/aluno` (política `Aluno`)

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/aluno/fichas` | Lista fichas ativas vinculadas ao aluno autenticado. |
| `GET` | `/aluno/fichas/{id}` | Detalhe de uma ficha com exercícios. |
| `GET` | `/aluno/execucoes` | Lista execuções registradas. |
| `POST` | `/aluno/execucoes` | Registra execução. Valida ficha ativa e aluno não inativo. |

---

### Regras de Negócio

#### Fluxo de Aprovação

```
Treinador:  Cadastro → AguardandoAprovacao → (admin aprova) → Ativo
Aluno:      Cadastro → AguardandoAprovacao → (treinador aprova vínculo) → Ativo
```

Toda aprovação e inativação registra um `LogAprovacao`.

#### Limites de Plano

| Serviço | O que valida | Quando |
|---------|-------------|--------|
| `LimiteTreinadorService` | `PlanoTreinador.MaxAlunos` vs alunos ativos | Ao aprovar vínculo |
| `LimiteFichasService` | `PacoteAluno.MaxFichas` vs fichas ativas | Ao vincular ficha |

Exceder o limite lança `LimiteAlunosAtingidoException` ou `LimiteFichasAtingidoException` → HTTP 422.

#### Cascata de Inativação

| Ação | Efeito |
|------|--------|
| Inativar `Treinador` | Inativa todos os `VinculoTreinadorAluno` ativos → inativa todos os `TreinoAluno` desses vínculos |
| Desvincular `VinculoTreinadorAluno` | Inativa todos os `TreinoAluno` do par (treinador × aluno) |

#### Isolamento de Dados

Sem `TenantId`. Isolamento garantido por `TreinadorId`. Os handlers validam `IUserContext.PerfilId` contra o dono do recurso, lançando `AcessoNegadoException` (403) em violações.

---

### Tratamento de Erros

O `GlobalExceptionHandler` implementa **RFC 7807** e mapeia exceções para status HTTP:

| Exceção | Status |
|---------|--------|
| `CredenciaisInvalidasException` | 401 |
| `AlunoNaoEncontradoException` | 404 |
| `TreinadorNaoEncontradoException` | 404 |
| `TreinoNaoEncontradoException` | 404 |
| `VinculoNaoEncontradoException` | 404 |
| `ExercicioNaoEncontradoException` | 404 |
| `AcessoNegadoException` | 403 |
| `AlunoInativoException` | 403 |
| `DomainException` (e subclasses) | 422 |
| `ValidationException` (FluentValidation) | 400 (com campo → erros) |
| Qualquer outra | 500 (mensagem interna não exposta) |

---

### Configuração

```jsonc
// appsettings.json — base (versionado)
{
  "Auth": {
    "JwtSecret": "",      // via secrets
    "JwtIssuer": "",      // via secrets
    "JwtAudience": ""     // via secrets
  },
  "ConnectionStrings": {
    "AppConnection": ""   // via secrets
  },
  "Database": {
    "Schema": "homolog"   // "public" em produção
  }
}
```

```bash
dotnet user-secrets set "Auth:JwtSecret" "<chave-hmac>"          --project forzion.tech.Api
dotnet user-secrets set "Auth:JwtIssuer" "forzion.tech"          --project forzion.tech.Api
dotnet user-secrets set "Auth:JwtAudience" "forzion.tech"        --project forzion.tech.Api
dotnet user-secrets set "ConnectionStrings:AppConnection" "<pg>"  --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminEmail" "admin@forzion.tech"   --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminPassword" "<senha>"           --project forzion.tech.Api
```

User Secrets ID: `049d65fb-2c12-483c-b56e-cb753632d11f`

| Ambiente | Schema | Swagger | Seeder |
|----------|--------|---------|--------|
| `Development` | `homolog` | ✅ | ✅ |
| `Homolog` | `homolog` | ✅ | ✅ |
| `Production` | `public` | ❌ | ❌ |
| `Test` | em memória / mock | ❌ | ❌ |

---

### Testes

```
324 testes | 0 falhas

Domain/          → entidades, value objects, domain events, exceções
Application/     → handlers (unit), services de limite
Infrastructure/  → JwtService
Api/Endpoints/   → endpoints via WebApplicationFactory (auth, status codes, isolamento)
Integration/     → fluxo completo
```

Padrões adotados:
- `HandleAsync` declarado como `virtual` para mock via Moq
- `It.IsAny<CancellationToken>()` em todos os setups de repositório
- `CallBase = true` em mocks de handlers com validação própria

---

## Frontend

Ver [`frontend/README.md`](frontend/README.md) para detalhes completos.

**Stack resumida**: Next.js 16 · React 19 · MUI v9 · TypeScript · React Hook Form + Zod · Axios · Zustand

```bash
cd frontend
npm install
npm run dev    # http://localhost:3000
```
