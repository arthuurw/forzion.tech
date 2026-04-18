# forzion.tech — Backend API

ASP.NET Core 8.0 Web API para a plataforma forzion.tech, uma solução de gestão de treinos para personal trainers (Treinadores) e seus alunos.

**Status**: ✅ **Refatoração de Domínio Concluída** | **274/274 testes passando** | **Arquitetura Clean 100%** | **Isolamento por Treinador**

## Stack

- **Framework**: ASP.NET Core 8.0 / C# 12 com nullable reference types
- **Banco**: PostgreSQL (Supabase/Local)
- **ORM**: Entity Framework Core 8.0
- **Auth**: JWT Customizado (HMAC-SHA256) com senhas em BCrypt. Sem dependência de provedores externos no core.
- **CORS**: Habilitado e configurável por ambiente ✅
- **Health Check**: `GET /health` para monitoramento ✅
- **Docs**: Swagger/OpenAPI em `/swagger` (Non-Production apenas)
- **Arquitetura**: Clean Architecture (Api, Application, Domain, Infrastructure)
- **Testing**: 274 testes (xUnit + Moq + FluentAssertions + WebApplicationFactory)

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

# Migrations — gerar (frequênciafresh após refatoração)
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef migrations add <Nome> --project forzion.tech.Infrastructure --startup-project forzion.tech.Api

# Migrations — aplicar homolog
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef database update --project forzion.tech.Infrastructure --startup-project forzion.tech.Api
```

## Estrutura do Projeto

```
forzion.tech/
├── forzion.tech.Api/              # Endpoints (Minimal APIs), middlewares, IUserContext (HttpUserContext)
├── forzion.tech.Application/      # Use Cases (Handlers), Interfaces, Services (Limites, JWT, Password)
├── forzion.tech.Domain/           # Entidades (Conta, Treinador, Aluno, Vinculo, etc.), VOs, Enums, Exceções
├── forzion.tech.Infrastructure/   # EF Core (DbContext, Configs), Repositórios, Migrações, Seeders
├── forzion.tech.Tests/            # Testes (Unitários de Domain/Application + Integração de API)
└── forzion.tech.slnx
```

## Endpoints Principais

| Categoria | Método | Rota | Descrição |
|-----------|--------|------|-----------|
| **Auth** | `POST` | `/auth/login` | Autentica usuário e retorna JWT com claims de Perfil. |
| **Auth** | `POST` | `/auth/register/treinador` | Cadastro inicial de treinador (aguarda aprovação admin). |
| **Auth** | `POST` | `/auth/register/aluno` | Cadastro de aluno com vínculo pendente a um treinador. |
| **Admin** | `POST` | `/admin/treinadores/{id}/aprovar` | Aprova um treinador no sistema. |
| **Treinador**| `POST` | `/treinador/vinculos/{id}/aprovar` | Aprova vínculo de aluno e define Pacote de Fichas. |
| **Treinador**| `GET` | `/treinador/alunos` | Lista alunos vinculados ao treinador. |
| **Treinador**| `POST` | `/treinador/pacotes` | Cria pacotes de fichas (ex: "3 Fichas/mês"). |
| **Treinador**| `POST` | `/treinador/exercicios/copiar` | Copia exercício da biblioteca global para a privada. |
| **Treinos** | `POST` | `/treinos` | Cria uma nova ficha de treino. |
| **Treinos** | `POST` | `/treinos/{id}/vincular-aluno`| Vincula uma ficha existente a um aluno (valida limites). |
| **Aluno** | `GET` | `/aluno/fichas` | Lista as fichas ativas vinculadas ao aluno logado. |
| **Aluno** | `POST` | `/aluno/execucoes` | Registra a execução (treino realizado) pelo aluno. |

## Arquitetura e Isolamento

O projeto foi totalmente refatorado para remover o conceito de "Academia/Tenant" em favor de um isolamento centrado no **Treinador**:
- **Isolamento de Dados**: Toda entidade de negócio possui um `TreinadorId`. O acesso é validado via `IUserContext` em tempo de execução nos Handlers.
- **Hierarquia de Contas**:
    - **SystemAdmin**: Gerencia planos globais e aprova treinadores.
    - **Treinador**: Gerencia sua biblioteca de exercícios, pacotes de alunos e fichas de treino.
    - **Aluno**: Visualiza suas fichas e registra execuções.
- **Vínculos**: Um aluno só pode treinar com um treinador ativo por vez. A inativação de um treinador inativa em cascata todos os seus vínculos e fichas.

## Qualidade e Segurança

- **Segurança**: Senhas criptografadas com **BCrypt**. JWT assinado com chave simétrica rotacionável.
- **Validação de Limites**:
    - `LimiteTreinadorService`: Bloqueia novos alunos se o treinador atingir o teto do seu plano.
    - `LimiteFichasService`: Bloqueia novos vínculos de fichas se o aluno atingir o limite do seu pacote contratado.
- **Testes**: ✅ 274/274 testes automatizados cobrindo fluxos críticos de negócio e segurança.
- **Logs**: Auditoria via `LogAprovacao` para todas as ações administrativas e de vínculo.

## Desenvolvimento e Seed

Em ambientes de `Development` e `Homolog`, o sistema executa automaticamente o `DataSeeder` ao iniciar, criando uma conta administrativa padrão (SuperAdmin) baseada nas chaves `Seed:AdminEmail` e `Seed:AdminPassword`.
