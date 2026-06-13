# specification-email — subsistema de e-mail (forzion.tech)

DOC PARA AGENTES. Fonte de verdade do processo de e-mail transacional (Resend). Formato denso, agent-oriented. Consultar antes de alterar envio de e-mail, verificação de conta, reset de senha ou webhook de entrega. Estrutura de tabelas de e-mail vive em [specification-db]: `contas`(email_verificado/verificado_em), `email_verification_tokens`, `password_reset_tokens`, `email_delivery_logs` — NÃO duplicar aqui.

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA de qualquer mudança relevante em: provedor, gate Null/real, chaves de config, templates, handlers de e-mail, fluxos, endpoints, webhook, roteamento (nginx), domínio remetente.
- Mudança de tabela → atualizar [specification-db], não aqui.

## STACK & GATE
- Provedor Resend + webhook Svix (ver AGENTS.md STACK): detalhe = Resend via HTTP API; Svix NuGet no Infrastructure.
- `IEmailService` (Application/Interfaces): `Task EnviarAsync(para, assunto, htmlBody, ct, string? replyTo = null)` + `bool Habilitado`. `replyTo` é o 5º arg (DEPOIS do `ct`) p/ não quebrar callers que passam ct posicional.
- Impls (Infrastructure/Services):
  - `ResendEmailService` — `Habilitado=true`; POST p/ `Resend:ApiUrl`, Bearer `Resend:ApiKey`, payload `{from,to,subject,html}` (+ `reply_to` snake_case SÓ quando `replyTo` não-nulo — payload é `Dictionary` p/ campo condicional). `from` montado de `EmailSettings.FromName/FromAddress` (fallback `forzion.tech <noreply@forzion.tech>` se vazio). Falha HTTP/exceção = LOG only, SEM rethrow (não quebra fluxo de negócio).
  - `NullEmailService` — `Habilitado=false`; no-op; loga warning no ctor.
  - `EnvironmentEmailDecorator` (Notifications/Email) — decora o `IEmailService` real; `Habilitado` delega ao inner. Ver SEPARAÇÃO POR AMBIENTE.
- GATE (DI, `InfrastructureExtensions`): `Resend:ApiKey` não-vazio → `ResendEmailService` (+ HttpClient "resend", timeout 15s); senão → `NullEmailService`. O `IEmailService` resolvido é SEMPRE o inner embrulhado no `EnvironmentEmailDecorator`.
- Remetente: `EmailSettings.FromName/FromAddress` (config `Email`), não mais hardcoded. Default/prod = `forzion.tech <noreply@forzion.tech>`. Exige domínio `forzion.tech` verificado no painel Resend (DKIM `resend._domainkey`, SPF+MX em `send.forzion.tech`, DMARC).
- Handlers de domain event checam `if(!emailService.Habilitado) return;` antes de enviar.

## CONFIG (chaves)
| Chave | Onde lida | Função | Ausente |
|-------|-----------|--------|---------|
| `Resend:ApiKey` | `InfrastructureExtensions` (gate) | seleciona Resend real | → NullEmailService |
| `Resend:ApiUrl` | idem | override endpoint | default `https://api.resend.com/emails` |
| `Resend:WebhookSecret` | `WebhookEndpoints`/`ProcessarWebhookResendHandler` | verificação Svix | webhook → 400 "Webhook não configurado" |
| `App:FrontendBaseUrl` | `AppSettings` (bound DI) | base dos links verify/reset | default vazio (links quebram) |
| `Email:FromName` / `Email:FromAddress` | `EmailSettings` (bound DI) → `ResendEmailService` | remetente | default `forzion.tech` / `noreply@forzion.tech` |
| `Email:SupportAddress` | `EmailSettings` → `MensagemSuporteCriadaEmailHandler` | destino das mensagens de suporte | default `suporte@forzion.tech` |
| `Email:MarcarComoTeste` | `EmailSettings` → `EnvironmentEmailDecorator` | liga marcação+redirect (não-prod) | default `false` (prod passthrough) |
| `Email:PrefixoAssuntoTeste` | idem | prefixo do assunto em não-prod | default vazio (sem prefixo) |
| `Email:RedirecionarDestinatariosPara` | idem | CSV; redireciona destinatário (1º alvo) | vazio → sem redirect (mantém destinatário) |
| `Email:AllowlistDominios` | idem | CSV de domínios isentos de redirect | vazio → ninguém isento |

- Local (dev): User Secrets `forzion-prod` (`dotnet user-secrets set "Resend:ApiKey" ...`). `App:FrontendBaseUrl` em appsettings = `http://localhost:3000`.
- Deploy: env vars no compose → `Resend__ApiKey`/`Resend__WebhookSecret`/`App__FrontendBaseUrl` ← `${RESEND_API_KEY}`/`${RESEND_WEBHOOK_SECRET}`/`${APP_FRONTEND_BASE_URL:-https://homologacao.forzion.tech}` (vêm do `/opt/forzion/.env` na VM). Default vazio = Null roda mesmo em homolog se .env não setado.
- Guardrail (homolog/dev): `Email__RedirecionarDestinatariosPara` ← `${EMAIL_REDIRECT_TO:-}` e `Email__AllowlistDominios` ← `${EMAIL_ALLOWLIST_DOMAINS:-}` (compose homolog+local; setar `EMAIL_REDIRECT_TO` no `/opt/forzion/.env` da VM p/ ativar). `MarcarComoTeste`/prefixo já vêm de `appsettings.Homolog.json` (não precisam de env).
- Chaves separadas por ambiente: WebhookSecret é por-endpoint (homolog≠prod obrigatório); ApiKey recomendável uma por ambiente. Resend NÃO tem test/live mode — chave de hmg envia e-mail real.

## SEPARAÇÃO POR AMBIENTE
Resend não tem sandbox → e-mail de não-prod é real. `EnvironmentEmailDecorator` (Infrastructure/Notifications/Email) decora o `IEmailService` real e, em não-prod, deixa claro que é teste + impede atingir usuários reais.
- Discriminador: `EmailSettings.MarcarComoTeste` (flag, não `IWebHostEnvironment`). `false` (default/prod) → **passthrough puro** (assunto/destinatário/html intactos). `true` (Homolog/Development via `appsettings.{Env}.json`) → aplica transformações abaixo.
- Transformações (não-prod): (1) **assunto** ← `"{PrefixoAssuntoTeste} {assunto}"` (prefixo vazio = sem alteração); (2) **banner** HTML de aviso prepended ao `htmlBody`; (3) **destinatário** → `ResolverDestinatario`: se `RedirecionarDestinatariosPara` vazio OU domínio do destinatário ∈ `AllowlistDominios` → mantém; senão redireciona p/ 1º alvo do CSV e loga `Information` com destinatário original.
- DI: `InfrastructureExtensions.EnvolverComDecorator` embrulha Resend (configurado) ou Null (não configurado). Gate Resend/Null preservado por baixo.
- Defaults por ambiente: base `appsettings.json` = prod-safe (remetente real, `MarcarComoTeste=false`, prefixo/redirect/allowlist vazios); `appsettings.Production.json` herda (sem override); `Homolog`/`Development` = `MarcarComoTeste=true` + prefixo `[HOMOLOG - TESTE]`/`[DEV - TESTE]`. Alvo de redirect/allowlist vazios em appsettings → setar via env no deploy p/ ativar o guardrail (sem alvo, não-prod marca mas ainda envia ao destinatário real).

## COMPONENTES
- `EmailTemplates` (Infrastructure) — HTML estático via `Layout(...)`. Métodos: `TreinadorAprovado`, `TreinadorReprovado`, `TreinadorInativado`, `VinculoAprovado(nomeAluno, nomeTreinador)`, `BemVindoAluno`, `AlunoInativado`, `RedefinirSenha(email,resetLink)`, `VerificarEmail(email,verifyLink)`, `AssinaturaAlunoCriada`, `CobrancaDisponivel(nomeAluno,valor,metodo,linkPortal)`, `CobrancaFalhou(nomeAluno,valor,tentativasFalhas,linkPortal)`, `AssinaturaInadimplente(nomeAluno,tentativasFalhas,linkPortal)`, `AssinaturaReativada(nomeAluno,linkPortal)`, `AssinaturaCancelada(nomeAluno,dataCancelamento,nomeTreinador)`, `AlunoCancelouAssinatura(nomeTreinador,nomeAluno,valor)`, `NovoAlunoPendente(nomeTreinador,nomeAluno)`, `PagamentoEstornado(nomeAluno,valor,linkPortal)`, `PagamentoEmDisputa(nomeTreinador,nomeAluno,valor,motivo)`, `CobrancaPlanoFalhou(nomeTreinador,valor,tentativasFalhas,linkPortal)` (billing treinador — falha por tentativa, tom progressivo), `PlanoInadimplente(nomeTreinador,tentativasFalhas,linkPortal)` (billing treinador — inadimplência no plano), `CobrancaProxima(nome,valor,dataCobranca,linkPortal)` (pré-aviso de renovação CDC art. 52 — "renova em 3 dias", ver §Pré-aviso), `RelatorioSaude(report)`. Valores monetários formatados pt-BR explícito (CultureInfo "pt-BR"). Templates de pagamento detalhados em [specification-stripe].
- `EmailVerificationSender` (Infrastructure, `virtual EnviarAsync`) — centraliza geração+envio do e-mail de verificação. Gera token 32 bytes → hex(64) (cru no link), armazena SHA-256 hex(64) em `email_verification_tokens` (expiry +24h), commita, envia link `{FrontendBaseUrl}/verify-email?token={raw}`. Usado por `ContaRegistradaEmailHandler` e `ReenviarVerificacaoHandler`.
- Handlers de domain event (Infrastructure, registrados manualmente no DI) — despachados no `UnitOfWork.CommitAsync`.

## FLUXOS

### Verificação de e-mail (Módulo C)
- Cadastro: `Conta.Criar` emite `ContaRegistradaEvent` (cobre Aluno+Treinador sem duplicar). Conta nasce `email_verificado=false`.
- **Treinador plano PAGO = verificação ADIADA**: `RegistrarTreinadorHandler` chama `Conta.Criar(..., emitirRegistro: !pago)` (`pago = plano.Preco>0`) → no pago `ContaRegistradaEvent` NÃO é emitido no cadastro, logo o e-mail de verificação NÃO sai ainda. Sai só após pagamento confirmado: webhook `payment_intent.succeeded` tipo=`plano_treinador`/finalidade `Cadastro` → `ProcessarWebhookStripeHandler.FinalizarCadastroAsync` (ativa assinatura + `treinador.ConfirmarPagamentoPlano` + `conta.EmitirRegistro(agora)`) NO MESMO commit → emite `ContaRegistradaEvent` → handler dispara o e-mail. Plano FREE (`Preco=0`) emite no `Criar` (fluxo padrão). Aluno: sempre no `Criar`. Ver [specification-stripe] (finalização atômica do cadastro pago).
- `ContaRegistradaEmailHandler` → `EmailVerificationSender.EnviarAsync` → e-mail com link.
- Verificar: `POST /auth/verify-email {token}` → `VerificarEmailHandler` (Application): valida formato (64 chars), hash SHA-256, busca token, checa `verified_at` nulo + `expires_at` futuro, marca `Conta.MarcarEmailVerificado` (idempotente) + `token.MarcarComoVerificado`, commit.
- Reenvio: `POST /auth/resend-verification {email}` → `ReenviarVerificacaoHandler` (Infrastructure): normaliza email, busca conta; SILENCIOSO (conta inexistente OU já verificada → no-op, não vaza); senão `EmailVerificationSender`. Endpoint sempre 200.
- Login: `EmailNaoVerificadoException` → 403 `ProblemDetails.Extensions["code"]="EMAIL_NAO_VERIFICADO"`, checado APÓS validação de senha (não vaza existência/verificação).
- Seed: admin recebe `MarcarEmailVerificado()` + `ClearDomainEvents()` (não gera token/e-mail).

### Reset de senha (Módulo B)
- `POST /auth/forgot-password {email}` → `EsqueceuSenhaHandler` (Infrastructure): gera token (mesmo padrão hex(64)+SHA-256), `password_reset_tokens` (expiry +1h), envia `RedefinirSenha` link. SEMPRE 200 (não vaza existência).
- `POST /auth/reset-password {token,novaSenha}` → `RedefinirSenhaHandler` (Application, sem deps Infra): valida token/expiry/used, atualiza senha, marca `used_at`.

### E-mails de notificação (domain events → handler → template)
`AlunoRegistradoEvent`→BemVindoAluno · `AlunoInativadoEvent`→AlunoInativado · `TreinadorAprovadoEvent`→TreinadorAprovado · `TreinadorReprovadoEvent`→TreinadorReprovado · `TreinadorInativadoEvent`→TreinadorInativado · `VinculoPendenteCriadoEvent`→NovoAlunoPendente (treinador) · `VinculoAprovadoEvent`→VinculoAprovado · `AssinaturaAlunoCriadaEvent`→AssinaturaAlunoCriada · `PagamentoCriadoEvent`→CobrancaDisponivel (aluno) · `PagamentoFalhouEvent`→CobrancaFalhou (aluno) · `AssinaturaAlunoMarcadaInadimplenteEvent`→AssinaturaInadimplente (aluno) · `AssinaturaAlunoReativadaEvent`→AssinaturaReativada (aluno) · `AssinaturaAlunoCanceladaEvent`→AssinaturaCancelada (aluno) + AlunoCancelouAssinatura (treinador) · `PagamentoEstornadoEvent`→PagamentoEstornado (aluno) · `PagamentoEmDisputaEvent`→PagamentoEmDisputa (treinador, URGENTE) · `AssinaturaTreinadorPagamentoFalhouEvent`→CobrancaPlanoFalhou (treinador, tom progressivo) · `AssinaturaTreinadorMarcadaInadimplenteEvent`→PlanoInadimplente (treinador). Eventos de pagamento aluno têm também notifiers WhatsApp paralelos (catálogo/handlers em [specification-whatsapp]) + alert log de disputa (em [specification-stripe]). Billing treinador: SÓ e-mail (sem WhatsApp).
- **Contato com suporte** (`MensagemSuporteCriadaEvent`→`MensagemSuporteCriadaEmailHandler`→template `MensagemSuporte`): único handler de e-mail **DURÁVEL** (via outbox — enfileirado no mesmo `CommitAsync` do ticket, re-despachado com retry pelo worker; ver durabilidade em [specification-backend]/[specification-concurrency]). Envia p/ `EmailSettings.SupportAddress` com `replyTo = e-mail da conta` (responder cai direto no usuário). Resolve nome/e-mail/tipo LIVE por `ContaId` (sem `IUserContext` — roda no worker); conta sumida entre commit e dispatch → THROW (propaga p/ retry, não engole). UNGATED (não passa por `IPlanoNotificationPolicy`). Respeita `emailService.Habilitado`. Corpo HtmlEncode'd (anti-injeção no HTML do e-mail).
- **Gating por tier** (`IPlanoNotificationPolicy`): handlers **OPERACIONAIS** (pagamento criado/falhou/estornado/disputa, assinatura criada/cancelada×2, vínculo aprovado/pendente, inadimplência, aluno-inativado) checam `canais.Email` antes de enviar — e-mail só enviado se treinador tem tier≥Pro. **UNGATED** (sempre enviam, sem consulta a plano): verificação de e-mail, reset de senha, reenvio de verificação (`ContaRegistradaEmailHandler`, `EsqueceuSenhaHandler`, `ReenviarVerificacaoHandler`), ciclo de conta do treinador (`TreinadorAprovado`/`Reprovado`/`Inativado`), bem-vindo aluno (`AlunoRegistradoEmailHandler`). Cross-ref `TierPlanoExtensions` [specification-model], `IPlanoNotificationPolicy` [specification-backend].
- **Gating por MODO DE PAGAMENTO (Externo = sem e-mail de PAGAMENTO)**: ortogonal ao gating por tier. Quando `treinador.ModoPagamentoAluno == Externo`, NENHUMA `AssinaturaAluno`/`Pagamento` do aluno é criada — o gate vive NO PONTO DE CRIAÇÃO (`VinculoAprovadoCriarAssinaturaAlunoHandler` retorna cedo se Externo; gate primário em `AprovarVinculoHandler`), NÃO espalhado pelos ~10 handlers de notificação. Logo os eventos de pagamento (`PagamentoCriadoEvent`/`PagamentoFalhouEvent`/`PagamentoEstornadoEvent`/`PagamentoEmDisputaEvent`/`AssinaturaAlunoCriadaEvent`/`AssinaturaAlunoMarcadaInadimplenteEvent`/`AssinaturaAlunoReativadaEvent`/`AssinaturaAlunoCanceladaEvent`) NUNCA são emitidos → as notificações de pagamento/assinatura (e os notifiers WhatsApp paralelos) estruturalmente não disparam no Externo. NÃO afeta verificação/reset/bem-vindo/vínculo aprovado-pendente/ciclo de conta (independem de billing). Defense-in-depth concentrada na criação. Provado por `VinculoApprovalCrossAggregateTests.AprovarVinculo_ModoExterno_AceitaSemOnboarding_NaoGeraBillingNemNotificacao`. Ver `ModoPagamentoAluno` [specification-model], paralelo em [specification-whatsapp].
- **Pré-aviso de renovação (R5, CDC art. 52)**: e-mail **3 dias** antes da `DataProximaCobranca`, aluno e treinador. NÃO é disparado por mudança de estado de domínio — é **job-driven**: workflow `billing-prenotification.yml` (diário) chama `POST /internal/processar-pre-avisos` e `.../-treinador` (`INTERNAL_API_KEY`) → `DespacharPreAvisos{Aluno,Treinador}Handler` (base genérica `DespacharPreAvisosHandlerBase<TAssinatura>`, CR#7) → `ListarParaPreAvisoAsync` (janela `[hoje+3d, hoje+4d)`, **SÓ status `Ativa`** — CR#3: Inadimplente em dunning não recebe "renova em 3 dias") → despacha `CobrancaProxima{Aluno,Treinador}Event` → handler de e-mail → template `CobrancaProxima`. **Gating**: aluno passa por `IPlanoNotificationPolicy` (canal por tier); treinador NÃO (sem gate de canal — CR#7). **Idempotência**: a janela diária limita a 1× por `DataProximaCobranca` (sem coluna/flag — CR#4); risco residual só em `workflow_dispatch` manual no mesmo dia.
- **Destinatário (handlers de aluno)**: `aluno.Email ?? conta.Email` (fallback). `Aluno.Email` é OPCIONAL e nasce `null` no cadastro (`RegistrarAlunoHandler` passa email=null), então o fallback p/ `Conta.Email` (login, obrigatório) é o caminho normal — sem ele alunos não recebiam nenhuma notificação. Handlers (`VinculoAprovado`/`AlunoInativado`/`AssinaturaAlunoCriada`) injetam `IContaRepository` e buscam por `aluno.ContaId`; `AlunoRegistradoEmailHandler` usa `AlunoRegistradoEvent.ContaId` (evento carrega ContaId). Ambos null → log warning + no-op.

### Webhook de entrega (Módulo D)
- `POST /webhooks/resend` → `ProcessarWebhookResendHandler` (Infrastructure, usa `Result`).
- Svix `Webhook.Verify` exige `WebHeaderCollection` (não dict) + headers `svix-id`/`svix-timestamp`/`svix-signature`. `Resend:WebhookSecret` ausente → 400 "Webhook não configurado"; assinatura inválida → 400 "Assinatura do webhook inválida".
- Eventos persistidos (`EventosRelevantes`): `email.delivered`, `email.bounced`, `email.complained`, `email.spam_complaint`. Outros → ignorados (200). `recipient_email` de `data.to[0]` (JsonDocument). Payload raw armazenado integral. Cada evento → linha em `email_delivery_logs` (idempotência por `resend_message_id` não forçada por UQ; idx).

## ENDPOINTS
| Método/Rota | Handler | Sucesso | Erros |
|-------------|---------|---------|-------|
| POST /auth/verify-email | VerificarEmailHandler | 200 | 422 (DomainException: token inválido/usado/expirado) · 400 (ValidationException: token≠64) · 429 |
| POST /auth/resend-verification | ReenviarVerificacaoHandler | 200 (sempre) | 429 |
| POST /auth/forgot-password | EsqueceuSenhaHandler | 200 (sempre) | 429 |
| POST /auth/reset-password | RedefinirSenhaHandler | 200 | 422 · 400 · 429 |
| POST /auth/login | LoginHandler | 200 | 403 EMAIL_NAO_VERIFICADO (pós-senha) · 401 · 400 |
| POST /webhooks/resend | ProcessarWebhookResendHandler | 200 (ok/ignorado) | 400 (sem secret ou assinatura inválida) |

⚠️ Token inválido/usado/expirado = `DomainException`→**422** (NÃO 400); só `ValidationException` (formato) → 400. Mapa exceção→status completo: [specification-backend] §4.

## INFRA / ROTEAMENTO
- nginx (`nginx/nginx.conf`): `location /webhooks/ { proxy_pass http://backend:8080; }` ANTES do `location /` (que vai p/ `frontend:3000`). Necessário porque o webhook precisa dos headers `svix-*` CRUS no backend.
- ⚠️ NÃO usar o proxy da SPA `/api/backend/[...path]` p/ webhook: ele só repassa `content-type`/`accept` (descarta `svix-*`) e injeta Bearer. Webhook Resend deve apontar p/ `https://<host>/webhooks/resend` (rota nginx direta).
- compose homolog (`docker-compose.homolog.yml`) e local (`docker-compose.yml`/`.env.example`) mapeiam as env vars Resend. Deploy aplica nginx via `restart nginx` (bind-mount).
- Painel Resend: webhook endpoint = `https://homologacao.forzion.tech/webhooks/resend`, eventos delivered/bounced/complained/spam_complaint; signing secret = `RESEND_WEBHOOK_SECRET`.

## FRONTEND
- Páginas (`app/(public)/`): `verify-email` (auto-POST do token, estados verifying/success/error; erro mostra botão "Reenviar verificação" → `/resend-verification`), `forgot-password`, `reset-password`, `resend-verification`. Pages que leem `useSearchParams` (verify-email, reset-password) DEVEM envolver em `<Suspense>` (senão `next build` falha por CSR bailout).
- Route handlers (`app/api/auth/*`): `verify-email`, `resend-verification`, `forgot-password`, `reset-password` — proxy específico → backend `/auth/*`, repassam status+body de erro (ProblemDetails). Mensagem de expiração exibida vem de `problem.detail`.

## SEGURANÇA
- Tokens: nunca armazenados crus. SHA-256 hex(64) no DB; cru só no e-mail/link. Verify 24h, reset 1h.
- Sem vazamento: forgot-password e resend-verification sempre 200 e silenciosos; 403 de e-mail não verificado só após validar senha.
- Webhook: assinatura Svix obrigatória; payload raw auditável; endpoint seguro p/ exposição pública (valida assinatura).
- Resend API key: backend-only (env/secret), nunca no frontend.

## TESTES
- Unit (xUnit, sem Docker): Domain (`EmailVerificationTokenTests`, `ContaTests`: evento+MarcarEmailVerificado), Application (`VerificarEmailHandlerTests`, `LoginHandlerTests` 403, `EmailSettingsTests` binding), Infra (`ContaRegistradaEmailHandlerTests`, `ReenviarVerificacaoHandlerTests` silencioso, `EmailVerificationSenderTests`, `ResendEmailServiceTests` from, `EnvironmentEmailDecoratorTests` prefixo/banner/redirect/passthrough), Api (`AuthEndpointsTests` verify 200/422/400 + resend 200).
- E2E/Infra-repo (Testcontainers) exigem Docker → rodam só no CI.

## DICAS / GOTCHAS
- "Domínio verified mas pending" no Resend = lag; reclicar Verify. Envio dá 403 enquanto não Active.
- Gate Null/real: sem warning no startup = real ativo (Null loga no ctor). Simétrico ao WhatsApp [specification-whatsapp].
- Verificar envio sem inbox: log do `ResendEmailService` (200 = ok; erro logado em falha). Verificar webhook ponta-a-ponta: linha em `email_delivery_logs` (schema do ambiente).
- Resend não alcança `localhost` → webhook não testável local sem túnel (ngrok). Outbound não depende do webhook.
- Migration de e-mail (`AdicionarVerificacaoEmail`) faz backfill `email_verificado=true` p/ contas existentes; novos = false.
