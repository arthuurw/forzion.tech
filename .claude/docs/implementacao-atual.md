# implementacao-atual.md
atualizado: 2026-04-15 | branch: backend

## visão geral
ASP.NET Core 8.0 Minimal API, Clean Architecture. Módulo de usuários implementado com testes.
Endpoints: POST /usuarios/registrar, GET /usuarios/me, PATCH /usuarios/me, PATCH /usuarios/{id}/status
Banco: PostgreSQL (Supabase) | Auth: Supabase JWT | Portas: HTTP 5230, HTTPS 7220

## projetos e dependências
forzion.tech.Api → Application, Infrastructure
forzion.tech.Application → Domain
forzion.tech.Infrastructure → Domain, Application
forzion.tech.Domain → (nenhuma)
forzion.tech.Tests → Api, Application, Domain (net8.0, xUnit + Moq + FluentAssertions + Mvc.Testing)

pacotes principais:
- Api: JwtBearer 8.0.11, Swashbuckle 6.6.2
- Infrastructure: Npgsql.EFCore.PostgreSQL 8.0.11, EFCore.NamingConventions 8.0.3
- Application: Microsoft.Extensions.Logging.Abstractions 8.0.*
- Tests: coverlet.collector, Microsoft.AspNetCore.Mvc.Testing 8.0.11

## Domain Layer

### Value Objects (Domain/ValueObjects/)

Email:
- string Value
- Email.Criar(string): valida presença, max 256, regex ^[^@\s]+@[^@\s]+\.[^@\s]+$ (timeout 100ms), normaliza lowercase+trim, lança DomainException
- Email.Reconstituir(string): sem validação (EF Core + testes)

Slug:
- string Value
- Slug.FromNome(string): lowercase, espaços→-, remove acentos (ã→a, â→a, á→a, à→a, ê→e, é→e, í→i, õ→o, ô→o, ó→o, ú→u, ü→u, ç→c)
- Slug.Reconstituir(string): sem transformação (EF Core + sufixo de unicidade)

### Entidades (Domain/Entities/)

Usuario:
- Id Guid — UUID Supabase Auth (PK)
- Nome string (max 100)
- Email Email VO (max 256, unique, lowercase)
- Role Role enum — Admin|Trainer, salvo como string
- Status UsuarioStatus enum — Ativo|Inativo, salvo como string
- TenantId Guid FK | Tenant Tenant (navegação — sempre preenchido via Include)
- FotoUrl string? (max 500, scheme obrigatório http/https) | Bio string? (max 500)
- CreatedAt DateTime UTC | UpdatedAt DateTime? UTC
- fábrica: Usuario.Criar(supabaseId, nome, email, tenantId, role=Admin) — seta Status=Ativo
- invariantes: supabaseId não vazio, nome obrigatório max 100, tenantId não vazio
- Atualizar(nome?, fotoUrl?, bio?): atualiza não-nulos, seta UpdatedAt; string vazia limpa fotoUrl/bio; fotoUrl valida scheme http/https (DomainException se inválido)
- AlterarStatus(UsuarioStatus): seta Status e UpdatedAt

Tenant:
- Id Guid | Nome string (max 100) | Slug Slug VO (max 200, unique)
- PlanoId Guid FK | Plano Plano (navegação)
- CreatedAt DateTime UTC | UpdatedAt DateTime? UTC
- fábrica: Tenant.Criar(nome, slug, planoId) — invariantes: nome max 100, planoId não vazio

Plano:
- Id Guid | Nome string | Preco decimal(10,2) | LimiteAlunos int | IsFree bool
- fábricas: Plano.Criar(nome, preco, limiteAlunos, isFree) | Plano.CriarComId(id, ...)

### Enums
Role: Admin=0, Trainer=1 | UsuarioStatus: Ativo=0, Inativo=1 — ambos salvos como string

### Exceções (todas herdam DomainException)
DomainException → 422 | UsuarioJaRegistradoException → 409 | UsuarioNaoEncontradoException → 404
UsuarioInativoException → 403 | AcessoNegadoException → 403 | PlanoNaoEncontradoException → 422

### Constantes
PlanoIds.FreeId = "00000000-0000-0000-0000-000000000001"
PlanoIds.ProId  = "00000000-0000-0000-0000-000000000002"

## Application Layer

### Interfaces
ITenantContext: Guid? TenantId — implementado por HttpTenantContext (claim tenant_id do JWT)
IUnitOfWork: Task CommitAsync(CancellationToken) — implementado por AppDbContext

IUsuarioRepository: ObterPorIdAsync(Guid, CT) | ExisteAsync(Guid, CT) | AdicionarAsync(Usuario, CT)
ITenantRepository: ObterPorIdAsync(Guid, CT) | SlugExisteAsync(Slug, CT) | AdicionarAsync(Tenant, CT)
IPlanoRepository: ObterPorIdAsync(Guid, CT) | ObterPlanoFreeAsync(CT)

### Handlers — IMPORTANTE: HandleAsync é virtual (necessário para Moq nos testes)

ObterUsuarioAtual:
- Query: ObterUsuarioAtualQuery(Guid UsuarioId)
- Response: ObterUsuarioAtualResponse(UsuarioId, Nome, Email, Role, Status, TenantId, TenantNome, FotoUrl, Bio, CreatedAt, UpdatedAt)
- Fluxo: ObterPorIdAsync → null → UsuarioNaoEncontradoException | Inativo → UsuarioInativoException | retorna response
- Acessa usuario.Tenant.Nome (garantido pelo Include no repositório)

AtualizarUsuario:
- Command: AtualizarUsuarioCommand(UsuarioId, Nome?, FotoUrl?, Bio?)
- Response: ObterUsuarioAtualResponse (reutilizado)
- Fluxo: ObterPorIdAsync → null → exc | Inativo → exc | Atualizar() | Commit | retorna response
- Status NÃO é alterável por este handler — usar AlterarStatusUsuario

AlterarStatusUsuario:
- Command: AlterarStatusUsuarioCommand(AdminId, UsuarioId, NovoStatus)
- Response: ObterUsuarioAtualResponse (reutilizado)
- Fluxo: ObterPorIdAsync(AdminId) → null → exc | Inativo → exc | Role≠Admin → AcessoNegadoException | ObterPorIdAsync(UsuarioId) → null → exc | AlterarStatus() | Commit | retorna response
- Requer que o requisitante seja Admin ativo (verificado via DB, não via JWT claim)

RegistrarUsuario:
- Command: RegistrarUsuarioCommand(SupabaseId, Nome, Email, TenantNome)
- Response: RegistrarUsuarioResponse(UsuarioId, Nome, Email, Role, TenantId, TenantNome)
- Fluxo: ExisteAsync → UsuarioJaRegistradoException | ObterPlanoFreeAsync → PlanoNaoEncontradoException |
  Slug único (max 5 tentativas com sufixo GUID) | persiste Tenant | Email.Criar | persiste Usuario | Commit

## Infrastructure Layer

### AppDbContext
implementa IUnitOfWork (CommitAsync = SaveChangesAsync)
DbSets: Usuarios, Tenants, Planos | HasDefaultSchema(_schema) | UseSnakeCaseNamingConvention

### Repositórios
UsuarioRepository: ObterPorIdAsync usa Include(u→u.Tenant) | ExisteAsync usa AnyAsync
TenantRepository: ObterPorIdAsync usa Include(t→t.Plano) | SlugExisteAsync usa AnyAsync
PlanoRepository: ObterPlanoFreeAsync usa FirstOrDefaultAsync(p→p.IsFree)

### Configurações Fluent API
usuarios: email HasConversion(e→e.Value, v→Email.Reconstituir(v)) | role/status HasConversion<string>()
tenants: slug HasConversion(s→s.Value, v→Slug.Reconstituir(v))
planos: seed Free(0001, 0.00, 5, true) + Pro(0002, 49.90, max_int, false)

### migrations aplicadas
- 20260413223047_InitialCreate: cria planos, tenants, usuarios + índices + seed
- 20260413232230_AddPlanoIsFree: adiciona planos.is_free
- 20260414201833_EnriquecimentoDoDominio: narrowing nomes/email; foto_url, bio, updated_at
- 20260414211515_AddUsuarioStatus: adiciona usuarios.status (text, default 'Ativo')
- 20260414212447_FixUsuarioStatusDefault: corrige status='' → 'Ativo'

### InfrastructureExtensions (todos Scoped)
TenantInterceptor | AppDbContext via AddScoped factory | IUnitOfWork→AppDbContext
IUsuarioRepository→UsuarioRepository | ITenantRepository→TenantRepository | IPlanoRepository→PlanoRepository

## API Layer

### Program.cs — serviços (ordem)
1. GlobalExceptionHandler + ProblemDetails
2. AddSwagger (Bearer JWT)
3. AddJwtAuthentication (Auth:Authority, Auth:Audience)
4. ConfigureHttpJsonOptions com JsonStringEnumConverter
5. IHttpContextAccessor + ITenantContext→HttpTenantContext (Scoped)
6. AddInfrastructure (ignorado se ASPNETCORE_ENVIRONMENT=Test)
7. RegistrarUsuarioHandler + ObterUsuarioAtualHandler + AtualizarUsuarioHandler + AlterarStatusUsuarioHandler (Scoped)
8. public partial class Program {} — expõe entry point para WebApplicationFactory

pipeline: UseSwaggerInNonProduction → UseExceptionHandler → UseHttpsRedirection → UseAuthentication → UseAuthorization → MapUsuarioEndpoints

### GET /usuarios/me
RequireAuthorization. sub → Guid UsuarioId.
200: ObterUsuarioAtualResponse | 401 | 403: inativo | 404: não encontrado | 500

### PATCH /usuarios/me
RequireAuthorization. Body (todos opcionais): {nome?, fotoUrl?, bio?}
null = não atualizar | string vazia fotoUrl/bio = limpar
Validação: nome max 100 (não vazio se fornecido), fotoUrl max 500 e scheme http/https, bio max 500
200: ObterUsuarioAtualResponse | 400: ValidationProblem | 401 | 403 | 404 | 422 | 500

### PATCH /usuarios/{id}/status
RequireAuthorization. Body: {status: "Ativo"|"Inativo"}
sub → AdminId (Guid). Verifica via DB que o requisitante é Admin ativo antes de alterar.
200: ObterUsuarioAtualResponse | 401 | 403: inativo ou não-admin | 404 | 500

### POST /usuarios/registrar
RequireAuthorization. sub → Guid SupabaseId.
Validação: nome(obrigatório max 100), email(EmailAddress max 256), tenantNome(obrigatório max 100)
201: RegistrarUsuarioResponse, Location:/usuarios/{id} | 400 | 401 | 409 | 422 | 500

### GlobalExceptionHandler
UsuarioJaRegistradoException→409 | UsuarioNaoEncontradoException→404 | UsuarioInativoException→403
AcessoNegadoException→403 | DomainException→422 | outros→500 | ≥500: log Error | <500: log Warning | [LoggerMessage] source-generated

### AuthenticationExtensions
JwtBearer, MapInboundClaims=false, ValidateIssuerSigningKey=true
BuildDiagnosticEvents() com logs diagnóstico JWT (não-produção) — marcado [ExcludeFromCodeCoverage]

### HttpTenantContext
Lê claim tenant_id do JWT → Guid? TenantId. Usado pelo TenantInterceptor.

## Camada de Testes (forzion.tech.Tests)

### Estrutura
net8.0 | xUnit 2.9.3 | Moq 4.20.70 | FluentAssertions 6.12.0 | Mvc.Testing 8.0.11 | coverlet.collector
157 testes | 0 falhas

### Arquivos de teste
Tests/Domain/ValueObjects/EmailTests.cs — Email.Criar/Reconstituir/ToString (10 testes)
Tests/Domain/ValueObjects/SlugTests.cs — Slug.FromNome/Reconstituir (9 testes)
Tests/Domain/Entities/UsuarioTests.cs — Criar/Atualizar/AlterarStatus (29 testes — +9 FotoUrl scheme)
Tests/Domain/Entities/TenantTests.cs — Criar invariantes (8 testes)
Tests/Domain/Entities/PlanoTests.cs — Criar/CriarComId/PlanoIds (6 testes)
Tests/Domain/Exceptions/DomainExceptionTests.cs — todos construtores de todas as exceções (15 testes)
Tests/Application/ObterUsuarioAtualHandlerTests.cs — 4 testes
Tests/Application/RegistrarUsuarioHandlerTests.cs — 7 testes
Tests/Application/AtualizarUsuarioHandlerTests.cs — 5 testes (removido teste de Status)
Tests/Application/AlterarStatusUsuarioHandlerTests.cs — 6 testes (novo)
Tests/Api/GlobalExceptionHandlerTests.cs — mapeamentos + logging (9 testes)
Tests/Api/Context/HttpTenantContextTests.cs — claims válido/inválido/nulo (5 testes)
Tests/Api/Endpoints/UsuarioEndpointsTests.cs — integração via WebApplicationFactory (24 testes — +8 novos)

### Padrões de teste
- Handlers mockados via Mock<ConcreteHandler>(deps_mockados) — exige HandleAsync virtual
- Tenant navigation preenchido via reflection: typeof(Usuario).GetProperty("Tenant")!.SetValue(usuario, tenant)
- ILogger mock: _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true) — necessário para [LoggerMessage]
- CancellationToken em Moq: sempre It.IsAny<CancellationToken>() (nunca default — HTTP pipeline envia token real)
- WebApplicationFactory: ASPNETCORE_ENVIRONMENT=Test ignora AddInfrastructure; handlers substituídos por mocks
- Cobertura: forzion.tech.Tests/coverage.runsettings exclui [forzion.tech.Infrastructure]*

## decisões de implementação
- Minimal API: MapGroup + MapPost/MapGet/MapPatch
- Validação manual no endpoint (sem FluentValidation)
- Handler direto injetado (sem MediatR) com HandleAsync virtual
- UsuarioId = SupabaseId (sub claim) — sem indireção
- Slug gerado automaticamente, nunca fornecido pelo cliente
- Plano Free automático no cadastro (Role padrão Admin)
- ConfigureAwait(false) em todos os repositórios
- snake_case em todas as colunas

## o que não existe ainda
- módulo Alunos, Treinos, Exercícios, Assinaturas (Stripe), Convites (SendGrid)
- gestão de membros do tenant
- cascade de inativação: AlterarStatus em Usuario deve inativar Alunos vinculados (módulo Alunos)
- Docker / CI/CD
