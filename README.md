# forzion.tech — Backend API

ASP.NET Core 8.0 Web API para a plataforma **forzion.tech**, solução de gestão de treinos para personal trainers e seus alunos.

**Status**: ✅ 329/329 testes passando | Clean Architecture | JWT próprio | Isolamento por Treinador

---

## Stack

| Camada | Tecnologia |
|--------|------------|
| Framework | ASP.NET Core 8.0 / C# 12 / Minimal APIs |
| Banco de dados | PostgreSQL (Supabase) |
| ORM | Entity Framework Core 8.0 + Npgsql |
| Auth | JWT HMAC-SHA256 + BCrypt (sem Supabase Auth) |
| Validação | FluentValidation |
| Testes | xUnit + Moq + FluentAssertions + WebApplicationFactory |
| Documentação | Swagger/OpenAPI (`/swagger`) — ambientes non-production |

---

## Comandos

```bash
# Executar a API (HTTP :5230 | HTTPS :7220)
dotnet run --project forzion.tech.Api

# Executar em ambiente específico
ASPNETCORE_ENVIRONMENT=Homolog dotnet run --project forzion.tech.Api

# Build completo
dotnet build

# Todos os testes
dotnet test forzion.tech.Tests

# Testes com relatório de cobertura
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

> **Atenção — Migrations**: Sempre geradas com `Homolog`. Gerar com outro ambiente causa diffs incorretos no snapshot. Produção não suporta `database update` direto — use o script gerado e aplique via Supabase SQL Editor substituindo `homolog` por `public`.

---

## Estrutura do Projeto

```
forzion.tech/
├── forzion.tech.Api/
│   ├── Configuration/        # AuthenticationExtensions, CORS, Swagger
│   ├── Context/              # HttpUserContext (extrai claims do JWT)
│   ├── Endpoints/            # Minimal API handlers por grupo
│   │   ├── Admin/
│   │   ├── AlunoArea/
│   │   ├── Alunos/
│   │   ├── Auth/
│   │   ├── Exercicios/
│   │   ├── Treinador/
│   │   └── Treinos/
│   └── Middleware/           # GlobalExceptionHandler (RFC 7807)
├── forzion.tech.Application/
│   ├── Interfaces/           # IUserContext, IJwtService, IPasswordHasher, IRepository<T>
│   ├── Services/             # LimiteTreinadorService, LimiteFichasService
│   └── UseCases/             # Handlers organizados por domínio (CQRS-like)
├── forzion.tech.Domain/
│   ├── Entities/             # Conta, Treinador, Aluno, VinculoTreinadorAluno, Treino, …
│   ├── Enums/                # TipoConta, TreinadorStatus, AlunoStatus, VinculoStatus, …
│   ├── Exceptions/           # Exceções de domínio tipadas
│   └── ValueObjects/         # Email
├── forzion.tech.Infrastructure/
│   ├── Migrations/
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   ├── Configurations/   # Fluent API por entidade
│   │   ├── Repositories/     # Implementações dos IRepository<T>
│   │   └── Seeders/          # DataSeeder (conta admin padrão)
│   └── Services/             # JwtService, BcryptPasswordHasher
└── forzion.tech.Tests/
    ├── Api/                  # Testes de endpoint (WebApplicationFactory)
    ├── Application/          # Testes unitários de handlers e services
    ├── Domain/               # Testes unitários de entidades e VOs
    ├── Infrastructure/       # Testes de JwtService
    └── Integration/          # Fluxo completo
```

---

## Modelo de Domínio

| Entidade | Descrição |
|----------|-----------|
| `Conta` | Auth unificada. Armazena e-mail e `PasswordHash` (BCrypt). `TipoConta`: `SystemAdmin`, `Treinador`, `Aluno`. |
| `SystemUser` | Perfil de admin. Vinculado a uma `Conta` do tipo `SystemAdmin`. |
| `Treinador` | Perfil de treinador. Possui `PlanoTreinadorId` e passa por aprovação (`AguardandoAprovacao → Ativo`). |
| `Aluno` | Perfil de aluno. Vinculado a uma `Conta` do tipo `Aluno`. |
| `VinculoTreinadorAluno` | Relação entre treinador e aluno. Status: `AguardandoAprovacao → Ativo`. Guarda `PacoteAlunoId`. |
| `PlanoTreinador` | Plano global (gerido pelo admin). Define o limite de alunos (`MaxAlunos`) por treinador. |
| `PacoteAluno` | Pacote criado pelo treinador. Define o limite de fichas (`MaxFichas`) por aluno vinculado. |
| `Treino` | Ficha de treino criada pelo treinador. Contém uma lista de `TreinoExercicio`. |
| `TreinoAluno` | Vínculo entre uma ficha e um aluno específico. Status: `Ativo / Inativo`. |
| `Exercicio` | Exercício global (`TreinadorId = null`) ou privado do treinador. |
| `ExecucaoTreino` | Registro de uma sessão de treino realizada pelo aluno. |
| `LogAprovacao` | Auditoria de todas as ações de aprovação e inativação. |

---

## Autenticação e Autorização

### Geração do Token

O `JwtService` gera um token HMAC-SHA256 com os claims:

| Claim | Conteúdo |
|-------|----------|
| `conta_id` | ID da `Conta` autenticada |
| `tipo_conta` | `SystemAdmin`, `Treinador` ou `Aluno` |
| `perfil_id` | ID do perfil correspondente (`SystemUser.Id`, `Treinador.Id` ou `Aluno.Id`) |

O `HttpUserContext` extrai esses claims em cada requisição via `IHttpContextAccessor`, expondo `ContaId`, `PerfilId`, `TipoConta`, `IsSystemAdmin`, `IsTreinador` e `IsAluno`.

### Políticas de Autorização

| Política | `TipoConta` exigido |
|----------|---------------------|
| `SystemAdmin` | `SystemAdmin` |
| `Treinador` | `Treinador` |
| `Aluno` | `Aluno` |
| *(sem política)* | Qualquer JWT válido |

---

## Endpoints

### Auth — `/auth` (público)

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/auth/login` | Autentica e retorna JWT + `TipoConta` + `ContaId`. |
| `POST` | `/auth/register/treinador` | Cadastra treinador. Status inicial: `AguardandoAprovacao`. |
| `POST` | `/auth/register/aluno` | Cadastra aluno vinculado a um treinador. Vínculo inicia `AguardandoAprovacao`. |
| `GET` | `/auth/planos` | Lista planos disponíveis (para exibição no cadastro). |
| `GET` | `/auth/treinadores/{id}/pacotes` | Lista pacotes de fichas de um treinador (para exibição no cadastro do aluno). |

### Admin — `/admin` (política `SystemAdmin`)

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/admin/treinadores/{id}/aprovar` | Aprova treinador. Muda status para `Ativo`. Registra `LogAprovacao`. |
| `POST` | `/admin/treinadores/{id}/inativar` | Inativa treinador em cascata (vínculos + fichas de alunos). Registra log. |
| `PATCH` | `/admin/treinadores/{id}/plano` | Atribui ou troca o `PlanoTreinador` de um treinador. |
| `GET` | `/admin/planos` | Lista todos os planos globais. |
| `POST` | `/admin/planos` | Cria novo plano global com `MaxAlunos`. |

### Treinador — `/treinador` (política `Treinador`)

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/treinador/vinculos/{id}/aprovar` | Aprova vínculo de aluno e define `PacoteAlunoId`. Valida limite de alunos. |
| `POST` | `/treinador/vinculos/{id}/desvincular` | Inativa vínculo. Inativa em cascata todas as fichas do par. |
| `GET` | `/treinador/alunos` | Lista alunos vinculados ao treinador autenticado. |
| `GET` | `/treinador/treinos` | Lista fichas de treino criadas pelo treinador. |
| `POST` | `/treinador/alunos/{alunoId}/fichas/{treinoId}` | Vincula ficha existente a um aluno. Valida limite de fichas do pacote. |
| `GET` | `/treinador/exercicios` | Lista exercícios disponíveis (globais + privados do treinador). |
| `POST` | `/treinador/exercicios` | Cria exercício privado do treinador. |
| `POST` | `/treinador/exercicios/{id}/copiar` | Copia exercício global para a biblioteca privada do treinador. |
| `GET` | `/treinador/pacotes` | Lista pacotes de fichas criados pelo treinador. |
| `POST` | `/treinador/pacotes` | Cria novo pacote de fichas (`MaxFichas` + preço). |

### Treinos — `/treinos` (JWT obrigatório)

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/treinos` | Cria nova ficha de treino. |
| `GET` | `/treinos/{id}` | Obtém ficha. Treinador só acessa as próprias; aluno acessa apenas as vinculadas. |
| `GET` | `/alunos/{alunoId}/treinos` | Lista fichas vinculadas a um aluno específico. |
| `POST` | `/treinos/{id}/vincular-aluno` | Vincula ficha a aluno (atalho via rota de treinos). |
| `POST` | `/treinos/{id}/exercicios` | Adiciona exercício à ficha. Bloqueado se a ficha já foi executada. |
| `DELETE` | `/treinos/{id}/exercicios/{exercicioId}` | Remove exercício da ficha. Bloqueado se já executada. |
| `POST` | `/treinos/{id}/duplicar` | Duplica ficha de treino (sem execuções). |
| `POST` | `/treinos/{id}/execucoes` | Registra execução de treino pelo aluno autenticado. |

### Alunos — `/alunos` (JWT obrigatório)

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/alunos` | Cadastra aluno (sem vínculo — fluxo admin/treinador). |
| `GET` | `/alunos` | Lista alunos. Treinador vê os seus; admin vê todos. |
| `GET` | `/alunos/{id}` | Obtém aluno. Aluno acessa o próprio; treinador acessa vinculados; admin acessa todos. |
| `PATCH` | `/alunos/{id}` | Atualiza dados do aluno. Validado por vínculo ativo. |
| `PATCH` | `/alunos/{id}/status` | Altera status do aluno. Exclusivo `SystemAdmin`. |

### Exercícios — `/exercicios` (JWT obrigatório)

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/exercicios` | Cria exercício (global se admin, privado se treinador). |
| `GET` | `/exercicios` | Lista exercícios globais. |

### Área do Aluno — `/aluno` (política `Aluno`)

| Método | Rota | Descrição |
|--------|------|-----------|
| `GET` | `/aluno/fichas` | Lista fichas ativas vinculadas ao aluno autenticado. |
| `GET` | `/aluno/execucoes` | Lista execuções registradas pelo aluno. |
| `POST` | `/aluno/execucoes` | Registra execução de treino. Valida que ficha está ativa e aluno não está inativo. |

---

## Regras de Negócio

### Fluxo de Aprovação

```
Treinador:  Cadastro → AguardandoAprovacao → (admin aprova) → Ativo
Aluno:      Cadastro → AguardandoAprovacao → (treinador aprova vínculo) → Ativo
```

Toda aprovação e inativação gera um registro em `LogAprovacao`.

### Limites de Plano

| Serviço | O que valida | Quando |
|---------|-------------|--------|
| `LimiteTreinadorService` | `PlanoTreinador.MaxAlunos` vs alunos ativos | Ao aprovar vínculo de aluno |
| `LimiteFichasService` | `PacoteAluno.MaxFichas` vs fichas ativas | Ao vincular ficha a aluno |

Exceder o limite lança `LimiteAlunosAtingidoException` ou `LimiteFichasAtingidoException` (422).

### Cascata de Inativação

| Ação | Efeito em cascata |
|------|-------------------|
| Inativar `Treinador` | Inativa todos `VinculoTreinadorAluno` ativos → inativa todos `TreinoAluno` desses vínculos |
| Desvincular `VinculoTreinadorAluno` | Inativa todos `TreinoAluno` do par (treinador × aluno) |

### Isolamento de Dados

Não existe `TenantId`. O isolamento é garantido por `TreinadorId` em cada entidade de negócio. Os handlers validam `IUserContext.PerfilId` contra o dono do recurso antes de qualquer operação, lançando `AcessoNegadoException` (403) em caso de violação.

---

## Tratamento de Erros

O `GlobalExceptionHandler` implementa **RFC 7807** (`ProblemDetails`) e mapeia exceções de domínio para status HTTP:

| Exceção | Status | Title |
|---------|--------|-------|
| `CredenciaisInvalidasException` | 401 | Não autorizado |
| `AlunoNaoEncontradoException` | 404 | Não encontrado |
| `TreinadorNaoEncontradoException` | 404 | Não encontrado |
| `TreinoNaoEncontradoException` | 404 | Não encontrado |
| `VinculoNaoEncontradoException` | 404 | Não encontrado |
| `ExercicioNaoEncontradoException` | 404 | Não encontrado |
| `AlunoInativoException` | 403 | Inativo |
| `AcessoNegadoException` | 403 | Acesso negado |
| `DomainException` (e subclasses) | 422 | Erro de domínio |
| `ValidationException` (FluentValidation) | 400 | Erro de validação (com campo → erros) |
| Qualquer outra | 500 | Erro interno (mensagem interna não exposta) |

Erros 4xx são logados em `Warning`. Erros 5xx são logados em `Error` com stack trace.

---

## Configuração

### appsettings

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

### Secrets (User Secrets ou variáveis de ambiente)

```bash
dotnet user-secrets set "Auth:JwtSecret" "<chave-hmac>"         --project forzion.tech.Api
dotnet user-secrets set "Auth:JwtIssuer" "forzion.tech"         --project forzion.tech.Api
dotnet user-secrets set "Auth:JwtAudience" "forzion.tech"       --project forzion.tech.Api
dotnet user-secrets set "ConnectionStrings:AppConnection" "<pg>" --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminEmail" "admin@forzion.tech"  --project forzion.tech.Api
dotnet user-secrets set "Seed:AdminPassword" "<senha>"          --project forzion.tech.Api
```

User Secrets ID: `049d65fb-2c12-483c-b56e-cb753632d11f`

### Ambientes

| Ambiente | Schema | Swagger | Seeder |
|----------|--------|---------|--------|
| `Development` | `homolog` | ✅ | ✅ |
| `Homolog` | `homolog` | ✅ | ✅ |
| `Production` | `public` | ❌ | ❌ |
| `Test` | (em memória / mock) | ❌ | ❌ |

---

## Testes

```
329 testes | 0 falhas

Domain/          → entidades, value objects, exceções
Application/     → handlers (unit), services de limite
Infrastructure/  → JwtService
Api/Endpoints/   → endpoints via WebApplicationFactory (auth, status codes, isolamento)
Integration/     → fluxo completo
```

Padrões adotados:
- `HandleAsync` declarado como `virtual` para permitir mock via Moq
- `It.IsAny<CancellationToken>()` em todos os setups de repositório
- `CallBase = true` em mocks de handlers que executam validação própria
- Handlers de endpoint testados com `TestAuthHandler` customizado
