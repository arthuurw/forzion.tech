# specification-backend — backend (forzion.tech)

DOC PARA AGENTES. Fonte de verdade da arquitetura do backend (.NET 8, Clean Architecture + DDD). Camadas Application/Api/Infrastructure. Formato denso. Cross-ref: [specification-model] (domínio: entidades/eventos/invariantes), [specification-db] (EF/persistência), [specification-stripe] (pagamento), [specification-email] (notificações), [specification-infrastructure] (deploy/nginx/CI), [specification-security] (postura consolidada: auth/headers/rate-limit/webhook signing), [specification-observability] (logging/health/relatório de saúde).

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
- `Error(string Code, string Message, ErrorType Type=Business)` record com discriminador `ErrorType` {Business, Validation, NotFound, Conflict}; factories `Error.Business`/`Validation`/`NotFound`/`Conflict` (semântica em [specification-model]). Catálogos de domínio em `Domain/Shared/Errors/*.cs` (code `<agg>.<motivo>`).
- Mapeamento HTTP no Api: `ResultExtensions.ToProblemResult()` faz switch em `Error.Type` → NotFound:404, Conflict:409, Validation:400, **default (Business):422**; `Results.Problem(detail=Error.Message, statusCode, extensions["code"]=Error.Code)`. Endpoints checam `result.IsFailure` e chamam `ToProblemResult()` (status já derivado do Type). Alguns ainda tratam codes específicos antes (ex.: `cobrar` plano treinador trata `plano_free_assinatura_cancelada` como 200; `CancelarMinhaAssinaturaAlunoHandler` code de "não encontrada").
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
- **Auth**: `Login` (gera JWT; resposta genérica anti-enumeração; exige `EmailVerificado`; treinador exige `Status==Ativo` — `AguardandoAprovacao`/`Inativo` → 403, e-mail verificado NÃO basta), `RedefinirSenha`, `VerificarEmail`.
- **Conta**: `ObterPerfil`, `AtualizarPerfil`, `AlterarSenha`, `Logout` (revoga jti); `Conta/Lgpd/ExportarDadosPessoais` (`GET /conta/lgpd/exportar` — portabilidade: export versionado só do titular, nunca PII de terceiros) e `Conta/Lgpd/AnonimizarConta` (`DELETE /conta/lgpd` — self exige reconfirmação de senha BCrypt; anonimiza Conta/Aluno|Treinador + read model Assinante + delivery logs por email/telefone; idempotente; treinador com vínculos ativos rejeitado → `conta.offboarding_necessario`; registra `LogAprovacao` LGPD). Grupo `/conta/lgpd` autenticado + rate-limit "write".
- **Alunos**: `RegistrarAluno` (self-signup), `CadastrarAluno`, `AtualizarAluno`, `AlterarStatusAluno`, `ListarAlunos`, `ObterAluno`, `ListarFichasAluno`/`ObterFichaAluno`, `ListarExecucoesAluno` (LEITURA read-only — só checa posse `IsSystemAdmin || PerfilId==alunoId`; NÃO injeta/exige vínculo ativo → histórico consultável mesmo após desvínculo; contraste com a ESCRITA em `RegistrarExecucao` que exige vínculo ativo), `ObterProgressaoAluno`/`ObterMinhaProgressao`.
- **Treinadores**: `RegistrarTreinador`, `AprovarTreinador`/`ReprovarTreinador`/`InativarTreinador`/`ExcluirTreinador`, `AtribuirPlano` (**rejeita tier=Elite** → `Result.Failure(PlanoPlataformaErrors.EliteIndisponivel)`), `IniciarOnboarding`/`VerificarOnboarding` (Stripe Connect; `OnboardingStatusResponse(OnboardingCompleto, ContaConfigurada, ModoPagamentoAluno, ModoPagamentoPodeAlterarEm)` — expõe `ModoPagamentoAluno` p/ UI condicional + `ModoPagamentoPodeAlterarEm` (= última troca + 90d, null se nunca; servidor calcula o boundary do cooldown, FE só compara com agora)), `AlterarModoPagamento` (troca `Plataforma↔Externo` pós-signup; cooldown 90d; →Externo cancela assinaturas dos alunos + cancela Pix pendentes pós-commit; →Plataforma exige onboarding + cria assinaturas p/ pacotes ativos cobráveis; supressão de notificação no bulk; resposta com `AssinaturasCriadas`/`VinculosIgnorados` — [specification-stripe]), `ListarTreinadores`/`ListarTreinadoresPublicos`/`ObterTreinador`. **Billing do plano do treinador** (PaymentIntent direto-plataforma, sem Connect — [specification-stripe]): `IniciarPagamentoPlano` (1º pagamento do cadastro — exige `TreinadorStatus.AguardandoPagamento` + `AssinaturaTreinador` Pendente; tx serializable + single-write; detecta pagamento zumbi; finalidade `Cadastro`; `IStripeService.CriarPix/CartaoPlataformaPaymentIntentAsync`), `GerarCobrancaPlanoTreinador` (renovação de assinatura ativa; trata downgrade p/ Free → encerra assinatura via code `plano_free_assinatura_cancelada`), `TrocarPlanoTreinador` (upgrade/downgrade/regularização — gera cobrança da troca, finalidade `TrocaPlano`).
- **Vinculos**: `AprovarVinculo` (injeta `ITreinadorRepository`; tx serializable; **gate de onboarding CONDICIONAL ao modo: SÓ se `treinador.ModoPagamentoAluno==Plataforma` exige `ContaRecebimento.OnboardingCompleto`; senão falha `treinador_sem_onboarding` ANTES de qualquer efeito** — modo `Externo` DISPENSA Connect (aluno paga fora da app), aprova sem onboarding; evita aluno ativo sem billing no modo Plataforma; troca de treinador inativa vínculo anterior + cascateia/copia fichas do treinador anterior quando o flag `AprovarVinculoCommand.TrarFichas=true`; valida limite de plano; notifica WhatsApp), `DesvincularAluno`, `ReativarVinculo`, `SolicitarTrocaTreinador`, `ListarVinculos`, `ObterVinculoAluno`.
- **AssinaturaAlunos**: `CriarAssinaturaAluno` (valor derivado de `Pacote.Preco` — NUNCA do caller; valida onboarding e cross-tenant pacote↔treinador). **NÃO tem caller**: DI-registrado mas SEM produtor (criação real é via evento — ver §5); por isso SEM guarda de modo `Externo` (de propósito, evita lógica morta); se um endpoint público passar a chamá-lo, adicionar lookup de vínculo ativo + match aluno/treinador. `CancelarAssinaturaAluno`, `CancelarMinhaAssinaturaAluno`, `ObterAssinaturaAluno`.
- **Pagamentos**: `GerarCobrancaMensal` (cria `Pagamento` em tx serializable; detecta "pagamento zumbi"; chama `IStripeService` Pix/Cartão; rollback marca Falhou), `ObterStatusPagamento`, `ListarPagamentosAssinaturaAluno`, `ProcessarWebhookStripe` (ver §3/§4), `ReconciliarPagamentosStripe` (reprocessa eventos via `Events.List` pelo MESMO núcleo `ProcessarEventoAsync`).
- **Exercicios**: `CriarExercicio`, `AtualizarExercicio`, `ExcluirExercicio`, `ListarExercicios`, `CopiarExercicioGlobal`.
- **Treinos**: `CriarTreino`, `AtualizarTreino`, `ExcluirTreino`, `DuplicarTreino`, `ObterTreino`, `ListarTreinos`/`ListarTreinosDoTreinador`/`ListarFichasDoAluno`/`ListarAlunosTreino`, `AdicionarExercicio`/`RemoverExercicio`/`EditarExercicioTreino`/`AtualizarObservacaoExercicio`, `RegistrarExecucao` (ESCRITA — exige vínculo ATIVO treinador↔aluno via `vinculoRepository.ObterAtivoAsync`; sem vínculo ativo → `AcessoNegadoException` 403, além de `TreinoAluno` Ativo + aluno não-inativo), `VincularFichaAoAluno`.
- **Pacotes**: `CriarPacote`, `AtualizarPacote`, `ExcluirPacote`, `ListarPacotes`.
- **Planos**: `CriarPlanoPlataforma`, `AtualizarPlanoPlataforma`, `ExcluirPlanoPlataforma`, `ListarPlanosPlataforma`.
- **Admin**: `Alunos/ListarAlunosAdmin`, `GruposMusculares/*` (CRUD), `HealthReport` (`ObterHealthReportConfig`/`AtualizarHealthReportConfig`/`ListarHealthSnapshots`/`ExecutarRelatorioSaude`), `Stats/ObterDashboardStats` (`GET /admin/stats/dashboard` — distribuição de treinadores por plano/tier e alunos por finalidade; via `IAdminStatsRepository`, response `DashboardStatsResponse`).

### FluentValidation
- Validators `AbstractValidator<TCommand>` colocalizados na pasta do use case (ex.: `LoginCommandValidator`).
- Auto-descobertos: `services.AddValidatorsFromAssembly(typeof(LoginHandler).Assembly)` em `AddApplicationHandlers`.
- Invocação: dentro do handler via `validator.ValidateAndThrowAsync(command, ct)` (NÃO há pipeline/behavior nem endpoint filter genérico de validação). `ValidationException` resultante é mapeada para 400 no `GlobalExceptionHandler`. Logo: handlers SEM validator injetado não validam input estruturalmente — invariantes ficam no domínio.

### Interfaces (`Application/Interfaces/`)
- Cross-cutting: `IUnitOfWork` (+ `ITransaction`/`IDbContextTransactionProvider` no mesmo arquivo), `IDomainEventDispatcher`, `IDomainEventHandler<T>`/`IDomainEventHandlerBase`, `IUserContext`.
- Auth/cripto: `IJwtService` (`GerarToken(Conta, perfilId)`), `IPasswordHasher` (`Hash`/`Verify`).
- Integrações: `IEmailService` (cross-ref [specification-email]), `IStripeService` (`CriarContaConnectAsync`, `GerarLinkOnboardingAsync`, `CriarPixPaymentIntentAsync`, `CriarCartaoPaymentIntentAsync`, `ContaEstaAtivadaAsync`, `ValidarWebhookAsync`, `ListarEventosDesdeAsync`; records `PixPaymentResult`/`CartaoPaymentResult`/`StripeEventSummary`; cross-ref [specification-stripe]), `IWhatsAppNotifier` (`SendAsync(phone, msg, ct)`).
- **Notificação por tier**: `IPlanoNotificationPolicy` (Application/Interfaces): `Task<CanaisNotificacao> ResolverPorTreinadorAsync(treinadorId, ct)` / `ResolverPorAlunoAsync(alunoId, ct)`. Record `CanaisNotificacao(bool Email, bool WhatsApp)` (`CanaisNotificacao.Nenhum` = `(false,false)`). Impl `PlanoNotificationPolicy` (Infrastructure/Notifications/): resolve treinador → `PlanoPlataformaId` → plano → `TierPlanoExtensions`; resolve aluno → vínculo ativo → assinatura atual → treinador → plano; sem plano = `Nenhum`. Registrado `AddScoped` no DI. Cross-ref `TierPlanoExtensions` [specification-model].
- Health: `IHealthReportCollector`, `IHealthReportSender`.
- App services: `ILimiteTreinadorService`.
- Repositórios (`Interfaces/Repositories/`, 28): Conta, Aluno, Treino, Exercicio, GrupoMuscular, TreinoAluno, ExecucaoTreino, SystemUser, Treinador, PlanoPlataforma, Pacote, VinculoTreinadorAluno, LogAprovacao, TokenRevogado, PasswordResetToken, EmailVerificationToken, EmailDeliveryLog, WhatsAppDeliveryLog, AssinaturaAluno, Pagamento, AssinaturaTreinador, PagamentoTreinador, Assinante, ContaRecebimento, HealthReportConfig, HealthSnapshot, ErrorLog, AdminStats. Implementações em `Infrastructure/Persistence/Repositories`.

### Services / Settings
- `Application/Services/LimiteTreinadorService` (`ILimiteTreinadorService`): valida que treinador tem plano e que `vínculos ativos < plano.MaxAlunos`; senão lança `LimiteAlunosAtingidoException`. Usa `ICapacidadePlano` (domínio).
- `Application/Services/CriarPagamentoComIntentService` — application service que centraliza a coreografia **G-PAY-1** (T7). Genérico `<TPagamento>`. Recebe `CriarPagamentoComIntentParams<TPagamento>` (record) com delegates: `ObterPendente`, `VerificarIdempotencia`, `CriarPagamento`, `AplicarIntentPix`, `AplicarIntentCartao`, `AdicionarAsync`, `MarcarFalhou`, `Metodo`. Fluxo em `ExecutarAsync`: (1) tx serializable; (2) SELECT pendente → idempotência: reutiliza se `VerificarIdempotencia` retorna não-null (intent já presente); (3) zumbi: pendente irrecuperável → `MarcarFalhou` + recria; (4) `AplicarIntentPix/Cartao` (Stripe ANTES do commit — falha Stripe = rollback, sem zumbi); (5) `AdicionarAsync` + `CommitAsync` + `tx.CommitAsync` (single-write). Usado por `IniciarPagamentoPlanoHandler`, `GerarCobrancaMensalHandler`, `GerarCobrancaPlanoTreinadorHandler`, `TrocarPlanoTreinadorHandler`. Registrado `AddScoped` em `AddApplicationHandlers`.
- `Application/Settings/` (POCOs bound por DI): `AppSettings` (`FrontendBaseUrl`; bind `App`), `EmailSettings` (bind `Email`; cross-ref [specification-email]), `PaymentSettings` (`TaxaPlataformaPercent=5`; configurado a partir de `StripeSettings`).
- `Infrastructure/Services/StripeSettings` (bind `Stripe`): `SecretKey`/`PublishableKey`/`WebhookSecret`/`TaxaPlataformaPercent`/`UrlBase`. **ValidateOnStart**: SecretKey e WebhookSecret não-vazios + `0 < TaxaPlataformaPercent <= 100` (falha o boot se ausente).

## 3. DOMAIN EVENT DISPATCH (mecânica) — cross-cutting CHAVE
`AppDbContext` implementa `IUnitOfWork` + `IDbContextTransactionProvider`. `CommitAsync` (`Infrastructure/Persistence/AppDbContext.cs`):
1. **Coleta** entidades `IHasDomainEvents` rastreadas pelo `ChangeTracker` com `DomainEvents.Count > 0` (snapshot da lista ANTES de salvar). Se `eventDispatcher` é null, lista vazia.
2. **Snapshot + clear ANTES do `SaveChanges`**: copia todos os eventos para uma lista flat e chama `ClearDomainEvents()`. RE-ENTRÂNCIA: handlers podem chamar `CommitAsync` de novo; limpar antes garante "cada evento dispara exatamente uma vez" (sem isso o commit aninhado re-coletaria — ex.: projeção `Assinante` inserida 2x → duplicate key).
3. **Enfileira efeitos duráveis**: para cada evento cujo tipo é durável (`OutboxDurabilityRegistry.EhDuravel`), adiciona uma linha `outbox_efeitos` (`tipo=evt:<FullName>`, payload = JSON do evento, chave de idempotência do registry, `proxima_tentativa = OcorridoEm`). Vai ao `ChangeTracker` ANTES do save → atomicidade.
4. **`SaveChangesAsync`** (persiste agregado + linhas outbox na MESMA transação EF).
5. **Dispatch in-memory** via `IDomainEventDispatcher.DispatchAsync(events, ct)`.

`DomainEventDispatcher` (`Infrastructure/Services/`): para cada evento, monta `IDomainEventHandler<TConcreteEvent>` via reflection (`MakeGenericType(evento.GetType())`), resolve TODOS os handlers via `IServiceProvider.GetServices(handlerType)` e invoca `HandleAsync` SEQUENCIALMENTE na ordem de registro no DI. `IDomainEventHandler<in T>` tem default interface method que faz cast `IDomainEvent → T`.
- **Partição durável**: `DispatchAsync` PULA handlers marcados duráveis no `OutboxDurabilityRegistry` (rodam no worker do outbox, §3.1) — as notificações best-effort do mesmo evento continuam in-memory. `DispatchDuravelAsync(evento)` (usado só pelo worker) faz o oposto: roda SÓ os handlers duráveis e PROPAGA exceção (→ retry).
- **Múltiplos handlers por evento**: suportado (ex.: `PagamentoEmDisputaEvent` → e-mail treinador + alert; `AssinaturaAlunoCanceladaEvent` → e-mail aluno + e-mail treinador + WhatsApp; `VinculoAprovadoEvent` → e-mail + WhatsApp + criar assinatura [durável]).
- **Boundary transacional**: handlers rodam APÓS `SaveChangesAsync` ter persistido. NÃO há tx que englobe save + handlers por padrão; quando o handler chama `CommitAsync` de novo, é um novo `SaveChanges` no mesmo `DbContext` scoped. Em handlers críticos (ex.: `AprovarVinculo`) o use case abre uma tx serializable explícita ao redor do `CommitAsync` + `tx.CommitAsync` — handlers de evento despachados dentro do `CommitAsync` participam dessa tx.
- Resolução de handlers é por escopo de request (todos `AddScoped`). Detalhe de catálogo de eventos/produtores em [specification-model].

## 3.1 OUTBOX DE EFEITOS DURÁVEIS (transacional) — cross-cutting
Efeitos que NÃO podem se perder (mutação de negócio crítica, chamada a API externa) vão por outbox: persistidos na mesma transação do agregado e processados por worker com retry. Substitui o best-effort do §3 para esses casos específicos (`specification-coding §1`).
- **Tabela** `outbox_efeitos` (`specification-db`): `tipo`, `payload jsonb`, `status` (`Pendente|Processando|Concluido|Falhou`), `tentativas`, `proxima_tentativa`, `ultimo_erro`, `chave_idempotencia` UNIQUE, `processado_em`. Entidade `OutboxEfeito` (factory `Criar` + transições com guard de máquina de estado).
- **Dois estilos de `tipo`**: `evt:<FullName>` = re-dispatch de domain-event durável (#10, mutação); `fx:<nome>` = `IOutboxEfeitoHandler` por tipo (#8, efeito externo, ex.: evidência de disputa Stripe).
- **Enfileiramento**: `evt:*` no `AppDbContext.CommitAsync` (§3 passo 3, automático para tipos no registry); `fx:*` via `IOutboxEnfileirador.Enfileirar(tipo, payload, chave)` chamado pela use case ANTES do `CommitAsync` (mesmo UnitOfWork). `OutboxEnfileirador` serializa + `IOutboxRepository.Enfileirar` (Add sem commit).
- **`OutboxDurabilityRegistry`** (singleton, `BuildOutboxDurabilityRegistry` no DI): pares `(evento × handler)` duráveis + extrator de chave de idempotência por evento. Granularidade por handler (não por evento) porque um evento pode ter 1 mutação durável + N notificações best-effort.
- **Worker**: `OutboxProcessorService : BackgroundService` (`Api/Services`, host fino, escopo por ciclo) delega a `OutboxProcessor` (`Infrastructure/Services`, lógica testável). `OutboxProcessor.ProcessarLoteAsync`: abre transação, lê lote sob lease (`IOutboxRepository.ObterProcessaveisAsync` → `FOR UPDATE SKIP LOCKED`), por item `MarcarProcessando` → `OutboxDispatcher.DespacharAsync` → `MarcarConcluido`/falha; `SaveChanges` + commit na MESMA tx (mutação do handler + avanço de status atômicos; locks soltam no commit).
- **`OutboxDispatcher`**: roteia por prefixo — `evt:` → `ResolverTipoEvento` (restrito aos registrados) + desserializa + `DispatchDuravelAsync`; `fx:` → `IOutboxEfeitoHandler` cujo `Tipo` casa.
- **Retry** (`OutboxOptions`, bind `Outbox`): `MaxTentativas` (5), backoff exponencial `BackoffBase·2^tentativas` (base 1min), `LotePorCiclo`, `IntervaloPolling`. Esgotado → `Falhou` + `LogCritical`. Idempotência: índice único em `chave_idempotencia` (re-enfileiramento bloqueado) + guards nos handlers (`specification-coding`).
- **Limpeza + observabilidade**: `OutboxLimpezaService : BackgroundService` (`Api/Services`, cadência `OutboxOptions.IntervaloLimpeza`=1h, separada do polling do worker) → `OutboxProcessor.LimparConcluidosAsync` remove `Concluido` com `processado_em < agora-RetencaoConcluidos` (7d) via `ExecuteDeleteAsync` (`IOutboxRepository.LimparConcluidosAnterioresAsync`). Estado do outbox (contagem por status + amostras de `Falhou`) exposto no relatório de saúde — ver [specification-observability] §3.
- **DI** (`InfrastructureExtensions`): registry singleton; `IOutboxEnfileirador`, `OutboxDispatcher`, `OutboxProcessor` scoped; `OutboxOptions` bind; `AppDbContext` recebe o registry. `OutboxProcessorService` + `OutboxLimpezaService` em `AddHostedService` (fora de Test).

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
- `RequireAssinaturaAtivaFilterBase` (abstract, T8) — base compartilhada `IEndpointFilter` para filtros de inadimplência. **GETs liberados** (sem checar assinatura — preserva leitura/histórico em inadimplência; princípio LGPD/CDC). Implementações injetam `IUserContext`, delegam checagem a `EstaInadimplenteAsync(services, userContext, ct)` e retornam 403 ProblemDetails com `code=CodigoErro` (abstract) se inadimplente.
- `RequireAssinaturaAtivaFilter : RequireAssinaturaAtivaFilterBase` (transient): impl aluno — `code=ASSINATURA_INADIMPLENTE`. `TipoConta != Aluno` → pass-through. Consulta `IAlunoRepository` + `IAssinaturaAlunoRepository` (última não-Cancelada). Cross-ref [specification-stripe]/[specification-frontend].
- `RequireAssinaturaTreinadorAtivaFilter : RequireAssinaturaAtivaFilterBase` (transient): impl treinador — `code=ASSINATURA_TREINADOR_INADIMPLENTE`. `TipoConta != Treinador` → pass-through. Atualmente retorna false (placeholder; enforcement efetivo pendente de integração com `IAssinaturaTreinadorRepository`). Não-Treinador / sem assinatura passam direto.

### Middleware / error mapping
`GlobalExceptionHandler` (`IExceptionHandler`, RFC7807 ProblemDetails):
- `ValidationException` (FluentValidation) → 400 `ValidationProblemDetails` com erros agrupados por propriedade (camelCase).
- Mapa de exceções de domínio → HTTP: `CredenciaisInvalidasException`→401; `*NaoEncontradaException`→404; `AlunoInativoException`/`AcessoNegadoException`/`EmailNaoVerificadoException`/`TreinadorAguardandoAprovacaoException`/`TreinadorInativoException`/`TreinadorPagamentoPendenteException`→403; `EmailJaCadastradoException`/`AlunoJaVinculadoException`→409; `DomainException`→422; demais→500 (mensagem genérica). `EmailNaoVerificadoException` (`EMAIL_NAO_VERIFICADO`), `TreinadorAguardandoAprovacaoException` (`TREINADOR_AGUARDANDO_APROVACAO`), `TreinadorInativoException` (`TREINADOR_INATIVO`) e `TreinadorPagamentoPendenteException` (`TREINADOR_PAGAMENTO_PENDENTE`) adicionam `code` extension consumido pelo frontend (login).
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
`/internal/processar-renovacoes` (POST), `/internal/processar-renovacoes-treinador` (POST) e `/internal/reconciliar-pagamentos` (POST), anônimos + rate `internal`. Autenticação por header `X-Internal-Key` comparado a `Internal:ApiKey` com **comparação de tempo constante** (`CryptographicOperations.FixedTimeEquals`, após checar igualdade de comprimento — evita `ArgumentException` e timing attack). Sem/divergente → 401.
- `processar-renovacoes`: renovação de assinaturas de ALUNO — lista assinaturas a renovar e chama `GerarCobrancaMensalHandler` por assinatura, conta processadas/falhas.
- `processar-renovacoes-treinador`: renovação de assinaturas de TREINADOR (endpoint SEPARADO) — `IAssinaturaTreinadorRepository.ListarParaRenovarAsync` + `GerarCobrancaPlanoTreinadorHandler` por assinatura; code `plano_free_assinatura_cancelada` = downgrade p/ Free (não conta como falha).
- `reconciliar-pagamentos`: body opcional `{ desdeUtc }` (default janela 7d); chama `ReconciliarPagamentosStripeHandler`. Cross-ref [specification-infrastructure] (cron/chamador).

### Webhooks
`/webhooks/stripe`: lê body (limite 64 KB), header `Stripe-Signature`, chama `ProcessarWebhookStripeHandler` → 200/400. `/webhooks/resend`: headers Svix → `ProcessarWebhookResendHandler`. Detalhes Stripe em [specification-stripe], Resend/Svix em [specification-email].

### Background services (hosted)
- `LimparTokensRevogadosService` (`Api/Services/`): loop horário, remove tokens revogados expirados.
- `RelatorioSaudeDiarioService`: loop de 15 min; envia relatório de saúde diário conforme `HealthReportConfig` (`DeveEnviar`: ativo, hora >= `HoraEnvioUtc`, não enviado hoje).
- `OutboxProcessorService`: loop de polling (`OutboxOptions.IntervaloPolling`); processa lote de efeitos do outbox via `OutboxProcessor` (§3.1).
- `OutboxLimpezaService`: loop `OutboxOptions.IntervaloLimpeza` (1h); remove efeitos `Concluido` além da retenção (§3.1).
- (Todos pulados em ambiente `Test`.)

### DI wiring (resumo)
- `Api/Extensions/DependencyInjectionExtensions`: `AddApiServices` (exception handler, ProblemDetails, rate limiter, Swagger, JWT, CORS, HealthChecks, JSON enum-as-string, `IUserContext`, `RequireAssinaturaAtivaFilter`, `RequireAssinaturaTreinadorAtivaFilter`, e — fora de Test — `AddInfrastructure` + hosted services + `ErrorLogDbSinkProvider`). `AddApplicationHandlers` (validators auto-scan, `ILimiteTreinadorService`, `CriarPagamentoComIntentService`, `AppSettings`, e os handlers — registro manual/scoped descrito em §2).
- CORS `AllowFrontend`: origens de `Cors:AllowedOrigins` (`;`-separado), filtra inválidas/curingas; métodos GET/POST/PUT/DELETE/PATCH/OPTIONS; `AllowCredentials`.

## 5. INFRASTRUCTURE LAYER

### EF Core / persistência
- `AppDbContext` schema-agnostic (sem `HasDefaultSchema`; schema vem do `Search Path` da connection), `UseSnakeCaseNamingConvention`, `MigrationsHistoryTable("__EFMigrationsHistory")`. Configs por entidade em `Persistence/Configurations` (`ApplyConfigurationsFromAssembly`). DbSets para 29 entidades EF (incl. `AssinaturaTreinador`/`PagamentoTreinador`); `TreinoExercicio`/`ExecucaoExercicio` internal (composição). Cross-ref [specification-db] (schema/migrations/enums).
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
- `Infrastructure/Handlers/` (projeção billing / orquestração): `VinculoAprovadoCriarAssinaturaAlunoHandler` (handler de `VinculoAprovadoEvent` que CRIA a `AssinaturaAluno` — único produtor real; PULA se `treinador.ModoPagamentoAluno==Externo`; defense-in-depth: re-checa `ContaRecebimento.OnboardingCompleto` p/ cobrir evento legado/reprocessado, gate primário fica no `AprovarVinculoHandler`), `AlunoRegistradoSincronizarAssinanteHandler`, `AlunoAtualizadoSincronizarAssinanteHandler`, `PagamentoTreinadorPagoHandler`. Este último (handler de `PagamentoTreinadorPagoEvent`) ramifica por `Finalidade`: `Cadastro`→no-op (finalização inline no webhook Stripe p/ atomicidade); `Renovacao`→`RegistrarPagamentoRegularizado` + `AgendarProximaCobranca(+1mês)`; `TrocaPlano`→resolve plano alvo, regulariza se inadimplente, `TrocarPlanoImediato`. **Os 5 eventos `AssinaturaTreinador*` NÃO têm handler registrado** (sem notificação/projeção dedicada; efeito orquestrado por use cases + `PagamentoTreinadorPagoHandler`).
Catálogo de eventos × produtores em [specification-model].

### Seed (`Infrastructure/Seed/DataSeeder`)
Idempotente (insere só o que falta). Contagens (grupos/exercícios/planos/admin) em [specification-db] §DICAS. Notas Application: exercícios globais nascem `TreinadorId=null`; SuperAdmin via `Seed:AdminEmail` (default `admin@forzion.tech`) + `Seed:AdminPassword` obrigatório (throw se ausente; conta criada já com e-mail verificado, eventos limpos). Roda só em Development/Homolog (Program.cs).

### Outros
- `Infrastructure/Logging/ErrorLogDbSinkProvider`: `ILoggerProvider` que persiste erros (registrado fora de Test).
- `Infrastructure/Health/` (`HealthReportCollector`/`HealthReportSender`), `Persistence/AppDbContextFactory` (design-time para migrations).

## 6. CONVENÇÕES
- Conventional Commits + scopes + gates pré-PR: ver AGENTS.md (§CONVENÇÕES-CHAVE / §FLUXO) e [specification-git]; commitlint vive em `frontend/`.
- Política de erro (Result vs exception), `TimeProvider` (nunca `DateTime.UtcNow`), FluentValidation, DI manual/scoped de handlers/repos/event-handlers: canônico em §2/§3/§5 — não re-listado aqui.

## 7. TESTES (backend) — resumo
`forzion.tech.Tests` (xUnit `2.9.3`). Frameworks: Moq, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory; ambiente `Test`), Testcontainers.PostgreSql (Integration/E2E/Infra — exigem Docker), `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`), CsCheck (property-based), Verify.Xunit (snapshot), NetArchTest.Rules (arquitetura — `Architecture/LayeringTests.cs` + `ConventionTests.cs`). Pastas: `Api`, `Application`, `Architecture`, `Builders`, `Domain`, `E2E`, `Infrastructure`, `Integration`. Split por trait `Category=Integration` (`--filter "Category!=Integration"` roda os unit sem Docker, ~1000+). Cross-ref README para harness completo.
