# specification-email — subsistema de e-mail (forzion.tech)

DOC PARA AGENTES. Fonte de verdade do processo de e-mail transacional (Resend). Formato denso, agent-oriented. Consultar antes de alterar envio de e-mail, verificação de conta, reset de senha ou webhook de entrega. Estrutura de tabelas de e-mail vive em [specification-db]: `contas`(email_verificado/verificado_em), `email_verification_tokens`, `password_reset_tokens`, `email_delivery_logs` — NÃO duplicar aqui.

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA de qualquer mudança relevante em: provedor, gate Null/real, chaves de config, templates, handlers de e-mail, fluxos, endpoints, webhook, roteamento (nginx), domínio remetente.
- Vive em `specs/` (versionado; commitar). NÃO confundir com `.specs/` (gitignorado).
- Mudança de tabela → atualizar [specification-db], não aqui.

## STACK & GATE
- Provedor: Resend (HTTP API). Verificação de webhook: Svix (NuGet no Infrastructure).
- `IEmailService` (Application/Interfaces): `Task EnviarAsync(para, assunto, htmlBody, ct)` + `bool Habilitado`.
- Impls (Infrastructure/Services):
  - `ResendEmailService` — `Habilitado=true`; POST p/ `Resend:ApiUrl`, Bearer `Resend:ApiKey`, payload `{from,to,subject,html}`. Falha HTTP/exceção = LOG only, SEM rethrow (não quebra fluxo de negócio).
  - `NullEmailService` — `Habilitado=false`; no-op; loga warning no ctor.
- GATE (DI, `InfrastructureExtensions`): `Resend:ApiKey` não-vazio → `ResendEmailService` (+ HttpClient "resend", timeout 15s); senão → `NullEmailService`.
- Remetente FIXO no código: `forzion.tech <noreply@forzion.tech>` (`ResendEmailService`). Exige domínio `forzion.tech` verificado no painel Resend (DKIM `resend._domainkey`, SPF+MX em `send.forzion.tech`, DMARC).
- Handlers de domain event checam `if(!emailService.Habilitado) return;` antes de enviar.

## CONFIG (chaves)
| Chave | Onde lida | Função | Ausente |
|-------|-----------|--------|---------|
| `Resend:ApiKey` | `InfrastructureExtensions` (gate) | seleciona Resend real | → NullEmailService |
| `Resend:ApiUrl` | idem | override endpoint | default `https://api.resend.com/emails` |
| `Resend:WebhookSecret` | `WebhookEndpoints`/`ProcessarWebhookResendHandler` | verificação Svix | webhook → 400 "Webhook não configurado" |
| `App:FrontendBaseUrl` | `AppSettings` (bound DI) | base dos links verify/reset | default vazio (links quebram) |

- Local (dev): User Secrets `forzion-prod` (`dotnet user-secrets set "Resend:ApiKey" ...`). `App:FrontendBaseUrl` em appsettings = `http://localhost:3000`.
- Deploy: env vars no compose → `Resend__ApiKey`/`Resend__WebhookSecret`/`App__FrontendBaseUrl` ← `${RESEND_API_KEY}`/`${RESEND_WEBHOOK_SECRET}`/`${APP_FRONTEND_BASE_URL:-https://homologacao.forzion.tech}` (vêm do `/opt/forzion/.env` na VM). Default vazio = Null roda mesmo em homolog se .env não setado.
- Chaves separadas por ambiente: WebhookSecret é por-endpoint (homolog≠prod obrigatório); ApiKey recomendável uma por ambiente. Resend NÃO tem test/live mode — chave de hmg envia e-mail real.

## COMPONENTES
- `EmailTemplates` (Infrastructure) — HTML estático via `Layout(...)`. Métodos: `TreinadorAprovado`, `TreinadorReprovado`, `TreinadorInativado`, `VinculoAprovado`, `BemVindoAluno`, `AlunoInativado`, `RedefinirSenha(email,resetLink)`, `VerificarEmail(email,verifyLink)`, `AssinaturaAlunoCriada`.
- `EmailVerificationSender` (Infrastructure, `virtual EnviarAsync`) — centraliza geração+envio do e-mail de verificação. Gera token 32 bytes → hex(64) (cru no link), armazena SHA-256 hex(64) em `email_verification_tokens` (expiry +24h), commita, envia link `{FrontendBaseUrl}/verify-email?token={raw}`. Usado por `ContaRegistradaEmailHandler` e `ReenviarVerificacaoHandler`.
- Handlers de domain event (Infrastructure, registrados manualmente no DI) — despachados no `UnitOfWork.CommitAsync`.

## FLUXOS

### Verificação de e-mail (Módulo C)
- Cadastro: `Conta.Criar` emite `ContaRegistradaEvent` (cobre Aluno+Treinador sem duplicar). Conta nasce `email_verificado=false`.
- `ContaRegistradaEmailHandler` → `EmailVerificationSender.EnviarAsync` → e-mail com link.
- Verificar: `POST /auth/verify-email {token}` → `VerificarEmailHandler` (Application): valida formato (64 chars), hash SHA-256, busca token, checa `verified_at` nulo + `expires_at` futuro, marca `Conta.MarcarEmailVerificado` (idempotente) + `token.MarcarComoVerificado`, commit.
- Reenvio: `POST /auth/resend-verification {email}` → `ReenviarVerificacaoHandler` (Infrastructure): normaliza email, busca conta; SILENCIOSO (conta inexistente OU já verificada → no-op, não vaza); senão `EmailVerificationSender`. Endpoint sempre 200.
- Login: `EmailNaoVerificadoException` → 403 `ProblemDetails.Extensions["code"]="EMAIL_NAO_VERIFICADO"`, checado APÓS validação de senha (não vaza existência/verificação).
- Seed: admin recebe `MarcarEmailVerificado()` + `ClearDomainEvents()` (não gera token/e-mail).

### Reset de senha (Módulo B)
- `POST /auth/forgot-password {email}` → `EsqueceuSenhaHandler` (Infrastructure): gera token (mesmo padrão hex(64)+SHA-256), `password_reset_tokens` (expiry +1h), envia `RedefinirSenha` link. SEMPRE 200 (não vaza existência).
- `POST /auth/reset-password {token,novaSenha}` → `RedefinirSenhaHandler` (Application, sem deps Infra): valida token/expiry/used, atualiza senha, marca `used_at`.

### E-mails de notificação (domain events → handler → template)
`AlunoRegistradoEvent`→BemVindoAluno · `AlunoInativadoEvent`→AlunoInativado · `TreinadorAprovadoEvent`→TreinadorAprovado · `TreinadorReprovadoEvent`→TreinadorReprovado · `TreinadorInativadoEvent`→TreinadorInativado · `VinculoAprovadoEvent`→VinculoAprovado · `AssinaturaAlunoCriadaEvent`→AssinaturaAlunoCriada.

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

⚠️ `DomainException` → **422** (UnprocessableEntity, `GlobalExceptionHandler`), NÃO 400. Só `ValidationException` (FluentValidation, formato) → 400.

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
- Unit (xUnit, sem Docker): Domain (`EmailVerificationTokenTests`, `ContaTests`: evento+MarcarEmailVerificado), Application (`VerificarEmailHandlerTests`, `LoginHandlerTests` 403), Infra (`ContaRegistradaEmailHandlerTests`, `ReenviarVerificacaoHandlerTests` silencioso, `EmailVerificationSenderTests`), Api (`AuthEndpointsTests` verify 200/422/400 + resend 200).
- E2E/Infra-repo (Testcontainers) exigem Docker → rodam só no CI.

## DICAS / GOTCHAS
- "Domínio verified mas pending" no Resend = lag; reclicar Verify. Envio dá 403 enquanto não Active.
- Sem warning "Serviço de e-mail não configurado" no startup = Resend ativo (Null loga no ctor).
- Verificar envio sem inbox: log do `ResendEmailService` (200 = ok; erro logado em falha). Verificar webhook ponta-a-ponta: linha em `email_delivery_logs` (schema do ambiente).
- Resend não alcança `localhost` → webhook não testável local sem túnel (ngrok). Outbound não depende do webhook.
- Migration de e-mail (`AdicionarVerificacaoEmail`) faz backfill `email_verificado=true` p/ contas existentes; novos = false.
