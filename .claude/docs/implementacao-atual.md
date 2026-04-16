# implementacao-atual.md
atualizado: 2026-04-16 | branch: backend

## visão geral
ASP.NET Core 8.0 Minimal API, Clean Architecture. Módulos de Usuários e Alunos completos com testes. Módulo Treinos/Exercícios: Domain ✅ Infrastructure ✅ Application ✅ API+Testes pendentes.
Endpoints Usuários: POST /usuarios/registrar, GET /usuarios/me, PATCH /usuarios/me, PATCH /usuarios/{id}/status
Endpoints Alunos: POST /alunos, GET /alunos, GET /alunos/{id}, PATCH /alunos/{id}, PATCH /alunos/{id}/status
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

Aluno:
- Id Guid (gerado no app, não no banco)
- Nome string (max 100) | Email string? (max 256, normalizado lowercase) | Telefone string? (max 20)
- Status AlunoStatus enum — Ativo|Inativo, salvo como string
- TenantId Guid FK | TreinadorId Guid FK→usuarios(id)
- CreatedAt DateTime UTC | UpdatedAt DateTime? UTC
- fábrica: Aluno.Criar(nome, tenantId, treinadorId, email?, telefone?) — seta Status=Ativo
- invariantes: nome obrigatório max 100, tenantId não vazio, treinadorId não vazio
- Atualizar(nome?, email?, telefone?): atualiza não-nulos, seta UpdatedAt; string vazia limpa email/telefone
- AlterarStatus(AlunoStatus): seta Status e UpdatedAt
- Nota: Email de Aluno é string simples (não VO) — validação básica (contains @, max 256)

Exercicio:
- Id Guid | Nome string (max 100) | GrupoMuscular GrupoMuscular enum | Descricao string? (max 500)
- TenantId Guid | CreatedAt DateTime UTC | UpdatedAt DateTime? UTC
- fábrica: Exercicio.Criar(nome, grupoMuscular, tenantId, descricao?)
- Atualizar(nome?, grupoMuscular?, descricao?): atualiza não-nulos, seta UpdatedAt; string vazia limpa descricao

Treino:
- Id Guid | Nome string (max 100) | ObjetivoTreino enum | TenantId Guid | TreinadorId Guid
- Exercicios IReadOnlyList<TreinoExercicio> (coleção interna)
- CreatedAt DateTime UTC | UpdatedAt DateTime? UTC
- fábrica: Treino.Criar(nome, objetivo, tenantId, treinadorId)
- Atualizar(nome?, objetivo?) | AdicionarExercicio(...) | RemoverExercicio(treinoExercicioId) | Duplicar()
- Duplicar() → nova instância com nome "(cópia)", mesmos exercícios, novas chaves

TreinoAluno:
- Id Guid | TreinoId Guid | AlunoId Guid | Status TreinoAlunoStatus | CreatedAt DateTime UTC | UpdatedAt DateTime? UTC
- fábrica: TreinoAluno.Criar(treinoId, alunoId) — sem TenantId (isolamento via Treino.TenantId)
- vínculo permanente (histórico) — nunca deletado | AlterarStatus(TreinoAlunoStatus)

TreinoExercicio:
- Id Guid | TreinoId Guid | ExercicioId Guid | Series int | Repeticoes int | Carga decimal? | Descanso int? | Ordem int
- Exercicio Exercicio (navegação) — carregada via ThenInclude quando necessário
- criado via Treino.AdicionarExercicio() (factory internal)
- AlterarOrdem(int) internal — usado por Treino.ReordenarExercicios()

ExecucaoTreino:
- Id Guid | TreinoId Guid | AlunoId Guid | TenantId Guid | DataExecucao DateTime | Observacao string? | CreatedAt DateTime UTC
- Exercicios IReadOnlyList<ExecucaoExercicio> (backing field _exercicios)
- fábrica: ExecucaoTreino.Criar(treinoId, alunoId, tenantId, dataExecucao, observacao?)
- AdicionarExercicio(treinoExercicioId, series, repeticoes, carga?, obs?)

ExecucaoExercicio:
- Id Guid | ExecucaoTreinoId Guid | TreinoExercicioId Guid
- SeriesExecutadas int | RepeticoesExecutadas int | CargaExecutada decimal? | Observacao string?
- criado via ExecucaoTreino.AdicionarExercicio() (factory internal)

### Enums
Role: Admin=0, Trainer=1 | UsuarioStatus: Ativo=0, Inativo=1 — salvos como string
AlunoStatus: Ativo=0, Inativo=1 — salvo como string
ObjetivoTreino: Hipertrofia, Forca, Resistencia, Emagrecimento, Reabilitacao
GrupoMuscular: Peito, Costas, Ombro, Biceps, Triceps, Pernas, Gluteos, Core, FullBody
TreinoAlunoStatus: Ativo=0, Inativo=1

### Exceções (todas herdam DomainException)
DomainException → 422 | UsuarioJaRegistradoException → 409 | UsuarioNaoEncontradoException → 404
UsuarioInativoException → 403 | AcessoNegadoException → 403 | PlanoNaoEncontradoException → 422
AlunoNaoEncontradoException → 404 | AlunoInativoException → 403
TreinoNaoEncontradoException → 404 | TreinoExecutadoException → 422 | ExercicioNaoEncontradoException → 404

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
IAlunoRepository: ObterPorIdAsync(Guid, CT) | ListarAsync(tenantId, pagina, tamanhoPagina, CT) | AdicionarAsync(Aluno, CT) | InativarPorTreinadorAsync(treinadorId, CT)
ITreinoRepository: ObterPorIdAsync(Guid, CT) [tracked, inclui Exercicios] | ListarAsync(tenantId, pagina, tamanhoPagina, CT) | ListarPorAlunoAsync(tenantId, alunoId, pagina, tamanhoPagina, CT) [filtra via TreinoAluno.Status=Ativo] | AdicionarAsync(Treino, CT)
IExercicioRepository: ObterPorIdAsync(Guid, CT) | ListarAsync(tenantId, pagina, tamanhoPagina, CT) | AdicionarAsync(Exercicio, CT) | ExisteAsync(id, tenantId, CT)
ITreinoAlunoRepository: ObterAsync(treinoId, alunoId, CT) | AdicionarAsync(TreinoAluno, CT)
IExecucaoTreinoRepository: AdicionarAsync(ExecucaoTreino, CT) | ExisteParaTreinoAsync(treinoId, CT)

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
- Fluxo: ObterPorIdAsync(AdminId) → null → exc | Inativo → exc | Role≠Admin → AcessoNegadoException | ObterPorIdAsync(UsuarioId) → null → exc | AlterarStatus() | se Inativo → InativarPorTreinadorAsync(UsuarioId) [cascade] | Commit
- Requer que o requisitante seja Admin ativo (verificado via DB, não via JWT claim)
- Cascade: ao inativar Usuário, inativa todos os Alunos vinculados via treinador_id (ExecuteUpdateAsync)

RegistrarUsuario:
- Command: RegistrarUsuarioCommand(SupabaseId, Nome, Email, TenantNome)
- Response: RegistrarUsuarioResponse(UsuarioId, Nome, Email, Role, TenantId, TenantNome)
- Fluxo: ExisteAsync → UsuarioJaRegistradoException | ObterPlanoFreeAsync → PlanoNaoEncontradoException |
  Slug único (max 5 tentativas com sufixo GUID) | persiste Tenant | Email.Criar | persiste Usuario | Commit

CadastrarAluno:
- Command: CadastrarAlunoCommand(TenantId, TreinadorId, Nome, Email?, Telefone?)
- Response: AlunoResponse(AlunoId, Nome, Email, Telefone, Status, TenantId, TreinadorId, CreatedAt, UpdatedAt)
- Fluxo: ObterPorIdAsync(TreinadorId) → null → UsuarioNaoEncontradoException | TreinadorId.TenantId ≠ TenantId → AcessoNegadoException | Aluno.Criar | AdicionarAsync | Commit

ObterAluno:
- Query: ObterAlunoQuery(TenantId, AlunoId)
- Fluxo: ObterPorIdAsync → null → AlunoNaoEncontradoException | TenantId mismatch → AcessoNegadoException | retorna AlunoResponse

ListarAlunos:
- Query: ListarAlunosQuery(TenantId, Pagina, TamanhoPagina)
- Response: ListarAlunosResponse(Items, Total, Pagina, TamanhoPagina)
- Fluxo: ListarAsync(tenantId, pagina, tamanhoPagina) — paginação server-side, OrderBy Nome

AtualizarAluno:
- Command: AtualizarAlunoCommand(TenantId, AlunoId, Nome?, Email?, Telefone?)
- Fluxo: ObterPorIdAsync → null → exc | TenantId mismatch → AcessoNegadoException | Inativo → AlunoInativoException | Atualizar() | Commit

AlterarStatusAluno:
- Command: AlterarStatusAlunoCommand(TenantId, AdminId, AlunoId, NovoStatus)
- Fluxo: ObterPorIdAsync(AdminId) → null → exc | Inativo → UsuarioInativoException | Role≠Admin ou TenantId mismatch → AcessoNegadoException | ObterPorIdAsync(AlunoId) → null → exc | TenantId mismatch → AcessoNegadoException | AlterarStatus() | Commit

CriarTreino:
- Command: CriarTreinoCommand(TenantId, TreinadorId, AlunoId, Nome, ObjetivoTreino)
- Response: TreinoResponse(TreinoId, Nome, Objetivo, TenantId, TreinadorId, Exercicios[], CreatedAt, UpdatedAt)
- Fluxo: ObterPorIdAsync(AlunoId) → null → AlunoNaoEncontradoException | aluno.TenantId ≠ TenantId → AcessoNegadoException | Treino.Criar | TreinoAluno.Criar | persiste ambos | Commit
- TreinoExercicioResponse: TreinoExercicioId, ExercicioId, Series, Repeticoes, Carga, Descanso, Ordem (sem nome do exercício)

ObterTreino:
- Query: ObterTreinoQuery(TenantId, TreinoId)
- Fluxo: ObterPorIdAsync → null → TreinoNaoEncontradoException | TenantId mismatch → AcessoNegadoException | retorna TreinoResponse

ListarTreinos:
- Query: ListarTreinosQuery(TenantId, AlunoId, Pagina, TamanhoPagina)
- Response: ListarTreinosResponse(Items, Total, Pagina, TamanhoPagina)
- Fluxo: ListarPorAlunoAsync — filtra treinos onde TreinoAluno.AlunoId = AlunoId e Status = Ativo

AdicionarExercicio:
- Command: AdicionarExercicioCommand(TenantId, TreinoId, ExercicioId, Series, Repeticoes, Carga?, Descanso?)
- Fluxo: ObterPorIdAsync → null → exc | TenantId mismatch → exc | ExisteParaTreinoAsync → TreinoExecutadoException | ExisteAsync(ExercicioId, TenantId) → ExercicioNaoEncontradoException | Treino.AdicionarExercicio() | Commit

RemoverExercicio:
- Command: RemoverExercicioCommand(TenantId, TreinoId, TreinoExercicioId)
- Fluxo: ObterPorIdAsync → null → exc | TenantId mismatch → exc | ExisteParaTreinoAsync → TreinoExecutadoException | Treino.RemoverExercicio() | Commit

DuplicarTreino:
- Command: DuplicarTreinoCommand(TenantId, TreinadorId, TreinoId)
- Fluxo: ObterPorIdAsync → null → exc | TenantId mismatch → exc | original.Duplicar() | AdicionarAsync | Commit
- Cópia não cria TreinoAluno — treino existe sem vínculo de aluno

RegistrarExecucao:
- Command: RegistrarExecucaoCommand(TenantId, TreinoId, AlunoId, DataExecucao, Observacao?, Exercicios[RegistrarExecucaoItemCommand])
- Response: RegistrarExecucaoResponse(ExecucaoId, TreinoId, AlunoId, TenantId, DataExecucao, Observacao, CreatedAt)
- Fluxo: verifica treino + aluno (TenantId) | ExecucaoTreino.Criar | AdicionarExercicio para cada item | AdicionarAsync | Commit
- Após execução registrada, ExisteParaTreinoAsync passa a retornar true → bloqueia mutações no treino

CriarExercicio:
- Command: CriarExercicioCommand(TenantId, Nome, GrupoMuscular, Descricao?)
- Response: ExercicioResponse(ExercicioId, Nome, GrupoMuscular, Descricao, TenantId, CreatedAt, UpdatedAt)
- Fluxo: Exercicio.Criar | AdicionarAsync | Commit

ListarExercicios:
- Query: ListarExerciciosQuery(TenantId, Pagina, TamanhoPagina)
- Response: ListarExerciciosResponse(Items, Total, Pagina, TamanhoPagina)
- Fluxo: ListarAsync(tenantId, pagina, tamanhoPagina) — OrderBy Nome

## Infrastructure Layer

### AppDbContext
implementa IUnitOfWork (CommitAsync = SaveChangesAsync)
DbSets: Usuarios, Tenants, Planos, Alunos | HasDefaultSchema(_schema) | UseSnakeCaseNamingConvention

### Repositórios
UsuarioRepository: ObterPorIdAsync usa Include(u→u.Tenant) | ExisteAsync usa AnyAsync
TenantRepository: ObterPorIdAsync usa Include(t→t.Plano) | SlugExisteAsync usa AnyAsync
PlanoRepository: ObterPlanoFreeAsync usa FirstOrDefaultAsync(p→p.IsFree)
AlunoRepository: ObterPorIdAsync usa FirstOrDefaultAsync | ListarAsync filtra por tenantId, ordena por Nome, pagina | InativarPorTreinadorAsync usa ExecuteUpdateAsync (bulk, sem carregar entidades)

### Configurações Fluent API
usuarios: email HasConversion(e→e.Value, v→Email.Reconstituir(v)) | role/status HasConversion<string>()
tenants: slug HasConversion(s→s.Value, v→Slug.Reconstituir(v))
planos: seed Free(0001, 0.00, 5, true) + Pro(0002, 49.90, max_int, false)
alunos: status HasConversion<string>() | FK tenant_id OnDelete:Restrict | FK treinador_id→usuarios OnDelete:Restrict | índices em tenant_id e treinador_id

### migrations aplicadas
- 20260413223047_InitialCreate: cria planos, tenants, usuarios + índices + seed
- 20260413232230_AddPlanoIsFree: adiciona planos.is_free
- 20260414201833_EnriquecimentoDoDominio: narrowing nomes/email; foto_url, bio, updated_at
- 20260414211515_AddUsuarioStatus: adiciona usuarios.status (text, default 'Ativo')
- 20260414212447_FixUsuarioStatusDefault: corrige status='' → 'Ativo'
- 20260415213739_AddAlunos: cria tabela alunos + índices

### InfrastructureExtensions (todos Scoped)
TenantInterceptor | AppDbContext via AddScoped factory | IUnitOfWork→AppDbContext
IUsuarioRepository→UsuarioRepository | ITenantRepository→TenantRepository | IPlanoRepository→PlanoRepository
IAlunoRepository→AlunoRepository

## API Layer

### Program.cs — serviços (ordem)
1. GlobalExceptionHandler + ProblemDetails
2. AddSwagger (Bearer JWT)
3. AddJwtAuthentication (Auth:Authority, Auth:Audience)
4. ConfigureHttpJsonOptions com JsonStringEnumConverter
5. IHttpContextAccessor + ITenantContext→HttpTenantContext (Scoped)
6. AddInfrastructure (ignorado se ASPNETCORE_ENVIRONMENT=Test)
7. Handlers Usuários: RegistrarUsuarioHandler + ObterUsuarioAtualHandler + AtualizarUsuarioHandler + AlterarStatusUsuarioHandler (Scoped)
8. Handlers Alunos: CadastrarAlunoHandler + ObterAlunoHandler + ListarAlunosHandler + AtualizarAlunoHandler + AlterarStatusAlunoHandler (Scoped)
9. public partial class Program {} — expõe entry point para WebApplicationFactory

pipeline: UseSwaggerInNonProduction → UseExceptionHandler → UseHttpsRedirection → UseAuthentication → UseAuthorization → MapUsuarioEndpoints → MapAlunoEndpoints

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

### POST /alunos
RequireAuthorization. sub → TreinadorId. Body: {nome, email?, telefone?}
Validação: nome obrigatório max 100, email max 256 com @, telefone max 20
201: AlunoResponse, Location:/alunos/{id} | 400 | 401 | 403 | 422 | 500

### GET /alunos
RequireAuthorization. Query: pagina (default 1), tamanhoPagina (default 20, max 100)
200: ListarAlunosResponse{Items, Total, Pagina, TamanhoPagina} | 401 | 500

### GET /alunos/{id}
RequireAuthorization.
200: AlunoResponse | 401 | 403 | 404 | 500

### PATCH /alunos/{id}
RequireAuthorization. Body (todos opcionais): {nome?, email?, telefone?}
null = não atualizar | string vazia email/telefone = limpar
Validação: nome max 100 (não vazio se fornecido), email max 256 com @, telefone max 20
200: AlunoResponse | 400 | 401 | 403 | 404 | 422 | 500

### PATCH /alunos/{id}/status
RequireAuthorization. sub → AdminId. Body: {status: "Ativo"|"Inativo"}
Verifica via DB que o requisitante é Admin ativo do mesmo tenant.
200: AlunoResponse | 401 | 403 | 404 | 500

### GlobalExceptionHandler
UsuarioJaRegistradoException→409 | UsuarioNaoEncontradoException→404 | UsuarioInativoException→403
AcessoNegadoException→403 | DomainException→422 | outros→500
AlunoNaoEncontradoException→404 | AlunoInativoException→403
≥500: log Error | <500: log Warning | [LoggerMessage] source-generated

### AuthenticationExtensions
JwtBearer, MapInboundClaims=false, ValidateIssuerSigningKey=true
BuildDiagnosticEvents() com logs diagnóstico JWT (não-produção) — marcado [ExcludeFromCodeCoverage]

### HttpTenantContext
Lê claim tenant_id do JWT → Guid? TenantId. Usado pelo TenantInterceptor.

## Camada de Testes (forzion.tech.Tests)

### Estrutura
net8.0 | xUnit 2.9.3 | Moq 4.20.70 | FluentAssertions 6.12.0 | Mvc.Testing 8.0.11 | coverlet.collector
267 testes | 0 falhas

### Arquivos de teste
Tests/Domain/ValueObjects/EmailTests.cs — Email.Criar/Reconstituir/ToString (10 testes)
Tests/Domain/ValueObjects/SlugTests.cs — Slug.FromNome/Reconstituir (9 testes)
Tests/Domain/Entities/UsuarioTests.cs — Criar/Atualizar/AlterarStatus (29 testes)
Tests/Domain/Entities/TenantTests.cs — Criar invariantes (8 testes)
Tests/Domain/Entities/PlanoTests.cs — Criar/CriarComId/PlanoIds (6 testes)
Tests/Domain/Entities/AlunoTests.cs — Criar/Atualizar/AlterarStatus (18 testes)
Tests/Domain/Entities/ExercicioTests.cs — Criar/Atualizar (7 testes)
Tests/Domain/Entities/TreinoTests.cs — Criar/Atualizar/AdicionarExercicio/Duplicar (15 testes)
Tests/Domain/Entities/TreinoAlunoTests.cs — Criar invariantes (4 testes)
Tests/Domain/Entities/ExecucaoTreinoTests.cs — Criar/ExecucaoExercicio (11 testes)
Tests/Domain/Exceptions/DomainExceptionTests.cs — todos construtores de todas as exceções (21 testes)
Tests/Application/ObterUsuarioAtualHandlerTests.cs — 4 testes
Tests/Application/RegistrarUsuarioHandlerTests.cs — 7 testes
Tests/Application/AtualizarUsuarioHandlerTests.cs — 5 testes
Tests/Application/AlterarStatusUsuarioHandlerTests.cs — 6 testes
Tests/Application/CadastrarAlunoHandlerTests.cs — 4 testes
Tests/Application/ObterAlunoHandlerTests.cs — 4 testes
Tests/Application/ListarAlunosHandlerTests.cs — 3 testes
Tests/Application/AtualizarAlunoHandlerTests.cs — 5 testes
Tests/Application/AlterarStatusAlunoHandlerTests.cs — 6 testes
Tests/Api/GlobalExceptionHandlerTests.cs — mapeamentos + logging (9 testes)
Tests/Api/Context/HttpTenantContextTests.cs — claims válido/inválido/nulo (5 testes)
Tests/Api/Endpoints/UsuarioEndpointsTests.cs — integração via WebApplicationFactory (24 testes)
Tests/Api/Endpoints/AlunoEndpointsTests.cs — integração via WebApplicationFactory (19 testes)

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
- Aluno.Email é string simples (não VO) — Exercicio tem tenantId (biblioteca por tenant, não global)
- Imutabilidade de treino verificada no Application layer (handler consulta ExecucaoTreino antes de mutar)

## o que não existe ainda
- Treinos — Infrastructure, Application, API + Testes
- Exercícios — Infrastructure, Application, API + Testes
- Gestão de membros do tenant — convite de treinadores (SendGrid)
- Assinaturas — integração Stripe, controle de limites por plano
- Docker / CI/CD
