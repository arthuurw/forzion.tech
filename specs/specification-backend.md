# specification-backend — backend (forzion.tech)

DOC PARA AGENTES. Fonte de verdade da arquitetura do backend (.NET 8, Clean Architecture + DDD). Camadas Application/Api/Infrastructure. Formato denso. Cross-ref: [specification-model] (domínio: entidades/eventos/invariantes), [specification-db] (EF/persistência), [specification-stripe] (pagamento), [specification-email] (notificações), [specification-infrastructure] (deploy/nginx/CI).

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA de mudança em: camadas, padrão Result, UnitOfWork/dispatch de eventos, validação, DI, middleware, filtros, convenções de endpoint, repositórios, auth, rate limiting.
- Vive em `specs/` (versionado; commitar). NÃO duplicar domínio ([specification-model]) nem schema ([specification-db]).

## 1. ARQUITETURA / CAMADAS
Clean Architecture + DDD. Projetos: `forzion.tech.Domain` (núcleo + `Shared/` Result/Error), `Application` (use cases/interfaces), `Infrastructure` (EF/repos/integrações/handlers de evento), `Api` (endpoints/middleware/DI), `Tests`.
- Direção de dependência (travada por NetArchTest em `Tests/Architecture/LayeringTests.cs`):
  - Domain → nada (nem Application, nem Infrastructure, nem Api, nem EF Core).
  - Application → só Domain (proibido depender de Infrastructure, Api, EF Core).
  - Infrastructure → Application + Domain (proibido depender de Api).
  - Api → Application + Infrastructure + Domain (composição/wiring).
- `ConventionTests.cs`: entidades de domínio sem setter público, construídas só por factory estática (`Criar`; exceção `LogAprovacao.Registrar`); handlers seguem sufixo/namespace.
- Referência de estrutura macro: AGENTS.md ESTRUTURA.

## 2. APPLICATION LAYER

### Result pattern (`forzion.tech.Domain/Shared/`)
- `Result`/`Result<T>`/`Error` vivem em `forzion.tech.Domain.Shared` (movidos de Application — o DOMÍNIO usa Result para invariantes; Domain não pode depender de Application). `Result`: `IsSuccess`/`IsFailure`/`Error?`. Factories `Success()`, `Failure(Error)`, `Success<T>(v)`, `Failure<T>(Error)`. `Result<T>.Value` lança `InvalidOperationException` se acessado em falha.
- `Error(string Code, string Message)` record; helper `Error.Business(msg)` → code `"business_error"`. Catálogos de domínio em `Domain/Shared/Errors/*.cs` (code `<agg>.<motivo>`).
- Mapeamento HTTP no Api: `ResultExtensions.ToProblemResult()` → `Results.Problem(detail=Error.Message, status=422)`. Endpoints checam `result.IsFailure` e ou chamam `ToProblemResult()` ou tratam codes específicos (ex.: `CancelarMinhaAssinaturaAlunoHandler.AssinaturaNaoEncontradaErrorCode` → 404).
- POLÍTICA DE ERRO (regra de arquitetura — erro de NEGÓCIO usa Result; demais erros usam exception):
  1. DOMÍNIO retorna `Result`/`Result<T>` para toda invariante de negócio (NUNCA lança `DomainException` p/ regra de negócio).
  2. HANDLERS retornam `Result`/`Result<T>` (`Task<Result>`/`Task<Result<TResponse>>`) e propagam a falha de negócio do domínio direto (`return Result.Failure(...)`/`Result.Failure<T>(...)`). Não há mais handler que re-lança `DomainException` p/ traduzir falha de negócio. Endpoints desembrulham: `if (result.IsFailure) return result.ToProblemResult();` (422) senão `Results.Ok/Created(result.Value)`.
  3. EXCEPTION só p/ NÃO-negócio (control-flow cross-cutting + infra/programação), lançada no handler/contexto e mapeada pelo `GlobalExceptionHandler`: `*NaoEncontradoException` (lookup miss → 404), `AcessoNegadoException` (authz → 403), `EmailNaoVerificadoException`/`CredenciaisInvalidasException` (401/403), `EmailJaCadastradoException`/`AlunoJaVinculadoException` (409), `LimiteAlunosAtingidoException`, `AlunoInativoException`; `ValidationException` (FluentValidation → 400); `ArgumentNullException.ThrowIfNull` (erro de programação).

### Use cases / handlers (CQRS-like)
Um handler por use case, organizado em `Application/UseCases/<Area>/<UseCase>/`. Pasta típica: `<X>Command.cs`/`<X>Query.cs` + `<X>Handler.cs` (+ `<X>Validator.cs`, `<X>Response.cs` opcionais). Convenções:
- Handler = classe com ctor primário (injeção), método público `virtual Task[<...>] HandleAsync(command, ct)` (virtual → mockável/override em testes). Guard `ArgumentNullException.ThrowIfNull(command)`; padrão comum: `HandleAsync` faz guard + delega a `HandleAsyncCore` privado.
- `TimeProvider` SEMPRE injetado para tempo (nunca `DateTime.UtcNow` direto na Application; testes injetam `FakeTimeProvider`).
- Persistência via interfaces de repositório + `IUnitOfWork.CommitAsync`. Operações com race condition usam `IDbContextTransactionProvider.BeginTransactionAsync(IsolationLevel.Serializable, ct)` (ex.: `AprovarVinculo`, `GerarCobrancaMensal`).
- Registro MANUAL no DI (`Api/Extensions/DependencyInjectionExtensions.AddApplicationHandlers`), todos `AddScoped<THandler>()`. NÃO há mediator/auto-scan de handlers.

Áreas (`UseCases/`) e use cases notáveis:
- **Auth**: `Login` (gera JWT; resposta genérica anti-enumeração; exige `EmailVerificado`), `RedefinirSenha`, `VerificarEmail`.
- **Conta**: `ObterPerfil`, `AtualizarPerfil`, `AlterarSenha`, `Logout` (revoga jti).
- **Alunos**: `RegistrarAluno` (self-signup), `CadastrarAluno`, `AtualizarAluno`, `AlterarStatusAluno`, `ListarAlunos`, `ObterAluno`, `ListarFichasAluno`/`ObterFichaAluno`, `ListarExecucoesAluno`, `ObterProgressaoAluno`/`ObterMinhaProgressao`.
- **Treinadores**: `RegistrarTreinador`, `AprovarTreinador`/`ReprovarTreinador`/`InativarTreinador`/`ExcluirTreinador`, `AtribuirPlano` (**rejeita tier=Elite** → `Result.Failure(PlanoPlataformaErrors.EliteIndisponivel)`), `IniciarOnboarding`/`VerificarOnboarding` (Stripe Connect), `ListarTreinadores`/`ListarTreinadoresPublicos`/`ObterTreinador`.
- **Vinculos**: `AprovarVinculo` (tx serializable; troca de treinador inativa vínculo anterior + cascateia/copia fichas via `TrarFichas`; valida limite de plano; notifica WhatsApp), `DesvincularAluno`, `ReativarVinculo`, `SolicitarTrocaTreinador`, `ListarVinculos`, `ObterVinculoAluno`.
- **AssinaturaAlunos**: `CriarAssinaturaAluno` (valor derivado de `Pacote.Preco` — NUNCA do caller; valida onboarding e cross-tenant pacote↔treinador), `CancelarAssinaturaAluno`, `CancelarMinhaAssinaturaAluno`, `ObterAssinaturaAluno`.
- **Pagamentos**: `GerarCobrancaMensal` (cria `Pagamento` em tx serializable; detecta "pagamento zumbi"; chama `IStripeService` Pix/Cartão; rollback marca Falhou), `ObterStatusPagamento`, `ListarPagamentosAssinaturaAluno`, `ProcessarWebhookStripe` (ver §3/§4), `ReconciliarPagamentosStripe` (reprocessa eventos via `Events.List` pelo MESMO núcleo `ProcessarEventoAsync`).
- **Exercicios**: `CriarExercicio`, `AtualizarExercicio`, `ExcluirExercicio`, `ListarExercicios`, `CopiarExercicioGlobal`.
- **Treinos**: `CriarTreino`, `AtualizarTreino`, `ExcluirTreino`, `DuplicarTreino`, `ObterTreino`, `ListarTreinos`/`ListarTreinosDoTreinador`/`ListarFichasDoAluno`/`ListarAlunosTreino`, `AdicionarExercicio`/`RemoverExercicio`/`EditarExercicioTreino`/`AtualizarObservacaoExercicio`, `RegistrarExecucao`, `VincularFichaAoAluno`.
- **Pacotes**: `CriarPacote`, `AtualizarPacote`, `ExcluirPacote`, `ListarPacotes`.
- **Planos**: `CriarPlanoPlataforma`, `AtualizarPlanoPlataforma`, `ExcluirPlanoPlataforma`, `ListarPlanosPlataforma`.
- **Admin**: `Alunos/ListarAlunosAdmin`, `GruposMusculares/*` (CRUD), `HealthReport` (`ObterHealthReportConfig`/`AtualizarHealthReportConfig`/`ListarHealthSnapshots`/`ExecutarRelatorioSaude`).

### FluentValidation
- Validators `AbstractValidator<TCommand>` colocalizados na pasta do use case (ex.: `LoginCommandValidator`).
- Auto-descobertos: `services.AddValidatorsFromAssembly(typeof(LoginHandler).Assembly)` em `AddApplicationHandlers`.
- Invocação: dentro do handler via `validator.ValidateAndThrowAsync(command, ct)` (NÃO há pipeline/behavior nem endpoint filter genérico de validação). `ValidationException` resultante é mapeada para 400 no `GlobalExceptionHandler`. Logo: handlers SEM validator injetado não validam input estruturalmente — invariantes ficam no domínio.

### Interfaces (`Application/Interfaces/`)
- Cross-cutting: `IUnitOfWork` (+ `ITransaction`/`IDbContextTransactionProvider` no mesmo arquivo), `IDomainEventDispatcher`, `IDomainEventHandler<T>`/`IDomainEventHandlerBase`, `IUserContext`.
- Auth/cripto: `IJwtService` (`GerarToken(Conta, perfilId)`), `IPasswordHasher` (`Hash`/`Verify`).
- Integrações: `IEmailService` (cross-ref [specification-email]), `IStripeService` (`CriarContaConnectAsync`, `GerarLinkOnboardingAsync`, `CriarPixPaymentIntentAsync`, `CriarCartaoPaymentIntentAsync`, `ContaEstaAtivadaAsync`, `ValidarWebhookAsync`, `ListarEventosDesdeAsync`; records `PixPaymentResult`/`CartaoPaymentResult`/`StripeEventSummary`; cross-ref [specification-stripe]), `IWhatsAppNotifier` (`SendAsync(phone, msg, ct)`).
- **Notificação por tier**: `IPlanoNotificationPolicy` (Application/Interfaces): `Task<CanaisNotificacao> ObterCanaispor TreinadorAsync(treinadorId, ct)` / `ObterCanaisPorAlunoAsync(alunoId, ct)`. Record `CanaisNotificacao(bool Email, bool WhatsApp)`. Impl `PlanoNotificationPolicy` (Infrastructure/Notifications/): resolve treinador → `PlanoPlataformaId` → plano → `TierPlanoExtensions`; resolve aluno → vínculo ativo → assinatura atual → treinador → plano; sem plano = `(false,false)`. Registrado `AddScoped` no DI. Cross-ref `TierPlanoExtensions` [specification-model].
- Health: `IHealthReportCollector`, `IHealthReportSender`.
- App services: `ILimiteTreinadorService`.
- Repositórios (`Interfaces/Repositories/`, 24): Conta, Aluno, Treino, Exercicio, GrupoMuscular, TreinoAluno, ExecucaoTreino, SystemUser, Treinador, PlanoPlataforma, Pacote, VinculoTreinadorAluno, LogAprovacao, TokenRevogado, PasswordResetToken, EmailVerificationToken, EmailDeliveryLog, AssinaturaAluno, Pagamento, Assinante, ContaRecebimento, HealthReportConfig, HealthSnapshot, ErrorLog. Implementações em `Infrastructure/Persistence/Repositories`.

### Services / Settings
- `Application/Services/LimiteTreinadorService` (`ILimiteTreinadorService`): valida que treinador tem plano e que `vínculos ativos < plano.MaxAlunos`; senão lança `LimiteAlunosAtingidoException`. Usa `ICapacidadePlano` (domínio).
- `Application/Settings/` (POCOs bound por DI): `AppSettings` (`FrontendBaseUrl`; bind `App`), `EmailSettings` (bind `Email`; cross-ref [specification-email]), `PaymentSettings` (`TaxaPlataformaPercent=5`; configurado a partir de `StripeSettings`).
- `Infrastructure/Services/StripeSettings` (bind `Stripe`): `SecretKey`/`PublishableKey`/`WebhookSecret`/`TaxaPlataformaPercent`/`UrlBase`. **ValidateOnStart**: SecretKey e WebhookSecret não-vazios + `0 < TaxaPlataformaPercent <= 100` (falha o boot se ausente).

## 3. DOMAIN EVENT DISPATCH (mecânica) — cross-cutting CHAVE
`AppDbContext` implementa `IUnitOfWork` + `IDbContextTransactionProvider`. `CommitAsync` (`Infrastructure/Persistence/AppDbContext.cs`):
1. **Coleta** entidades `IHasDomainEvents` rastreadas pelo `ChangeTracker` com `DomainEvents.Count > 0` (snapshot da lista ANTES de salvar). Se `eventDispatcher` é null, lista vazia.
2. **`SaveChangesAsync`** (persiste a transação EF).
3. **Snapshot + clear ANTES de despachar**: copia todos os eventos para uma lista flat e chama `ClearDomainEvents()` em cada entidade. RE-ENTRÂNCIA: handlers podem chamar `CommitAsync` de novo; limpar antes garante "cada evento dispara exatamente uma vez" (sem isso o commit aninhado re-coletaria e re-despacharia — ex.: projeção `Assinante` inserida 2x → duplicate key).
4. **Dispatch** via `IDomainEventDispatcher.DispatchAsync(events, ct)` se houver eventos.

`DomainEventDispatcher` (`Infrastructure/Services/`): para cada evento, monta `IDomainEventHandler<TConcreteEvent>` via reflection (`MakeGenericType(evento.GetType())`), resolve TODOS os handlers via `IServiceProvider.GetServices(handlerType)` e invoca `HandleAsync` SEQUENCIALMENTE na ordem de registro no DI. `IDomainEventHandler<in T>` tem default interface method que faz cast `IDomainEvent → T`.
- **Múltiplos handlers por evento**: suportado (ex.: `PagamentoEmDisputaEvent` → e-mail treinador + alert; `AssinaturaAlunoCanceladaEvent` → e-mail aluno + e-mail treinador + WhatsApp; `VinculoAprovadoEvent` → e-mail + criar assinatura).
- **Boundary transacional**: handlers rodam APÓS `SaveChangesAsync` ter persistido. NÃO há tx que englobe save + handlers por padrão; quando o handler chama `CommitAsync` de novo, é um novo `SaveChanges` no mesmo `DbContext` scoped. Em handlers críticos (ex.: `AprovarVinculo`) o use case abre uma tx serializable explícita ao redor do `CommitAsync` + `tx.CommitAsync` — handlers de evento despachados dentro do `CommitAsync` participam dessa tx.
- Resolução de handlers é por escopo de request (todos `AddScoped`). Detalhe de catálogo de eventos/produtores em [specification-model].

## 4. API LAYER

### Bootstrap
`Program.cs`: `AddApiServices(config, env).AddApplicationHandlers()`; em Development/Homolog roda `db.Database.MigrateAsync()` + `DataSeeder.SeedAsync()`; `UseApiConfiguration()` + `MapApiEndpoints()`. User Secrets `forzion-prod` carregados em Homolog/Development.
- `UseApiConfiguration` (ordem): Swagger(dev) → ExceptionHandler → HttpsRedirection(prod) → headers de segurança (X-Content-Type-Options nosniff, X-Frame-Options DENY, Referrer-Policy no-referrer, Permissions-Policy, HSTS em prod) → CORS `AllowFrontend` → **Authentication → Authorization → RateLimiter** (auth ANTES do rate limiter para particionar por `sub`) → `/health`.

### Endpoints (Minimal APIs, `Api/Endpoints/`)
Registro via extensões `MapXxxEndpoints` agregadas em `RouteBuilderExtensions.MapApiEndpoints`: Auth, Admin, HealthReport, Treinador, AlunoArea, Aluno, Conta, Exercicio, Treino, Pagamentos, Webhook. Padrão:
- Agrupamento por recurso com `MapGroup("/...")` + `.WithTags(...)` + `.RequireAuthorization("<Policy>")` + `.RequireRateLimiting("<policy>")` + `.AddEndpointFilter<...>()`.
- Handlers injetados via `[FromServices]`; contexto do usuário via `[FromServices] IUserContext` (`userContext.PerfilId`); `CancellationToken` no fim. Retorno: `Results.Ok/NoContent/NotFound/Problem` ou `result.ToProblemResult()`.
- Ex.: `AlunoAreaEndpoints` (`/aluno`, policy Aluno, `PaginacaoFilter`), `PagamentosEndpoints` (grupos `/aluno/pagamentos` read e `/treinador/pagamentos` write + endpoints internos), `WebhookEndpoints` (`/webhooks/stripe`, `/webhooks/resend` — anônimos, rate `webhook`, body limitado a 64 KB via `LimitedStream`).

### Auth / authorization
- JWT Bearer (`Api/Configuration/AuthenticationExtensions`): segredo `Auth:JwtSecret` (>= 32 bytes, senão throw no boot), `JwtIssuer`/`JwtAudience` default `forzion.tech`, `ClockSkew=0`, `MapInboundClaims=false`. `OnTokenValidated` checa blacklist via `ITokenRevogadoRepository.EstaRevogadoAsync(jti)` → `ctx.Fail` se revogado (logout). Eventos de diagnóstico só fora de produção.
- Policies por claim `tipo_conta`: `"SystemAdmin"`, `"Treinador"`, `"Aluno"`.
- `JwtService` (Infrastructure): claims `conta_id`, `tipo_conta`, `perfil_id`, `jti` (novo Guid); expiração `Auth:JwtExpirationMinutes` (default 60).
- `Api/Context/HttpUserContext` (`IUserContext`, scoped): lê claims `conta_id`/`tipo_conta`/`perfil_id`/`jti`/`exp`; parse inválido lança `AcessoNegadoException`. Helpers `IsSystemAdmin`/`IsTreinador`/`IsAluno`.

### Filters (`Api/Filters/`, `IEndpointFilter`)
- `PerfilIdRequiredFilter`: rejeita (401) requisições autenticadas sem claim `perfil_id` (`PerfilId == Guid.Empty`). Aplicado em nível de grupo.
- `PaginacaoFilter`: valida query `pagina >= 1` e `1 <= tamanhoPagina <= 100`; senão 400.
- `RequireAssinaturaAtivaFilter` (transient, registrado explicitamente): se `TipoConta==Aluno` e assinatura atual `Inadimplente` → 403 com `code=ASSINATURA_INADIMPLENTE`. Tipos não-Aluno passam direto; sem aluno/assinatura passa direto. Bloqueia só fluxos de consumo/escrita (GET de leitura permanecem liberados — visibilidade LGPD). Cross-ref [specification-stripe]/[specification-frontend].

### Middleware / error mapping
`GlobalExceptionHandler` (`IExceptionHandler`, RFC7807 ProblemDetails):
- `ValidationException` (FluentValidation) → 400 `ValidationProblemDetails` com erros agrupados por propriedade (camelCase).
- Mapa de exceções de domínio → HTTP: `CredenciaisInvalidasException`→401; `*NaoEncontradaException`→404; `AlunoInativoException`/`AcessoNegadoException`/`EmailNaoVerificadoException`→403; `EmailJaCadastradoException`/`AlunoJaVinculadoException`→409; `DomainException`→422; demais→500 (mensagem genérica). `EmailNaoVerificadoException` adiciona `code` extension (cross-ref [specification-email]).
- Log: >=500 Error; <500 Warning.
- `Results` de falha de negócio com `Result` pattern usam `ResultExtensions.ToProblemResult()` → 422.

### Rate limiting (`AddApiServices`)
Políticas FixedWindow (rejeição 429). Em ambiente `Test` todas viram NoLimiter. Chave: `auth`/`internal`/`webhook` por IP; `read`/`write` por `sub` (autenticado) ou IP (fallback).
| Policy | Limite | Janela | Chave |
|--------|--------|--------|-------|
| auth | 10 | 1 min | IP |
| write | 60 | 1 min | sub/IP |
| read | 120 | 1 min | sub/IP |
| internal | 5 | 1 min | IP |
| webhook | 300 | 1 min | IP |

### Endpoints internos (server-to-server)
`/internal/processar-renovacoes` (POST) e `/internal/reconciliar-pagamentos` (POST), anônimos + rate `internal`. Autenticação por header `X-Internal-Key` comparado a `Internal:ApiKey` com **comparação de tempo constante** (`CryptographicOperations.FixedTimeEquals`, após checar igualdade de comprimento — evita `ArgumentException` e timing attack). Sem/divergente → 401.
- `processar-renovacoes`: lista assinaturas a renovar e chama `GerarCobrancaMensalHandler` por assinatura, conta processadas/falhas.
- `reconciliar-pagamentos`: body opcional `{ desdeUtc }` (default janela 7d); chama `ReconciliarPagamentosStripeHandler`. Cross-ref [specification-infrastructure] (cron/chamador).

### Webhooks
`/webhooks/stripe`: lê body (limite 64 KB), header `Stripe-Signature`, chama `ProcessarWebhookStripeHandler` → 200/400. `/webhooks/resend`: headers Svix → `ProcessarWebhookResendHandler`. Detalhes Stripe em [specification-stripe], Resend/Svix em [specification-email].

### Background services (hosted)
- `LimparTokensRevogadosService` (`Api/Services/`): loop horário, remove tokens revogados expirados.
- `RelatorioSaudeDiarioService`: loop de 15 min; envia relatório de saúde diário conforme `HealthReportConfig` (`DeveEnviar`: ativo, hora >= `HoraEnvioUtc`, não enviado hoje).
- (Ambos pulados em ambiente `Test`.)

### DI wiring (resumo)
- `Api/Extensions/DependencyInjectionExtensions`: `AddApiServices` (exception handler, ProblemDetails, rate limiter, Swagger, JWT, CORS, HealthChecks, JSON enum-as-string, `IUserContext`, `RequireAssinaturaAtivaFilter`, e — fora de Test — `AddInfrastructure` + hosted services + `ErrorLogDbSinkProvider`). `AddApplicationHandlers` (validators auto-scan, `ILimiteTreinadorService`, `AppSettings`, e CADA handler `AddScoped<THandler>()` manualmente).
- CORS `AllowFrontend`: origens de `Cors:AllowedOrigins` (`;`-separado), filtra inválidas/curingas; métodos GET/POST/PUT/DELETE/PATCH/OPTIONS; `AllowCredentials`.

## 5. INFRASTRUCTURE LAYER

### EF Core / persistência
- `AppDbContext` schema-agnostic (sem `HasDefaultSchema`; schema vem do `Search Path` da connection), `UseSnakeCaseNamingConvention`, `MigrationsHistoryTable("__EFMigrationsHistory")`. Configs por entidade em `Persistence/Configurations` (`ApplyConfigurationsFromAssembly`). DbSets para 28 entidades EF; `TreinoExercicio`/`ExecucaoExercicio` internal (composição). Cross-ref [specification-db] (schema/migrations/enums).
- Registrado scoped em `InfrastructureExtensions` montando `DbContextOptions` na hora e passando o `IDomainEventDispatcher`. `IUnitOfWork` e `IDbContextTransactionProvider` resolvem para o MESMO `AppDbContext` scoped.
- Repositórios (`Persistence/Repositories`): classe com ctor primário `(AppDbContext context)`, métodos async; leituras de listagem usam `AsNoTracking`; paginação `(IReadOnlyList<T> Items, int Total)` com `Skip/Take`. Todos `AddScoped` em `InfrastructureExtensions`.

### Integrações + gate Null* (`InfrastructureExtensions.AddInfrastructure`)
- **Email** (`IEmailService`): `Resend:ApiKey` presente → `ResendEmailService` (HttpClient "resend", timeout 15s); senão `NullEmailService`. SEMPRE embrulhado em `EnvironmentEmailDecorator`. Cross-ref [specification-email].
- **WhatsApp** (`IWhatsAppNotifier`): `WhatsApp:PhoneNumberId` + `WhatsApp:AccessToken` presentes → `MetaWhatsAppCloudNotifier` (HttpClient typed para Graph API `v21.0` default, timeout 15s); senão `NullWhatsAppNotifier` (no-op, warning no ctor).
- **Stripe** (`IStripeService`): NÃO tem variante Null — `StripeService` sempre registrado, mas `StripeSettings` tem `ValidateOnStart` (boot falha sem SecretKey/WebhookSecret). Idempotência via `Stripe-Idempotency-Key` (`pagamento-{guid}`). Cross-ref [specification-stripe].
- `TimeProvider.System` singleton; `IJwtService`/`IPasswordHasher` (BCrypt) scoped.

### Domain event handlers (registrados por `IDomainEventHandler<T>`)
Localização e categorias:
- `Infrastructure/Notifications/Email/`: handlers de e-mail (Treinador aprovado/reprovado/inativado, Vínculo aprovado, AssinaturaAluno criada/inadimplente/cancelada×2, Aluno registrado/inativado, Conta registrada, Pagamento criado/falhou/estornado/em disputa). Também os senders de verificação/reset (`EmailVerificationSender`, `EsqueceuSenhaHandler`, `ReenviarVerificacaoHandler`) e `ProcessarWebhookResendHandler`, `EmailTemplates`, `EnvironmentEmailDecorator`. Handlers checam `IEmailService.Habilitado` antes de enviar. **GATED** (handlers operacionais): checam `IPlanoNotificationPolicy` → `canais.Email` antes de enviar (pagamento criado/falhou/estornado/disputa, assinatura criada/cancelada×2, vínculo aprovado/pendente, inadimplência, aluno-inativado). **UNGATED** (sempre enviam, sem gating): verificação e-mail, reset senha, reenvio verificação, treinador aprovado/reprovado/inativado, bem-vindo aluno. Cross-ref [specification-email].
- `Infrastructure/Notifications/WhatsApp/`: handlers Pagamento criado/falhou/estornado, AssinaturaAluno inadimplente/cancelada + os notifiers (`MetaWhatsAppCloudNotifier`/`NullWhatsAppNotifier`). **GATED**: mesmos handlers operacionais checam `canais.WhatsApp` via `IPlanoNotificationPolicy`. **UNGATED**: TreinadorAprovado/Reprovado/Inativado, BemVindoAluno.
- `Infrastructure/Notifications/Alerts/`: `PagamentoEmDisputaAlertHandler`.
- `Infrastructure/Handlers/` (projeção billing / orquestração): `VinculoAprovadoCriarAssinaturaAlunoHandler`, `AlunoRegistradoSincronizarAssinanteHandler`, `AlunoAtualizadoSincronizarAssinanteHandler`.
Catálogo de eventos × produtores em [specification-model].

### Seed (`Infrastructure/Seed/DataSeeder`)
Idempotente (insere só o que falta): 9 grupos musculares padrão, ~90 exercícios globais (`TreinadorId=null`), 5 planos de plataforma (Free/Basic/Pro/ProPlus/Elite com MaxAlunos/Preço), e SuperAdmin (`Seed:AdminEmail` default `admin@forzion.tech`; `Seed:AdminPassword` obrigatório — throw se ausente; conta criada já com e-mail verificado, eventos limpos). Roda só em Development/Homolog (Program.cs).

### Outros
- `Infrastructure/Logging/ErrorLogDbSinkProvider`: `ILoggerProvider` que persiste erros (registrado fora de Test).
- `Infrastructure/Health/` (`HealthReportCollector`/`HealthReportSender`), `Persistence/AppDbContextFactory` (design-time para migrations).

## 6. CONVENÇÕES
- Commits Conventional Commits; scopes válidos `frontend|backend|infra|ci|deps|tests|docs`; subject lowercase. Detalhes/hooks em [specification-git] (commitlint vive em `frontend/`).
- Gates pré-PR (AGENTS.md FLUXO): build completo (frontend + backend) Release, avaliar/criar testes, rodar TODOS os testes (integração/E2E exigem Docker — CI roda no PR), confirmar PR → `homolog` (ou → `master` se feito direto em `homolog`).
- Result pattern OU exceção tipada (manter estilo local do handler); `TimeProvider` injetado (nunca `DateTime.UtcNow` na Application); FluentValidation auto-descoberto e invocado dentro do handler; DI de handlers/repos/event-handlers MANUAL e scoped.

## 7. TESTES (backend) — resumo
`forzion.tech.Tests` (xUnit `2.9.3`). Frameworks: Moq, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory; ambiente `Test`), Testcontainers.PostgreSql (Integration/E2E/Infra — exigem Docker), `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`), CsCheck (property-based), Verify.Xunit (snapshot), NetArchTest.Rules (arquitetura — `Architecture/LayeringTests.cs` + `ConventionTests.cs`). Pastas: `Api`, `Application`, `Architecture`, `Builders`, `Domain`, `E2E`, `Infrastructure`, `Integration`. Split por trait `Category=Integration` (`--filter "Category!=Integration"` roda os unit sem Docker, ~1000+). Cross-ref README para harness completo.
