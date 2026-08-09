# specification-observability — observabilidade & monitoramento (forzion.tech)
DOC PARA AGENTES. Fonte de verdade de logging estruturado, health checks, relatório de saúde diário, RUM frontend, performance budgets e alerting. Formato denso, agent-oriented. Cross-ref: [specification-backend], [specification-infrastructure], [specification-frontend], [specification-tests].

## MANUTENÇÃO DESTE ARQUIVO
- ATUALIZAR quando mexer em: política de logging/níveis, sink de log, `/health` (liveness) ou `/health/ready` (readiness/checks), relatório de saúde (collector/sender/scheduler/snapshot/endpoints), RUM/Web Vitals, Sentry (init/replay/tracing/CSP), perf budgets (lighthouse), métricas/tracing/alerting.
- Revisão obrigatória (não às cegas). Manter atualizado na MESMA tarefa que altera o código.
- NÃO duplicar infra §OBSERVABILITY (curta: healthcheck/Sentry/smoke) — este doc EXPANDE e REFERENCIA. NÃO duplicar [specification-backend] §hosted services/§middleware — referenciar.

## ESTADO GERAL (resumo)
- Observabilidade É APP-LEVEL, não plataforma. Sem stack dedicada (sem Prometheus/Grafana/Loki/OTel/APM backend).
- Backend: logging estruturado via `ILogger` + sink ERROR→DB + sink ERROR→Sentry (ambos `>=Error`, coexistem, nenhum substitui o outro — T4/#375, 2026-08-08); `/health` liveness + `/health/ready` readiness (DbContextCheck + SchemaHealthCheck + StripeHealthCheck + ResendHealthCheck + WhatsAppHealthCheck); relatório de saúde diário por e-mail; alert de chargeback via LogCritical (agora chega no Sentry, ver §1/§6); auth-failure LogWarning (R1-R4, **abaixo do teto `>=Error` dos dois sinks — segue só no stdout**, T4 não muda isso); auditoria durável de ações privilegiadas (R10-R14, `logs_aprovacao`). **Sentry backend**: `Sentry.Extensions.Logging` (`AddSentry` no `ILoggingBuilder`, NÃO `Sentry.AspNetCore` — sem middleware, sem captura automática de request/user, menor superfície de PII), `Sentry:Dsn` ausente = no-op + `LogWarning` no boot (mesmo padrão `NullEmailService`, nunca fail-fast — Sentry é observabilidade, não caminho de negócio). Projeto Sentry dedicado (org `forziontech`, distinto do projeto do frontend) — ver §1.
- Frontend: Sentry (erros + tracing + Session Replay) + Web Vitals RUM; Lighthouse CI semanal (budgets). DSN ativo em PROD (`vars.PROD_SENTRY_DSN`, build-arg de `release-images.yml`). Homolog tem o DSN no `.env` da VM mas a imagem de lá **ainda não foi rebuildada** (`NEXT_PUBLIC_*` é build-time) ⇒ homolog segue cego. Source map NÃO sobe em prod (§4 `Sentry init`). Sentry cobre só o FRONTEND — não é substituto do agregador de backend que falta acima.
- Gates LGPD: Sentry no browser só com consentimento analytics; ver [specification-lgpd]. Gate `Null/no-op` sem DSN.
- PII: nenhum e-mail/telefone cru em qualquer nível de log; mascaramento em fonte via `MascaraPii`; chokepoint Scrub no `HealthReportCollector` antes do relatório de saúde.

## 1. LOGGING ESTRUTURADO (backend)
### Sink ERROR→DB
- `Infrastructure/Logging/ErrorLogDbSinkProvider.cs` — `ILoggerProvider` custom. Registrado em `Api/Extensions/DependencyInjectionExtensions.cs` SÓ fora de env `Test` como singleton concreto + forward `AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<ErrorLogDbSinkProvider>())` (junto com `AddInfrastructure` + hosted services).
- `IsEnabled`: `logLevel >= LogLevel.Error` (Error + Critical apenas). Persiste em `error_logs` via `ErrorLogEntry.Criar(...)` + `AppDbContext.SaveChangesAsync` num scope próprio (`IServiceScopeFactory`).
- **Canal bounded + worker** (capacidade/`TamanhoLote` no `ErrorLogDbSinkProvider.cs`): `Log` enfileira `LogEntry` num `Channel<LogEntry>` (`FullMode=Wait`); worker background drena e persiste. Overflow descarta o item novo (via `TryWrite`) e incrementa `DropsContados` (nunca silencioso). `catch {}` no `PersistirLoteAsync` engole tudo — NUNCA propaga nem loga (evita recursão).
- **Persistência em LOTE** (PERF-07): `ProcessarCanalAsync` drena itens disponíveis numa janela → `PersistirLoteAsync` faz 1 `SaveChangesAsync` no MESMO `AppDbContext` (round-trip único por lote, `CreatedAt` lido 1×/lote). Linhas idênticas ao modo 1-a-1 (lote só agrupa I/O).
  - **BLAST RADIUS ACEITO** (PERF-07): o `catch {}` do `PersistirLoteAsync` engole a falha do `SaveChanges` do lote inteiro → uma falha transitória de DB descarta o lote inteiro de uma vez (1-a-1 perdia 1), SEM incrementar `DropsContados` (contador é só do overflow do canal, não de falha de persistência) = perda silenciosa do lote. ACEITO (`error_logs` é best-effort, não fonte-de-verdade; propagar dentro do sink recursaria). Hardening futuro: fallback per-entry ou retry curto antes de descartar.
  - GOTCHA (TST-02/03): `FullMode` PRECISA ser `Wait`, NÃO `DropWrite`/`DropOldest`/`DropNewest`. Sob os modos `Drop*`, `TryWrite` "abre espaço" e retorna `true` mesmo ao lotar → o `if (!TryWrite) Increment(DropsContados)` NUNCA dispara (descarte silencioso, contador morto). Só `Wait` faz `TryWrite` (não-bloqueante) retornar `false` quando cheio, permitindo contar o drop.
- **Drain no shutdown**: `RegistrarDrenoNoShutdown(IHostApplicationLifetime)` chamado pelo `ErrorLogDbSinkDrenoService` (`IHostedService`) no `StartAsync`, NÃO no ctor do provider. WHY: injetar `IHostApplicationLifetime` no ctor de um `ILoggerProvider` fecha ciclo de DI que aborta `host.Build()`; hosted services resolvem após o host construído. `ApplicationStopping.Register` → ao SIGTERM `Writer.TryComplete()` + `Wait(5 s)` (timeout evita travar shutdown com DB indisponível).
- **Anti-recursão**: ignora categorias com prefixo `Microsoft.EntityFrameworkCore`, `Npgsql`, `forzion.tech.Infrastructure.Logging` (logs gerados pela própria gravação).
- Campos persistidos em `error_logs` (`OcorridoEm`/`Nivel`/`Origem`/`Mensagem`): mapeamento re-derivável do `ErrorLogEntry.Criar`/sink.
- **PRIVACIDADE — sem PII crua em qualquer nível**: NUNCA logar e-mail, telefone, nome, token ou segredo em qualquer `Log*` call. Identificadores opacos (`ContaId`/`Guid`) ou pseudônimos (hash). Estende a Warning/Info que iriam a um agregador externo — SE existisse um (não existe no backend hoje, ver ESTADO GERAL). Front mascara Session Replay (§Sentry `maskAllText`).
  - **Mascaramento em fonte via `MascaraPii`** (`Infrastructure/Common/MascaraPii.cs`): `MascaraPii.Email(e)` → `a***@dominio.com` / `"(vazio)"` / `"***"`; `MascaraPii.Telefone(t)` → `***7766` (últimos 4) / `"(vazio)"` / `"***"`. Usado em todos os call-sites: `ResendEmailService`, `NullEmailService`, `MetaWhatsAppCloudNotifier`, `NullWhatsAppNotifier`, `EnvironmentEmailDecorator`, `EnvironmentWhatsAppDecorator`, `HealthReportSender`, `EsqueceuSenhaHandler`, `ReenviarVerificacaoHandler`, `SolicitarTrocaEmailHandler`.
  - **Chokepoint Scrub no `HealthReportCollector`** (`private static string? Scrub(string?)`): regex email→`[email]` (`\b[\w.+-]+@[\w-]+\.[\w.-]+\b`) + regex sequência ≥7 dígitos→`[num]` (`\d{7,}`) aplicado em `outbox_efeitos.UltimoErro` (seção Outbox) e `error_logs.Mensagem` (seção Erros) ANTES de construir o relatório de saúde — defense-in-depth contra PII acidental em stack traces.
- Consumido pelo relatório de saúde (§3, seção Erros) — janela 24h.

### Sink ERROR→Sentry (T4/#375, 2026-08-08)
- `Api/Extensions/DependencyInjectionExtensions.cs` `AddSentryLogging` (extraído de `AddApiServices` pra ser testável sem levantar o grafo de DI inteiro) — registrado no MESMO ponto que o sink de DB, fora de env `Test`. **Coexiste** com `ErrorLogDbSinkProvider`: nenhum sink substitui o outro, ambos recebem o mesmo `ILogger.Log*` global.
- Pacote `Sentry.Extensions.Logging` (`AddSentry` no `ILoggingBuilder`) — NÃO `Sentry.AspNetCore`: essa outra variante adiciona middleware completo (tracing automático, captura de request/headers/user), superfície de PII maior que o necessário pra "só mais um sink de log".
- `Sentry:Dsn` (config, override `Sentry__Dsn` nos compose — mesma convenção `Resend__ApiKey`) ausente/em branco → `AddSentryLogging` retorna sem registrar nada (no-op); `Program.cs` loga 1 `LogWarning` no boot fora de env `Test`. Nunca fail-fast — diferente do e-mail (`ResendEmailService`, que aborta boot em prod sem `Resend:ApiKey`): Sentry é observabilidade pura, elevar a disponibilidade da app à de um serviço terceiro só de telemetria não se justifica.
- Opções travadas: `SendDefaultPii = false` (explícito — não é o default do SDK em toda integração); `MinimumEventLevel = LogLevel.Error` (mesmo teto do sink de DB — Warning/Info não chegam); `MinimumBreadcrumbLevel = LogLevel.None` (breadcrumb DESLIGADO — só o evento de erro em si sai da app; breadcrumb ligado replicaria qualquer `LogInformation` existente, não escrutinado por PII do jeito que `MascaraPii.Scrub` escrutina o texto final do erro). `Environment` = `IWebHostEnvironment.EnvironmentName` (não repetido como var de compose separada).
- `SetBeforeSend` → `ScrubPii` (`DependencyInjectionExtensions.cs`, `internal`, testado direto via `InternalsVisibleTo`): roda `MascaraPii.Scrub` em `@event.Message.Formatted` e em cada `SentryException.Value` (`Sentry.Protocol.SentryException`) ANTES do evento sair do processo — mesma função usada pelo sink de DB e pelo `HealthReportCollector.Scrub` (§ acima), não uma cópia.
- Projeto Sentry DEDICADO (org `forziontech`, distinto do projeto do frontend — §4): alertas/issues de .NET não se misturam com stack de React, rate limit do free tier não é compartilhado entre os dois.
- Testes: `Tests/Api/Extensions/DependencyInjectionExtensionsSentryTests.cs` — DSN ausente/em branco não registra `ILoggerProvider`; DSN presente não lança; `ScrubPii` mascara e-mail/telefone em mensagem e em texto de exceção.

### LoggerMessage source-gen
- Padrão preferido para hot-path: `[LoggerMessage(...)]` partial methods (alocação zero, source-gen). Ex.: `Api/Middleware/GlobalExceptionHandler.cs` (`partial class`).
- Demais call-sites usam `ILogger.Log*` direto (ex.: `RelatorioSaudeDiarioService`, `HealthReportSender`, alert handlers).

### Política de nível (GlobalExceptionHandler)
- `Api/Middleware/GlobalExceptionHandler.cs` (`IExceptionHandler`). Após `MapException → statusCode`:

| Condição | Método | Nível |
|---|---|---|
| `statusCode >= 500` | `LogErroInesperado(logger, exception, message)` | `Error` (→ sink DB) |
| `statusCode < 500` | `LogErroDominio(logger, type, message)` | `Warning` (NÃO persiste) |
| `ValidationException` | retorno antecipado `ValidationProblemDetails` 400 | sem log |

- Racional: erros de domínio esperados (404/403/409/422) são `Warning` e ficam fora do `error_logs`; só falhas inesperadas (500) viram registro de erro. Mapeamento exceção→status: ver [specification-backend] §middleware.

### Categorias
- Categoria = nome completo do tipo do `ILogger<T>` (convenção .NET). Sink filtra por prefixo (acima). Sem categorias customizadas além das de namespace.

### Auth-failure LogWarning (R1-R4)
Sem PII crua em nenhum campo; conta-não-encontrada NÃO loga identificador. **Destino hoje: stdout do container apenas** — `LogWarning` fica abaixo do teto `>=Error` dos dois sinks (DB e Sentry, §1), T4/#375 não muda isso.
| Handler | Condição | Campos estruturados |
|---|---|---|
| `LoginHandler` | conta não encontrada | — (sem identificador) |
| `LoginHandler` | senha inválida | `ContaId` |
| `LoginHandler` | e-mail não verificado | `ContaId` |
| `RenovarSessaoHandler` | refresh token inválido | — |
| `RenovarSessaoHandler` | reuso detectado (família revogada) | `FamiliaId` |
| `RenovarSessaoHandler` | perfil indisponível | `ContaId` |
| `CompletarLoginMfaHandler` | verificação MFA falhou | `ContaId`, `Fator` |
| `VerificarStepUpHandler` | verificação step-up falhou | `ContaId` |

### Auditoria durável de ações privilegiadas (R10-R14)
`LogAprovacao` gravado NA MESMA transação da ação (antes de `CommitAsync`) + `LogInformation` correlacionado. Falha ao gravar → aborta a ação (fail-closed). Actor: `IUserContext.ContaId` (self-service / owner) ou `IUserContext.PerfilId` (admin).
- **14 `TipoAcaoAprovacao`** (texto mapeado em runtime, sem nova migration):

| Valor enum | Origem |
|---|---|
| `DefinicaoDadosFiscaisTreinador` | `DefinirDadosFiscaisTreinadorHandler` (admin actor: `PerfilId`) |
| `MfaHabilitado` | `ConfirmarEnrollTotpHandler` (self: `ContaId`) |
| `MfaDesabilitado` | `DesabilitarMfaHandler` (self: `ContaId`) |
| `RecoveryCodesRegenerados` | `RegenerarRecoveryCodesHandler` (self: `ContaId`) |
| `SenhaAlterada` | `AlterarSenhaHandler` (self: `ContaId`) |
| `SenhaRedefinida` | `RedefinirSenhaHandler` (self: `ContaId`) |
| `EmailAlterado` | `ConfirmarTrocaEmailHandler` (self: `ContaId`) |
| `ExclusaoGrupoMuscular` | `ExcluirGrupoMuscularHandler` (admin actor: `PerfilId`) |
| `ExclusaoExercicio` | `ExcluirExercicioHandler` (owner treinador: `TreinadorId`) |
| `ExclusaoPacote` | handler de pacote (owner treinador) |
| `InativacaoPlanoPlataforma` | `ExcluirPlanoPlataformaHandler` (admin actor: `PerfilId`) |
| `AlteracaoStatusAluno` | `AlterarStatusAlunoHandler` (treinador owner) |
| `AlteracaoModoPagamentoTreinador` | handler modo pagamento (owner treinador) |

## 2. HEALTH CHECKS (`/health` liveness + `/health/ready` readiness)
- Registro `Api/Extensions/DependencyInjectionExtensions.cs`: `services.AddHealthChecks().AddDbContextCheck<AppDbContext>("db", tags: ["ready"]).AddCheck<SchemaHealthCheck>("schema", tags: ["ready"]).AddCheck<StripeHealthCheck>("stripe", tags: ["ready"]).AddCheck<ResendHealthCheck>("resend", tags: ["ready"]).AddCheck<WhatsAppHealthCheck>("whatsapp", tags: ["ready"])` (pacotes `...HealthChecks.EntityFrameworkCore` + `...HealthChecks` 8.0.11; `AddHttpClient()` garante `IHttpClientFactory` p/ os probes externos). Check `db` = `CanConnectAsync` do `AppDbContext`, só executado quando o endpoint readiness é chamado. Check `schema` (`SchemaHealthCheck`) = `SELECT current_schema()` vs schema esperado (`MigrationHistorySchemaResolver.Resolve` da connection string `AppConnection`); `Unhealthy` em divergência (sinal de Transaction pooler :6543 que perde search_path), `Healthy` se Search Path não fixado (não verificável). `stripe`/`resend`/`whatsapp` = probe externo com timeout 3s, `Degraded` em falha (nunca `Unhealthy`), `Healthy` quando a integração não está configurada — EXCETO `resend` em Production: não-configurado → `Unhealthy` (e-mail transacional viraria no-op silencioso; espelha o fail-fast de `AddInfrastructure`, que lança se `Resend:ApiKey` ausente em prod). `WhatsAppHealthCheck`: GET `https://graph.facebook.com/{version}/{phoneNumberId}?fields=id` com Bearer token; não-configurado → `Healthy("WhatsApp não configurado.")`; falha/timeout/não-2xx → `Degraded`. Em ambiente `Test` o `AddInfrastructure` é pulado → `AppDbContext` não registrado → o check `db` só roda se o teste o registrar (ver `Tests/Api/Endpoints/HealthEndpointsTests.cs`); `stripe`/`resend`/`whatsapp` sem chave retornam `Healthy`.
- Mapeamento `Api/Extensions/RouteBuilderExtensions.cs` `MapHealthCheck` (ambos `AllowAnonymous().RequireRateLimiting("read")`; ordem no pipeline: depois de auth/authz/rateLimiter — ver [specification-backend] §UseApiConfiguration):
  - LIVENESS `/health`: `HealthCheckOptions { Predicate = _ => false }` → nenhum check; `200` enquanto o processo ASP.NET Core responde. NÃO checa DB/integrações. CONTRATO ESTÁVEL (consumido por compose/frontend) — não alterar.
  - READINESS `/health/ready`: `HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") }` → executa os 5 checks tag `ready` (semântica por check já descrita acima). DB indisponível OU schema divergente = `503 Unhealthy` (corta tráfego); `Degraded` (Stripe/Resend/WhatsApp) mapeia p/ `200` por padrão (dependência externa instável não tira o pod de rotação). `Infrastructure/Health/{Schema,Stripe,Resend,WhatsApp}HealthCheck.cs` (OBS-02).
- Consumido por: compose healthcheck `GET :8080/health` (liveness) + frontend `depends_on: backend healthy`. NÃO duplicar — ver [specification-infrastructure] §OBSERVABILITY (`docker-compose.homolog.yml` curl; `docker-compose.yml` via /dev/tcp; `docker-compose.server.yml` curl). Compose/frontend `depends_on` continuam em `/health` (decisão: liveness puro).
- Smoke pós-deploy bate em `/health`: `Tests/E2E/SmokeTests.cs` + `.github/workflows/smoke.yml` (`workflow_run` após CI/CD). README §endpoints documenta `GET /health → 200 Healthy`. Testes unit (sem Docker): `Tests/Api/Endpoints/HealthEndpointsTests.cs` cobre `/health` (200) e `/health/ready` (200 com `AppDbContext` EF InMemory).

## 3. RELATÓRIO DE SAÚDE DIÁRIO (DB ping + KPIs + e-mail)
Pipeline distinto do `/health`: coleta profunda (DB connect real, KPIs, entregabilidade, erros 24h), snapshot persistido, envio por e-mail.

### Collector
- `Infrastructure/Health/HealthReportCollector.cs` (`IHealthReportCollector.ColetarAsync(config, ct)`). Seções OPCIONAIS por flag do `HealthReportConfig` (campos por seção re-deriváveis do collector): **Liveness** (`IncluirLiveness`) · **KPIs** (`IncluirKpis`) · **Entregabilidade** (`IncluirEntregabilidade`, via `IEmailDeliveryLogRepository`, 24h — [specification-email]) · **Erros** (`IncluirErros`, `error_logs` 24h, alimentado pelo sink §1) · **Outbox** (`outbox_efeitos` por status + amostras `Falhou`). Invariantes não-óbvios:
  - **KPIs = COUNTs SEQUENCIAIS**: DbContext NÃO é thread-safe → sem `Task.WhenAll` (decisão comentada no código).
  - **Outbox reusa a flag `IncluirErros`** (mesmo sinal de falha operacional) — sem coluna/flag dedicada (evita migração de `HealthReportConfig`). Ver [specification-backend] §3.1.
- `Ambiente` ← `ASPNETCORE_ENVIRONMENT` (`"Unknown"` fallback). `CapturadoEm` ← `TimeProvider` UTC.
- `StatusGeral` (`DerivarStatus`): `!bancoAcessivel → Falha`; `erros.Total > 0` OU `outbox.Falhou > 0 → Degradado`; senão `Ok` (enum `StatusSaude`).
- Email `EmailTemplates.RelatorioSaude` renderiza `SecaoOutbox` (tabela de contagens + lista de falhas terminais com `UltimoErro` HTML-escapado). Outbox null (flag off) → seção omitida.

### Sender
- `Infrastructure/Health/HealthReportSender.cs` (`IHealthReportSender.EnviarAsync`). Assunto `[forzion.tech] Relatório de saúde — {Ambiente} ({StatusGeral})`; HTML via `EmailTemplates.RelatorioSaude(report)`. Itera destinatários; falha de envio individual → `LogError` (não aborta os demais). Ver [specification-email].

### Scheduler
- `Api/Services/RelatorioSaudeDiarioService.cs` — `BackgroundService`. Registrado `AddHostedService` fora de `Test`. Loop: `Task.Delay(15min)` → `ProcessarAsync`. `OperationCanceledException` → break; outras exceções → `LogError` e continua.
- `DeveEnviar(config, agoraUtc)` (static, testável): `false` se `!Ativo` OU `TimeOnly(agora) < HoraEnvioUtc` OU já enviado hoje (`UltimoEnvioEm.Date == agora.Date`). Garante 1 envio/dia após a hora-alvo.
- `ProcessarAsync` (ordering canônico, `specification-coding §1`): scope próprio → `config` via `IHealthReportConfigRepository.ObterAsync`; se null/!DeveEnviar → return. Senão: `Coletar` → `HealthSnapshot.Criar(...)` → `IHealthSnapshotRepository.AdicionarAsync` → `config.MarcarEnviado(agora)` → `IUnitOfWork.CommitAsync` (1º commit) → (try) `Sender.EnviarAsync(destinatarios)` + `LogInformation` (catch `OperationCanceledException` → propaga SEM marcar `EmailEnviado`; catch demais → `LogCritical`, não reverte estado) → `snapshot.MarcarEmailEnviado(sucesso)` → `IUnitOfWork.CommitAsync` (2º commit, persiste o resultado real do envio). Efeito externo SEMPRE depois do 1º commit — commit pós-envio faltando duplicaria e-mail no ciclo seguinte.
- `ExecutarRelatorioSaudeHandler` (`Application/UseCases/Admin/HealthReport/`, endpoint admin `POST /admin/health-report/run`, execução sob demanda): mesmo ordering do bullet acima (2 commits + try/catch em torno do envio); delta: retorna `Result.Success` com `HealthSnapshotResponse.EmailEnviado` refletindo o resultado real (200 mesmo se o e-mail falhou — snapshot já foi persistido no 1º commit).
- Testes: `Tests/Api/Services/RelatorioSaudeDiarioServiceTests.cs` (cobre `DeveEnviar` + ordering + `EmailEnviado`); `Tests/Application/Admin/HealthReport/ExecutarRelatorioSaudeHandlerTests.cs` (ordering + catch + `EmailEnviado`).

### Snapshot (armazenamento)
- `Domain/Entities/HealthSnapshot.cs`: `Ambiente`, `StatusGeral` (`StatusSaude`), `PayloadJson` (relatório serializado via `HealthReportPayload.Serializar`), `EmailEnviado` (`bool?`, null=não rastreado). Factory `Criar` valida `Ambiente`/`Payload` obrigatórios (`HealthErrors`). Persistido a cada envio → histórico consultável.

### Config (entidade)
- `Domain/Entities/HealthReportConfig.cs`: `Ativo`, `HoraEnvioUtc` (`TimeOnly`), `Destinatarios` (CSV; `ObterDestinatarios()` split/trim), 4 flags `Incluir*`, `UltimoEnvioEm`. `MarcarEnviado(agora)` seta `UltimoEnvioEm`+`UpdatedAt`. Singleton lógico (repo `ObterAsync` sem id).

### Endpoints admin
- `Api/Endpoints/Admin/HealthReportEndpoints.cs` — group `/admin/health-report`, `RequireAuthorization("SystemAdmin")` + `RequireRateLimiting("write")`:

| Método | Rota | Handler | Resposta |
|---|---|---|---|
| GET | `/config` | `ObterHealthReportConfigHandler` | `200 HealthReportConfigResponse` / `204` se não configurado |
| PUT | `/config` | `AtualizarHealthReportConfigHandler` (`AtualizarHealthReportConfigCommand`) | `200` / `400` validação |
| GET | `/snapshots` | `ListarHealthSnapshotsHandler` (query `limite`) | `200 HealthSnapshotResponse[]` |
| POST | `/run` | `ExecutarRelatorioSaudeHandler` (executa imediato) | `200 HealthSnapshotResponse` / `422` |

- Frontend: `frontend/src/lib/api/admin.ts` (clientes) + página `app/(admin)/admin/saude`. Testes: `Tests/Api/Endpoints/HealthReportEndpointsTests.cs`, `admin.health.test.ts`, `saude/page.client.test.tsx`. Ver [specification-frontend].

## 4. FRONTEND RUM (Sentry + Web Vitals)
### Web Vitals
- `frontend/src/components/observability/WebVitals.tsx` — `"use client"`, montado 1× no root layout, sem UI. `useReportWebVitals` (next/web-vitals) → cada métrica (LCP, CLS, INP, FCP, TTFB) vira `Sentry.addBreadcrumb({ category: "web-vitals", level: "info", data: {value,id,label} })`. Breadcrumb anexa ao erro/replay da mesma sessão (contexto de perf). Core vitals de pageload também podem ser coletados pelo `browserTracingIntegration` — integração DEFAULT do `@sentry/nextjs` (ativa via `tracesSampleRate`), NÃO registrada explicitamente em `instrumentation-client.ts` (que tem `integrations:[]`; o replay é adicionado lazy por `ReplayManager` — §4). Agregação p75/dashboards = gap (Fase 18 / §6).

### Sentry init (gates + no-op)
- `next.config.ts` `withSentryConfig`: plugin de build. Source maps SÓ com `SENTRY_AUTH_TOKEN` (`sourcemaps.disable = !TOKEN`) → `next build` em dev/CI sem token funciona. `silent: !CI`, `disableLogger: true`, `widenClientFileUpload: true`. Org/project/authToken via env.
- **Upload de source map de PRODUÇÃO** (2026-08-08): `frontend/Dockerfile` monta `SENTRY_AUTH_TOKEN` como BuildKit secret (`RUN --mount=type=secret,id=SENTRY_AUTH_TOKEN,env=SENTRY_AUTH_TOKEN npm run build`) — disponível só durante aquele `RUN`, nunca vira layer nem entra no cache exportado (`cache-to: type=gha,mode=max` do `release-images.yml`). `SENTRY_ORG`/`SENTRY_PROJECT` (slugs, não-segredo) via `ARG`/`ENV` normal no mesmo stage `builder`, descartado no final (`runner` nasce de `FROM` limpo + `COPY --from=builder`). **NÃO usar `ARG`/`ENV` pro token** — mesmo descartado do stage final, o `cache-to:mode=max` exporta TODAS as camadas (inclusive `builder`) pro cache do GHA, que persiste entre runs; secret mount tem garantia mais forte (BuildKit nunca escreve em layer NEM em cache, disco nenhum). Verificado localmente via A/B do hook `runAfterProductionCompile` (Turbopack sempre usa upload pós-build): 496ms sem secret (no-op) vs 25.5s com secret+org/project de teste (upload tentado) — token inválido não quebra o build (`silent` engole o erro), então um token expirado em prod só perde o source map, não derruba o deploy. `SENTRY_ORG`/`SENTRY_PROJECT`/`SENTRY_AUTH_TOKEN` no `release-images.yml` job `frontend`.
- Runtimes (3 inits, todos gated `NEXT_PUBLIC_SENTRY_DSN` → `enabled: Boolean(dsn)`; **no-op completo sem DSN** = dev/CI sem config):

| Arquivo | Runtime | Notas |
|---|---|---|
| `instrumentation-client.ts` | browser | `enabled: Boolean(dsn) && analyticsConsented` (LGPD opt-in via `readConsentCookie().analytics===true`, padrão OFF); `integrations:[]` (Session Replay é lazy via `ReplayManager`); `onRouterTransitionStart` (tracing de navegação App Router) |
| `sentry.server.config.ts` | node | importado por `instrumentation.ts` (`register`) quando `NEXT_RUNTIME==="nodejs"` |
| `sentry.edge.config.ts` | edge | importado quando `NEXT_RUNTIME==="edge"` |
| `instrumentation.ts` | hook | `register()` carrega config por runtime; `onRequestError = captureRequestError` (erros SSR/RSC/route handlers) |

- Comuns: `environment ← NEXT_PUBLIC_SENTRY_ENV ?? NODE_ENV`; `tracesSampleRate ← env ?? 0.05` (default safe-by-default — sem env var em prod NÃO cai em 10%; subir custa egress/CPU no VPS 2-vCPU); `sendDefaultPii: false` (LGPD: sem IP/cookies/headers). Vars de override documentadas em `.env.example`.
- **Session Replay** (só browser, LAZY): `replaysSessionSampleRate ← env ?? 0.02` (default safe; replay grava DOM → o rate baixo limita egress + exposição de PII/dados de saúde, defesa-em-profundidade c/ mask/block + gate de consentimento) e `replaysOnErrorSampleRate: 1.0` (100% em erro — alto valor, baixo volume) ficam no `init` (a replayIntegration adicionada tardiamente as respeita — padrão oficial de lazy-load). A `replayIntegration` NÃO está no `integrations:[]` estático: o `init` não importa o worker do replay. É adicionada on-idle por `ReplayManager` (§ abaixo) via `import("@sentry/nextjs").then(m => Sentry.addIntegration(m.replayIntegration({ maskAllText:true, blockAllMedia:true })))` — webpack/turbopack splita o bundle pesado do replay (rrweb, ~62KB gz / chunk ~410KB raw) num chunk **async**, fora do critical path de toda página. Verificado via `ANALYZE=true npm run build`: o chunk do replay não é referenciado por nenhum entry de página/layout (`app-build-manifest`), só carrega sob dynamic import. Mask/block → não vaza dados de usuário (LGPD).
- **NÃO usar `lazyLoadIntegration()`**: ela baixa o bundle do **CDN da Sentry** → violaria a CSP `script-src 'self'` (sem origem de CDN) e exigiria abrir nova origem. O dynamic import self-hosted mantém a CSP intacta (ver [specification-security] §3).
- **`ReplayManager`** (`src/components/observability/ReplayManager.tsx`, `"use client"`, montado 1× no root layout, sem UI): dono do lazy-load + gating por rota/consent. `desired = dsn && consent.analytics && !isReplayDenied(pathname)` (via `useConsent` + `usePathname`). `desired && !added` → on-idle (`requestIdleCallback`, fallback `setTimeout`) dynamic import + `addIntegration`; `desired && added && !recording` → `getReplay()?.start()`; `!desired && recording` → `await getReplay()?.stop()` (cobre navegação SPA PRA rota sensível). Refs `added`/`recording` evitam churn; sem consentimento o dynamic import NÃO dispara (chunk de 62KB não baixa pra quem não consentiu). `import` com `.catch` silencioso (replay é best-effort — telemetria não quebra a app). Denylist: `src/lib/observability/replayDenylist.ts` (`REPLAY_DENYLIST=['/admin/saude','/cadastro/aluno']`, match por segmento — ver [specification-lgpd]).
- Gate LGPD: replay/RUM no browser exige consentimento analytics. Ver [specification-lgpd].

### CSP (Sentry)
- CSP completo + 3 camadas de headers: CANÔNICO em [specification-security] §3 (`next.config.ts buildCsp`, inclui `font-src 'self'`). Diretivas Sentry-relevantes: `connect-src 'self' https://api.stripe.com https://*.sentry.io` (ingest erros/replay/tracing; no-op sem DSN) + `worker-src 'self' blob:` (worker do Session Replay). CSP só enforcing (sem cópia Report-Only — ver [specification-security] §3).

## 5. PERFORMANCE BUDGETS (Lighthouse CI)
### Budgets (`frontend/lighthouserc.json`)
- `collect`: preset `desktop`, `numberOfRuns: 3`, `skipAudits: ["uses-http2"]`, chromeFlags `--no-sandbox --disable-dev-shm-usage`; servidor local via `npm run start` (`startServerReadyPattern: "Ready in"`).
- `assert` (preset `lighthouse:recommended` + overrides):

| Assertion | Nível | Threshold |
|---|---|---|
| `categories:performance` | error | minScore 0.85 |
| `categories:accessibility` | error | minScore 0.95 |
| `categories:best-practices` | error | minScore 0.90 |
| `categories:seo` | warn | minScore 0.80 |
| `largest-contentful-paint` (LCP) | error | ≤ 2500 ms |
| `cumulative-layout-shift` (CLS) | error | ≤ 0.1 |
| `total-blocking-time` (TBT) | error | ≤ 300 ms |
| `interactive` (TTI) | warn | ≤ 3500 ms |
| `first-contentful-paint` (FCP) | warn | ≤ 1800 ms |
| `uses-text-compression` / `uses-rel-preconnect` / `csp-xss` | off | — |

- `upload.target: temporary-public-storage`.

### Cadência (`.github/workflows/lighthouse.yml`)
- `schedule` cron `0 6 * * 3` (Quarta 06:00 UTC — pós-deploys de terça em homolog) + `workflow_dispatch` (input `base_url`, fallback `vars.HOMOLOG_BASE_URL`; aborta sem URL). Node 22, `working-directory: frontend`.
- **Alvo = páginas PÚBLICAS homolog**: `/login`, `/cadastro/aluno`, `/cadastro/treinador` (URLs locais em `lighthouserc.json` `collect.url` incluem também `/`). `lhci collect --url=... ; lhci assert`. Report → artifact `lighthouse-report` (`.lighthouseci/`, `if: always()`). **NÃO cobre produção** — mesma lacuna estrutural do DAST ([specification-security] §8): a cadência agendada só bate em homolog; rodar contra prod exige `workflow_dispatch` manual com `base_url` de produção.
- Perf de runtime/SEO da app: ver [specification-frontend].

## 6. MÉTRICAS / TRACING / ALERTING — ESTADO ATUAL + GAPS
### Implementado
- **Alerting de chargeback**: `Infrastructure/Notifications/Alerts/PagamentoTreinadorEmDisputaAlertHandler.cs` (`IDomainEventHandler<PagamentoTreinadorEmDisputaEvent>`). `LogCritical` com campos estruturados (`PagamentoTreinadorId`, `TreinadorId`, `Valor`). Handler in-memory / best-effort (NOT durable): não persiste, não retenta, não é outbox. Registrado em `InfrastructureExtensions` como `AddScoped`. **RESOLVIDO parcialmente (T4/#375, 2026-08-08)**: `LogCritical` é `>=Error` → chega no sink Sentry (§1) além do `error_logs`, com alerta real (se `Sentry:Dsn` configurado). Regra de alerta/rota (quem é notificado, canal) ainda não versionada no repo — é config do dashboard Sentry, fora do código. Ver [specification-stripe].
- **Auditoria durável de ações privilegiadas**: `logs_aprovacao` — ver §1 "Auditoria durável" acima.
- **Dead-man switch dos crons (healthchecks.io)**: cada workflow agendado pinga `https://hc-ping.com/<HC_PING_KEY>/<slug>` no sucesso e `<slug>/fail` na falha (`|| true` — ping nunca quebra o job). `HC_PING_KEY` = **Ping Key do PROJETO** (Project Settings → Ping Key), secret de repo. Cobre o que nenhum gate cobre: cron que **deixou de disparar** (sem fallback interno — se o workflow morre, a renovação para; [specification-stripe] §CRON).
  - **Slug carrega o ambiente** (`<slug>-${{ matrix.env }}` nos 6 crons de matrix): com slug fixo, homolog e production pingariam o MESMO check e o sucesso de homolog marcaria "up" por cima da falha de production — mascarando exatamente o ambiente que importa. `db-backup` (DB-level, dumpa os 2 alvos num run) e `deploy-prod` (1 ambiente só) mantêm slug simples. Guard: `scripts/test/cron-env-parametrizacao-check.sh`.
  - **GOTCHA 404 silencioso**: a URL por slug responde **404 se o check não existir** — auto-provisionamento exige `?create=1`, que os workflows NÃO passam. Somado ao `|| true`, um slug inexistente vira no-op invisível. Os checks precisam ser criados à mão com o slug EXATO.
  - **Grace generoso é requisito, não preguiça**: cron do GitHub atrasa rotineiramente 5-60min (e mais sob incidente de plataforma). Grace apertado = alarme falso diário = alerta ignorado. Sugerido: 2h nos diários, 6h no semanal (`billing-reconciliation`, seg 04:00 UTC), 12h no mensal (`lgpd-purge`, dia 1 03:00 UTC).
  - **`deploy-prod` é event-driven** (`workflow_run`, não cron): cadastrar como period-based com período longo (365d), NÃO como cron — senão fica permanentemente down entre deploys. O valor dele é o ping `/fail`, que derruba o check na hora independente do período.
- Tracing frontend: Sentry browser tracing + replay (§4). É o único tracing existente — e cobre só o frontend (backend sem tracing/APM, ver GAPS).

### GAPS (o que falta para observabilidade madura) — itens marcados `0-rep` = 0 ocorrências no repo
- **Sem OpenTelemetry / APM backend** (`0-rep`): nenhum `AddOpenTelemetry`/`ActivitySource`/exporter. Sem distributed tracing backend↔frontend↔Stripe.
- **Sem métricas** (`0-rep`): nenhum Prometheus/`/metrics`/contador/histograma. KPIs só via relatório diário por e-mail (§3), não série temporal.
- **Correlation/request id: RESOLVIDO** (OBS-01): middleware em `RouteBuilderExtensions.cs` (antes de `UseAuthentication`) resolve `X-Request-Id` de entrada ou `TraceIdentifier`, abre `ILogger.BeginScope` e ecoa no header de resposta. Frontend propaga como tag Sentry (`instrumentation-client.ts beforeSend` lê `window.__lastRequestId`, gravado pelo interceptor do `apiClient`). Teste `Tests/Api/Endpoints/CorrelationMiddlewareTests.cs`.
- **Readiness DB+Schema+Stripe+Resend+WhatsApp: RESOLVIDO** — ver §2 (`/health/ready`: `Unhealthy` em DB down/schema divergente; `Degraded` em falha de Stripe/Resend/WhatsApp; `resend` `Unhealthy` se não-configurado em Production). `/health` permanece liveness puro. Gap anterior (verificação só no relatório diário) fechado.
- **Sink ERROR fire-and-forget: RESOLVIDO** (OBS-03) — canal bounded + worker com drain no `ApplicationStopping`. Ver §1.
- **Alerting reativo por log — parcialmente resolvido (T4/#375, 2026-08-08)**: chargeback (`LogCritical`) e qualquer 500 (`GlobalExceptionHandler` → `LogErroInesperado`, §1 "Política de nível") agora chegam no Sentry backend (§1 "Sink ERROR→Sentry"), além do `error_logs`. **Ainda GAP**: auth-failure (R1-R4) e reuse-detection de refresh token são `LogWarning` — abaixo do teto `>=Error` dos dois sinks, seguem só no stdout do container (T4 deliberadamente não baixou o teto — breadcrumb/evento de Warning ampliaria a superfície de PII sem revisão, ver §1). Sem regra de alerta/rota versionada no repo (config de dashboard Sentry). Sem alerta para DB down (readiness já reporta via `/health/ready`, §2, mas ninguém está inscrito num ping ativo) nem fila de e-mail atrasada.
- **RUM sem agregação**: Web Vitals viram breadcrumbs (contexto), não métrica agregada p75/dashboards (marcado "Fase 18" no código).
- Gates de teste/qualidade de observabilidade: ver [specification-tests].
