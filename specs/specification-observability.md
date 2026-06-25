# specification-observability — observabilidade & monitoramento (forzion.tech)
DOC PARA AGENTES. Fonte de verdade de logging estruturado, health checks, relatório de saúde diário, RUM frontend, performance budgets e alerting. Formato denso, agent-oriented. Cross-ref: [specification-backend], [specification-infrastructure], [specification-frontend], [specification-tests].

## MANUTENÇÃO DESTE ARQUIVO
- ATUALIZAR quando mexer em: política de logging/níveis, sink de log, `/health` (liveness) ou `/health/ready` (readiness/checks), relatório de saúde (collector/sender/scheduler/snapshot/endpoints), RUM/Web Vitals, Sentry (init/replay/tracing/CSP), perf budgets (lighthouse), métricas/tracing/alerting.
- Revisão obrigatória (não às cegas). Manter atualizado na MESMA tarefa que altera o código.
- NÃO duplicar infra §OBSERVABILITY (curta: healthcheck/Sentry/smoke) — este doc EXPANDE e REFERENCIA. NÃO duplicar [specification-backend] §hosted services/§middleware — referenciar.

## ESTADO GERAL (resumo)
- Observabilidade É APP-LEVEL, não plataforma. Sem stack dedicada (sem Prometheus/Grafana/Loki/OTel/APM backend).
- Backend: logging estruturado via `ILogger` + sink ERROR→DB; `/health` liveness + `/health/ready` readiness (DbContextCheck + StripeHealthCheck + ResendHealthCheck + WhatsAppHealthCheck); relatório de saúde diário por e-mail; alert de chargeback via LogCritical; auth-failure LogWarning (R1-R4); auditoria durável de ações privilegiadas (R10-R14, `logs_aprovacao`).
- Frontend: Sentry (erros + tracing + Session Replay) + Web Vitals RUM; Lighthouse CI semanal (budgets).
- Gates LGPD: Sentry no browser só com consentimento analytics; ver [specification-lgpd]. Gate `Null/no-op` sem DSN.
- PII: nenhum e-mail/telefone cru em qualquer nível de log; mascaramento em fonte via `MascaraPii`; chokepoint Scrub no `HealthReportCollector` antes do relatório de saúde.

## 1. LOGGING ESTRUTURADO (backend)
### Sink ERROR→DB
- `Infrastructure/Logging/ErrorLogDbSinkProvider.cs` — `ILoggerProvider` custom. Registrado em `Api/Extensions/DependencyInjectionExtensions.cs` SÓ fora de env `Test` como singleton concreto + forward `AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<ErrorLogDbSinkProvider>())` (junto com `AddInfrastructure` + hosted services).
- `IsEnabled`: `logLevel >= LogLevel.Error` (Error + Critical apenas). Persiste em `error_logs` via `ErrorLogEntry.Criar(...)` + `AppDbContext.SaveChangesAsync` num scope próprio (`IServiceScopeFactory`).
- **Canal bounded + worker**: `Log` enfileira `LogEntry` em `Channel<LogEntry>` (capacidade 1000, `FullMode=Wait`); worker background (`Task.Run`) drena e persiste. Overflow descarta o item novo (via `TryWrite`) e incrementa `DropsContados` (nunca silencioso). `catch {}` no `PersistirLoteAsync` engole tudo — NUNCA propaga nem loga (evita recursão).
- **Persistência em LOTE** (PERF-07): `ProcessarCanalAsync` drena até `TamanhoLote=100` itens disponíveis numa janela → `PersistirLoteAsync` faz 1 `SaveChangesAsync` no MESMO `AppDbContext` (round-trip único por lote, não por log; `CreatedAt` lido 1×/lote). Linhas persistidas idênticas ao modo 1-a-1 (lote só agrupa I/O).
  - **BLAST RADIUS ACEITO** (PERF-07): o `catch {}` do `PersistirLoteAsync` engole a falha do `SaveChanges` do lote inteiro → uma falha transitória de DB descarta ATÉ `TamanhoLote=100` entries de uma vez (1-a-1 perdia 1), e SEM incrementar `DropsContados` (contador é só do overflow do canal, não de falha de persistência) = perda silenciosa do lote. ACEITO (`error_logs` é best-effort, não fonte-de-verdade; propagar dentro do sink recursaria). Hardening futuro: fallback per-entry ou retry curto antes de descartar.
  - GOTCHA (TST-02/03, auditoria 2026-06-14): `FullMode` PRECISA ser `Wait`, NÃO `DropWrite`/`DropOldest`/`DropNewest`. Sob os modos `Drop*`, `TryWrite` "abre espaço" e retorna `true` mesmo ao lotar → o `if (!TryWrite) Increment(DropsContados)` NUNCA dispara (descarte silencioso, contador morto). Só `Wait` faz `TryWrite` (não-bloqueante) retornar `false` quando cheio, permitindo contar o drop. Era o bug real que o teste de overflow expôs.
- **Drain no shutdown**: `RegistrarDrenoNoShutdown(IHostApplicationLifetime)` chamado pelo `ErrorLogDbSinkDrenoService` (`IHostedService`) no `StartAsync`, NÃO no ctor do provider. WHY: injetar `IHostApplicationLifetime` no ctor de um `ILoggerProvider` fecha ciclo de DI que aborta `host.Build()`; hosted services resolvem após o host construído. `ApplicationStopping.Register` → ao SIGTERM `Writer.TryComplete()` + `Wait(5 s)` (timeout evita travar shutdown com DB indisponível).
- **Anti-recursão**: ignora categorias com prefixo `Microsoft.EntityFrameworkCore`, `Npgsql`, `forzion.tech.Infrastructure.Logging` (logs gerados pela própria gravação).
- Campos persistidos: `OcorridoEm` (UTC via `TimeProvider`), `Nivel` (`logLevel.ToString()`), `Origem` (categoria), `Mensagem` (formatter + `{ExceptionType}: {Message}` quando há exceção).
- **PRIVACIDADE — sem PII crua em qualquer nível**: NUNCA logar e-mail, telefone, nome, token ou segredo em qualquer `Log*` call. Identificadores opacos (`ContaId`/`Guid`) ou pseudônimos (hash). Estende a Warning/Info que vão a agregador externo (Sentry). Front mascara Session Replay (§Sentry `maskAllText`).
  - **Mascaramento em fonte via `MascaraPii`** (`Infrastructure/Common/MascaraPii.cs`): `MascaraPii.Email(e)` → `a***@dominio.com` / `"(vazio)"` / `"***"`; `MascaraPii.Telefone(t)` → `***7766` (últimos 4) / `"(vazio)"` / `"***"`. Usado em todos os call-sites: `ResendEmailService`, `NullEmailService`, `MetaWhatsAppCloudNotifier`, `NullWhatsAppNotifier`, `EnvironmentEmailDecorator`, `EnvironmentWhatsAppDecorator`, `HealthReportSender`, `EsqueceuSenhaHandler`, `ReenviarVerificacaoHandler`, `SolicitarTrocaEmailHandler`.
  - **Chokepoint Scrub no `HealthReportCollector`** (`private static string? Scrub(string?)`): regex email→`[email]` (`\b[\w.+-]+@[\w-]+\.[\w.-]+\b`) + regex sequência ≥7 dígitos→`[num]` (`\d{7,}`) aplicado em `outbox_efeitos.UltimoErro` (seção Outbox) e `error_logs.Mensagem` (seção Erros) ANTES de construir o relatório de saúde — defense-in-depth contra PII acidental em stack traces.
- Consumido pelo relatório de saúde (§3, seção Erros) — janela 24h.

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
Sem PII crua em nenhum campo; conta-não-encontrada NÃO loga identificador.
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
- **14 novos `TipoAcaoAprovacao`** (texto mapeado em runtime, sem nova migration):

| Valor enum | Int | Origem |
|---|---|---|
| `DefinicaoDadosFiscaisTreinador` | 11 | `DefinirDadosFiscaisTreinadorHandler` (admin actor: `PerfilId`) |
| `MfaHabilitado` | 12 | `ConfirmarEnrollTotpHandler` (self: `ContaId`) |
| `MfaDesabilitado` | 13 | `DesabilitarMfaHandler` (self: `ContaId`) |
| `RecoveryCodesRegenerados` | 14 | `RegenerarRecoveryCodesHandler` (self: `ContaId`) |
| `SenhaAlterada` | 15 | `AlterarSenhaHandler` (self: `ContaId`) |
| `SenhaRedefinida` | 16 | `RedefinirSenhaHandler` (self: `ContaId`) |
| `EmailAlterado` | 17 | `ConfirmarTrocaEmailHandler` (self: `ContaId`) |
| `ExclusaoGrupoMuscular` | 18 | `ExcluirGrupoMuscularHandler` (admin actor: `PerfilId`) |
| `ExclusaoExercicio` | 19 | `ExcluirExercicioHandler` (owner treinador: `TreinadorId`) |
| `ExclusaoPacote` | 20 | handler de pacote (owner treinador) |
| `InativacaoPlanoPlataforma` | 21 | `ExcluirPlanoPlataformaHandler` (admin actor: `PerfilId`) |
| `AlteracaoStatusAluno` | 22 | `AlterarStatusAlunoHandler` (treinador owner) |
| `AlteracaoModoPagamentoTreinador` | 23 | handler modo pagamento (owner treinador) |
| `ReprocessamentoNotaFiscal` | 24 | `ReprocessarNotaFiscalHandler` (admin actor: `PerfilId`) |

### NFS-e (workers de efeito fiscal — fora do GlobalExceptionHandler)
- `MarcarErro` (rejeição do gov 4xx) → `LogWarning` em `EmitirNfseEfeitoHandler` com `NotaFiscalId`/`TreinadorId`/`Codigo`/`Motivo` (nota visível em `Erro`, reprocessável; pagamento intacto). Timeout/5xx → exceção propaga p/ retry do outbox (sem log de erro terminal).
- `CancelamentoExpirado` (prazo estourado OU rejeição de prazo do gov) → `LogCritical`: estado TERMINAL não-cancelado, exige ajuste fiscal manual do contador. Alerta operacional.
- Reconciliação (`ReconciliarNfseHandler`) loga `de→para` por nota alterada + sumário `(Consultadas/Atualizadas/SemAlteracao/Erros)`. Workflows cron (`gerar-nfse-comissao.yml`/`reconciliar-nfse.yml`) abrem Issue on failure. Canônico em [specification-fiscal].

## 2. HEALTH CHECKS (`/health` liveness + `/health/ready` readiness)
- Registro `Api/Extensions/DependencyInjectionExtensions.cs`: `services.AddHealthChecks().AddDbContextCheck<AppDbContext>("db", tags: ["ready"]).AddCheck<StripeHealthCheck>("stripe", tags: ["ready"]).AddCheck<ResendHealthCheck>("resend", tags: ["ready"]).AddCheck<WhatsAppHealthCheck>("whatsapp", tags: ["ready"])` (pacotes `...HealthChecks.EntityFrameworkCore` + `...HealthChecks` 8.0.11; `AddHttpClient()` garante `IHttpClientFactory` p/ os probes externos). Check `db` = `CanConnectAsync` do `AppDbContext`, só executado quando o endpoint readiness é chamado. `stripe`/`resend`/`whatsapp` = probe externo com timeout 3s, `Degraded` em falha (nunca `Unhealthy`), `Healthy` quando a integração não está configurada. `WhatsAppHealthCheck`: GET `https://graph.facebook.com/{version}/{phoneNumberId}?fields=id` com Bearer token; não-configurado → `Healthy("WhatsApp não configurado.")`; falha/timeout/não-2xx → `Degraded`. Em ambiente `Test` o `AddInfrastructure` é pulado → `AppDbContext` não registrado → o check `db` só roda se o teste o registrar (ver `Tests/Api/Endpoints/HealthEndpointsTests.cs`); `stripe`/`resend`/`whatsapp` sem chave retornam `Healthy`.
- Mapeamento `Api/Extensions/RouteBuilderExtensions.cs` `MapHealthCheck` (ambos `AllowAnonymous().RequireRateLimiting("read")`; ordem no pipeline: depois de auth/authz/rateLimiter — ver [specification-backend] §UseApiConfiguration):
  - LIVENESS `/health`: `HealthCheckOptions { Predicate = _ => false }` → nenhum check; `200` enquanto o processo ASP.NET Core responde. NÃO checa DB/integrações. CONTRATO ESTÁVEL (consumido por compose/frontend) — não alterar.
  - READINESS `/health/ready`: `HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") }` → executa `db` (DbContextCheck), `stripe`, `resend` e `whatsapp` (tag `ready`). DB indisponível = `503 Unhealthy` (corta tráfego). Stripe/Resend/WhatsApp = `Degraded` em falha/timeout (3s), mapeado p/ `200` por padrão (dependência externa instável não tira o pod de rotação); chave ausente = `Healthy` ("não configurado"). `Infrastructure/Health/{Stripe,Resend,WhatsApp}HealthCheck.cs` (OBS-02).
- Consumido por: compose healthcheck `GET :8080/health` (liveness) + frontend `depends_on: backend healthy`. NÃO duplicar — ver [specification-infrastructure] §OBSERVABILITY (`docker-compose.homolog.yml` curl; `docker-compose.yml` via /dev/tcp; `docker-compose.server.yml` curl). Compose/frontend `depends_on` continuam em `/health` (decisão: liveness puro).
- Smoke pós-deploy bate em `/health`: `Tests/E2E/SmokeTests.cs` + `.github/workflows/smoke.yml` (`workflow_run` após CI/CD). README §endpoints documenta `GET /health → 200 Healthy`. Testes unit (sem Docker): `Tests/Api/Endpoints/HealthEndpointsTests.cs` cobre `/health` (200) e `/health/ready` (200 com `AppDbContext` EF InMemory).

## 3. RELATÓRIO DE SAÚDE DIÁRIO (DB ping + KPIs + e-mail)
Pipeline distinto do `/health`: coleta profunda (DB connect real, KPIs, entregabilidade, erros 24h), snapshot persistido, envio por e-mail.

### Collector
- `Infrastructure/Health/HealthReportCollector.cs` (`IHealthReportCollector.ColetarAsync(config, ct)`). Seções OPCIONAIS (flags do `HealthReportConfig`):

| Seção | Flag config | Conteúdo |
|---|---|---|
| Liveness | `IncluirLiveness` | `BancoAcessivel` (`Database.CanConnectAsync`), `EmailHabilitado` (`IEmailService.Habilitado`), `StripeConfigurado` (`Stripe:SecretKey` presente), `WhatsAppConfigurado` (`WhatsApp:PhoneNumberId`+`AccessToken`), `Versao` (`Assembly.GetName().Version`), `Commit` (`AssemblyInformationalVersion` após `+`) |
| KPIs | `IncluirKpis` | Treinadores/Alunos ativos, NovasContas24h, Pagamentos Pendentes/Falhos, AssinaturasAtivas — 6 COUNT sequenciais (DbContext NÃO é thread-safe → sem `Task.WhenAll`; decisão comentada no código) |
| Entregabilidade | `IncluirEntregabilidade` | últimos 24h via `IEmailDeliveryLogRepository`: Total, Entregues (`email.delivered`), Bounces (`email.bounced`), Spam (`email.complained`/`email.spam_complaint`) — ver [specification-email] |
| Erros | `IncluirErros` | `error_logs` últimos 24h: Total + até `MaxAmostrasErro=10` amostras (`OcorridoEm`/`Nivel`/`Origem`/`Mensagem`) — alimentado pelo sink §1 |
| Outbox | `IncluirErros` (mesma flag) | estado do `outbox_efeitos`: contagem por status (`Pendente`/`Processando`/`Concluido`/`Falhou`) via `IOutboxRepository.ContarPorStatusAsync` + até `MaxAmostrasOutbox=10` amostras dos `Falhou` (`Id`/`Tipo`/`Tentativas`/`UltimoErro`/`CriadoEm`) via `ListarPorStatusAsync`. Reusa a flag `IncluirErros` (sinal de falha operacional) — sem coluna/flag dedicada (evita migração de `HealthReportConfig`). Ver [specification-backend] §3.1 (mecanismo outbox). |

- `Ambiente` ← `ASPNETCORE_ENVIRONMENT` (`"Unknown"` fallback). `CapturadoEm` ← `TimeProvider` UTC.
- `StatusGeral` (`DerivarStatus`): `!bancoAcessivel → Falha`; `erros.Total > 0` OU `outbox.Falhou > 0 → Degradado`; senão `Ok` (enum `StatusSaude`).
- Email `EmailTemplates.RelatorioSaude` renderiza `SecaoOutbox` (tabela de contagens + lista de falhas terminais com `UltimoErro` HTML-escapado). Outbox null (flag off) → seção omitida.

### Sender
- `Infrastructure/Health/HealthReportSender.cs` (`IHealthReportSender.EnviarAsync`). Assunto `[forzion.tech] Relatório de saúde — {Ambiente} ({StatusGeral})`; HTML via `EmailTemplates.RelatorioSaude(report)`. Itera destinatários; falha de envio individual → `LogError` (não aborta os demais). Ver [specification-email].

### Scheduler
- `Api/Services/RelatorioSaudeDiarioService.cs` — `BackgroundService`. Registrado `AddHostedService` fora de `Test`. Loop: `Task.Delay(15min)` → `ProcessarAsync`. `OperationCanceledException` → break; outras exceções → `LogError` e continua.
- `DeveEnviar(config, agoraUtc)` (static, testável): `false` se `!Ativo` OU `TimeOnly(agora) < HoraEnvioUtc` OU já enviado hoje (`UltimoEnvioEm.Date == agora.Date`). Garante 1 envio/dia após a hora-alvo.
- `ProcessarAsync`: scope próprio → `config` via `IHealthReportConfigRepository.ObterAsync`; se null/!DeveEnviar → return. Senão: `Coletar` → `HealthSnapshot.Criar(...)` → `IHealthSnapshotRepository.AdicionarAsync` → `Sender.EnviarAsync(destinatarios)` → `config.MarcarEnviado(agora)` → `IUnitOfWork.CommitAsync` → `LogInformation`.
- Testes: `Tests/Api/Services/RelatorioSaudeDiarioServiceTests.cs` (cobre `DeveEnviar`).

### Snapshot (armazenamento)
- `Domain/Entities/HealthSnapshot.cs`: `Ambiente`, `StatusGeral` (`StatusSaude`), `PayloadJson` (relatório serializado via `HealthReportPayload.Serializar`). Factory `Criar` valida `Ambiente`/`Payload` obrigatórios (`HealthErrors`). Persistido a cada envio → histórico consultável.

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
- `frontend/src/components/observability/WebVitals.tsx` — `"use client"`, montado 1× no root layout, sem UI. `useReportWebVitals` (next/web-vitals) → cada métrica (LCP, CLS, INP, FCP, TTFB) vira `Sentry.addBreadcrumb({ category: "web-vitals", level: "info", data: {value,id,label} })`. Breadcrumb anexa ao erro/replay da mesma sessão (contexto de perf). Core vitals de pageload também podem ser coletados pelo `browserTracingIntegration` — integração DEFAULT do `@sentry/nextjs` (ativa via `tracesSampleRate`), NÃO registrada explicitamente em `instrumentation-client.ts` (que só wira `replayIntegration`). Agregação p75/dashboards = gap (Fase 18 / §6).

### Sentry init (gates + no-op)
- `next.config.ts` `withSentryConfig`: plugin de build. Source maps SÓ com `SENTRY_AUTH_TOKEN` (`sourcemaps.disable = !TOKEN`) → `next build` em dev/CI sem token funciona. `silent: !CI`, `disableLogger: true`, `widenClientFileUpload: true`. Org/project/authToken via env.
- Runtimes (3 inits, todos gated `NEXT_PUBLIC_SENTRY_DSN` → `enabled: Boolean(dsn)`; **no-op completo sem DSN** = dev/CI sem config):

| Arquivo | Runtime | Notas |
|---|---|---|
| `instrumentation-client.ts` | browser | `enabled: Boolean(dsn) && analyticsConsented` (LGPD opt-in via `readConsentCookie().analytics===true`, padrão OFF); Session Replay; `onRouterTransitionStart` (tracing de navegação App Router) |
| `sentry.server.config.ts` | node | importado por `instrumentation.ts` (`register`) quando `NEXT_RUNTIME==="nodejs"` |
| `sentry.edge.config.ts` | edge | importado quando `NEXT_RUNTIME==="edge"` |
| `instrumentation.ts` | hook | `register()` carrega config por runtime; `onRequestError = captureRequestError` (erros SSR/RSC/route handlers) |

- Comuns: `environment ← NEXT_PUBLIC_SENTRY_ENV ?? NODE_ENV`; `tracesSampleRate ← env ?? 0.05` (default safe-by-default — sem env var em prod NÃO cai em 10%; subir custa egress/CPU no VPS 2-vCPU); `sendDefaultPii: false` (LGPD: sem IP/cookies/headers). Vars de override documentadas em `.env.example`.
- **Session Replay** (só browser): `replayIntegration({ maskAllText: true, blockAllMedia: true })`; `replaysSessionSampleRate ← env ?? 0.02` (default safe; replay grava DOM → o rate baixo limita egress + exposição de PII/dados de saúde, defesa-em-profundidade c/ mask/block + gate de consentimento); `replaysOnErrorSampleRate: 1.0` (100% em erro — alto valor, baixo volume). Mask/block → não vaza dados de usuário (LGPD). NOTA: `replayIntegration` ainda é import estático (worker ~62KB gz no bundle de toda página mesmo sem consentimento) — lazy-load do replay é hardening PENDENTE (Tier 4 passo 5).
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
- **Alvo = páginas PÚBLICAS homolog**: `/login`, `/cadastro/aluno`, `/cadastro/treinador` (URLs locais em `lighthouserc.json` `collect.url` incluem também `/`). `lhci collect --url=... ; lhci assert`. Report → artifact `lighthouse-report` (`.lighthouseci/`, `if: always()`).
- Perf de runtime/SEO da app: ver [specification-frontend].

## 6. MÉTRICAS / TRACING / ALERTING — ESTADO ATUAL + GAPS
### Implementado
- **Alerting de chargeback**: `Infrastructure/Notifications/Alerts/PagamentoTreinadorEmDisputaAlertHandler.cs` (`IDomainEventHandler<PagamentoTreinadorEmDisputaEvent>`). `LogCritical` com campos estruturados (`PagamentoTreinadorId`, `TreinadorId`, `Valor`). Handler in-memory / best-effort (NOT durable): não persiste, não retenta, não é outbox. Registrado em `InfrastructureExtensions` como `AddScoped` junto com `CancelarNfseHandler` (ambos consomem `PagamentoTreinadorEmDisputaEvent`; APENAS `CancelarNfseHandler` é durable/outbox). Decisão: alerting via log estruturado (Sentry/agregador faz pickup) em vez de GitHub-issue direto. Ver [specification-stripe].
- **Auditoria durável de ações privilegiadas**: `logs_aprovacao` — ver §1 "Auditoria durável" acima.
- Tracing frontend: Sentry browser tracing + replay (§4). É o único tracing existente.

### GAPS (o que falta para observabilidade madura) — itens marcados `0-rep` = 0 ocorrências no repo
- **Sem OpenTelemetry / APM backend** (`0-rep`): nenhum `AddOpenTelemetry`/`ActivitySource`/exporter. Sem distributed tracing backend↔frontend↔Stripe.
- **Sem métricas** (`0-rep`): nenhum Prometheus/`/metrics`/contador/histograma. KPIs só via relatório diário por e-mail (§3), não série temporal.
- **Correlation/request id: RESOLVIDO** (OBS-01): middleware em `RouteBuilderExtensions.cs` (antes de `UseAuthentication`) resolve `X-Request-Id` de entrada ou `TraceIdentifier`, abre `ILogger.BeginScope` e ecoa no header de resposta. Frontend propaga como tag Sentry (`instrumentation-client.ts beforeSend` lê `window.__lastRequestId`, gravado pelo interceptor do `apiClient`). Teste `Tests/Api/Endpoints/CorrelationMiddlewareTests.cs`.
- **Readiness DB+Stripe+Resend+WhatsApp: RESOLVIDO** (§2): `/health/ready` expõe `DbContextCheck` (`db`) + `StripeHealthCheck`/`ResendHealthCheck`/`WhatsAppHealthCheck` (tag `ready`, Degraded em falha, OBS-02); `/health` permanece liveness puro. `WhatsAppHealthCheck` (`Infrastructure/Health/WhatsAppHealthCheck.cs`): Healthy se não configurado, Degraded em timeout/falha/não-2xx (nunca Unhealthy). Gap anterior (verificação APENAS no relatório diário) RESOLVIDO.
- **Sink ERROR fire-and-forget**: RESOLVIDO (OBS-03) — substituído por canal bounded + worker com drain no `ApplicationStopping`. Ver §1 acima.
- **Alerting reativo só por log**: chargeback depende de o agregador externo (Sentry) estar configurado p/ alertar; sem regra de alerta versionada no repo. Sem alertas para 500s, DB down, fila de e-mail.
- **RUM sem agregação**: Web Vitals viram breadcrumbs (contexto), não métrica agregada p75/dashboards (marcado "Fase 18" no código).
- Gates de teste/qualidade de observabilidade: ver [specification-tests].
