# specification-stripe — subsistema de pagamento (forzion.tech)

DOC PARA AGENTES. Fonte de verdade do processo de pagamento (Stripe Connect Express + PaymentIntent Pix/Cartão + webhook + billing mensal). Formato denso, agent-oriented. Consultar antes de alterar onboarding de treinador, cobrança mensal, processamento de webhook, cálculo de taxa de plataforma, fluxo de pagamento Pix/Cartão ou roteamento (nginx). Estrutura de tabelas vive em [specification-db]: `pagamentos`, `assinaturas_aluno`, `conta_recebimento`, `pacotes` — NÃO duplicar aqui.

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA de qualquer mudança relevante em: SDK version, chaves de config, métodos do `IStripeService`, handlers, fluxos (onboarding/cobrança/webhook), endpoints, roteamento (nginx), cron de renovação, defesas de segurança (cross-account, idempotência, rate limit).
- Vive em `specs/` (versionado; commitar). NÃO confundir com `.specs/` (gitignorado).
- Mudança de tabela → atualizar [specification-db], não aqui.

## STACK & GATE
- SDK: Stripe.net `51.1.0` (NuGet no Infrastructure). Pinada em `forzion.tech.Infrastructure.csproj`.
- `IStripeService` (Application/Interfaces): 6 métodos:
  - `CriarContaConnectAsync(email, nome, ct)` → `accountId`
  - `GerarLinkOnboardingAsync(accountId, urlRetorno, urlCancelamento, ct)` → URL
  - `CriarPixPaymentIntentAsync(valor, accountId, pagamentoId, taxaPlataformaPercent, ct)` → `PixPaymentResult(intentId, qrCode, qrCodeUrl, expiracao)`
  - `CriarCartaoPaymentIntentAsync(valor, accountId, pagamentoId, taxaPlataformaPercent, ct)` → `CartaoPaymentResult(intentId, clientSecret)`
  - `ContaEstaAtivadaAsync(accountId, ct)` → `bool` (poll `account.ChargesEnabled`)
  - `ValidarWebhookAsync(payload, assinaturaHeader)` → `bool` (`EventUtility.ConstructEvent` HMAC-SHA256)
- Impl única: `StripeService` (Infrastructure/Services). Sem `NullStripeService` — `StripeSettings.ValidateOnStart` exige `SecretKey`/`WebhookSecret` não-vazios (boot falha sem config).
- `RequestOptions { ApiKey }` passada em cada chamada (sem estado global).
- Métodos `Create*PaymentIntent`: usam `MoneyCentavos.ValorETaxaCentavos(valor, taxaPercent)` (Application/UseCases/Pagamentos) — extração de F16 (truncamento via `(long)`; sum preservation ≤1 centavo). NÃO usar Math.Round / banker's rounding.
- Connect flow: `ApplicationFeeAmount=taxaCentavos` + `TransferData.Destination=stripeAccountId` (taxa fica na plataforma, restante vai pro treinador).
- Metadata: `["pagamento_id"]=pagamento.Id.ToString()` — útil pra reconciliação.
- ⚠ Sem `Stripe-Idempotency-Key` header nas Create requests. App layer mitiga via `GerarCobrancaMensalHandler` (serializable tx + idempotent re-uso de pendente, F12). Considerar adicionar IdempotencyKey se Stripe retry policy ficar mais agressiva.

## CONFIG (chaves)
| Chave | Onde lida | Função | Ausente |
|-------|-----------|--------|---------|
| `Stripe:SecretKey` | `StripeSettings` (DI bind + ValidateOnStart) | API secret | boot falha |
| `Stripe:PublishableKey` | passada pro frontend via `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | client-side | frontend desliga `<Elements>` (stripePromise null) |
| `Stripe:WebhookSecret` | `StripeService.ValidarWebhookAsync` | HMAC-SHA256 verify | boot falha |
| `Stripe:TaxaPlataformaPercent` | `StripeSettings` + `PaymentSettings` (espelho App) | taxa plataforma (0,100] | default `5m`; ValidateOnStart falha se fora do range |
| `Stripe:UrlBase` | frontend base usada em URLs de retorno onboarding | retorno Stripe → app | vazio → links quebram |
| `Internal:ApiKey` | endpoint `/internal/processar-renovacoes` (FixedTimeEquals) | autoriza GH Actions cron | endpoint → 401 |

- Local (dev): User Secrets ou `.env` na raiz (compose pega `STRIPE_SECRET_KEY`, etc).
- Deploy: env vars no compose → `Stripe__SecretKey`/`Stripe__WebhookSecret`/`Stripe__UrlBase` ← `${STRIPE_*}` (vêm do `/opt/forzion/.env` na VM). `Internal__ApiKey` ← `${INTERNAL_API_KEY}`.
- ValidateOnStart enforça: SecretKey/WebhookSecret não-vazios, TaxaPlataformaPercent ∈ (0,100].

## COMPONENTES
- `StripeService` (Infrastructure/Services) — implementa `IStripeService`. Wrapper fino sobre Stripe.net SDK; sem retry custom.
- `MoneyCentavos` (Application/UseCases/Pagamentos) — `ToCentavos(valor)`, `CalcularTaxaCentavos(valorCentavos, taxaPercent)`, `ValorETaxaCentavos(valor, taxaPercent)`. Truncamento deliberado (`(long)`), 8 invariantes em property tests (F16).
- `ProcessarWebhookStripeHandler` (Application/UseCases/Pagamentos/ProcessarWebhookStripe) — eventos: `payment_intent.succeeded` → MarcarPago + AssinaturaAluno.Ativar() + AgendarProximaCobranca(+1mês); `payment_intent.payment_failed` → MarcarFalhou; `payment_intent.canceled` → MarcarExpirado (Stripe envia auto após `ExpiresAfterSeconds=3600` no Pix); `account.updated` (chargesEnabled=true) → ContaRecebimento.ConfirmarOnboarding. Default → log + ignore.
- `StripeWebhookParser` (Application/UseCases/Pagamentos/ProcessarWebhookStripe) — parse JsonDocument: type, paymentIntentId, accountId, chargesEnabled.
- `IniciarOnboardingTreinadorHandler` (Application/UseCases/Treinadores/IniciarOnboarding) — idempotente: cria ContaRecebimento se ausente, cria Stripe Connect account se StripeConnectAccountId vazio, gera link onboarding sempre.
- `VerificarOnboardingTreinadorHandler` (Application/UseCases/Treinadores/VerificarOnboarding) — short-circuit se OnboardingCompleto; senão poll `account.ChargesEnabled` + ConfirmarOnboarding local. Retorna `OnboardingStatusResponse(OnboardingCompleto, ContaConfigurada)`.
- `GerarCobrancaMensalHandler` (Application/UseCases/Pagamentos/GerarCobrancaMensal) — **F12 protege race**. Wrap em `IsolationLevel.Serializable` tx: SELECT pendente → se pendente.StripePaymentIntentId não-null → retorna idempotente; senão MarcarFalhou + Pagamento.Criar + commit. Fora tx: chama StripeService.CriarPix/CartaoPaymentIntent + DefinirDadosPix/Cartao + commit. Catch → MarcarFalhou + rethrow.
- `Pagamento` (Domain/Entities) — agregado: Status (Pendente → Pago/Falhou/Expirado, transições guard'd via DomainException); `DefinirDadosPix`/`DefinirDadosCartao` setam StripePaymentIntentId; PixExpiracao info-only (não auto-marca Expirado — vem do webhook).
- `AssinaturaAluno` (Domain/Entities) — agregado: Status (Pendente → Ativa → Inadimplente/Cancelada), `DataProximaCobranca`. `Ativar()` e `AgendarProximaCobranca(novaData, agora)` chamadas no webhook payment_intent.succeeded.
- `ContaRecebimento` (Domain/Entities) — agregado por treinador: StripeConnectAccountId + OnboardingCompleto. `ConfigurarStripeConnect(accountId)` + `ConfirmarOnboarding()` idempotentes.

## FLUXOS

### Onboarding Stripe Connect (treinador)
1. Treinador autenticado: `POST /treinador/onboarding {urlRetorno, urlCancelamento}` → `IniciarOnboardingTreinadorHandler`.
2. Handler garante ContaRecebimento + (re-)gera link onboarding Stripe Express (`AccountLink.Create`). Retorna URL.
3. Frontend redireciona via `window.location.href = res.data.url`.
4. Treinador completa form na Stripe → Stripe redireciona pra `urlRetorno` (frontend `/treinador/onboarding/retorno`).
5. Pagina retorno: `GET /treinador/onboarding/status` → `VerificarOnboardingTreinadorHandler` → mostra "Cadastro concluído!" se OnboardingCompleto.
6. Em paralelo: webhook `account.updated` com `chargesEnabled=true` marca OnboardingCompleto idempotente (defesa-em-depth).

### Cobrança (treinador inicia OU cron renova)
1. **Manual treinador**: `POST /treinador/pagamentos/cobrar/{assinaturaId}?metodo=Pix|Cartao` (default Pix).
2. **Cron**: GH Actions `billing-renewal.yml` cron `0 8 * * *` UTC → `POST /internal/processar-renovacoes` com header `X-Internal-Key`. Loop em `AssinaturaAlunoRepository.ListarParaRenovarAsync(now)` → chama `GerarCobrancaMensalHandler` por assinatura.
3. Handler: tx serializable: re-uso de pendente OU MarcarFalhou+Criar novo. Fora tx: chama Stripe (CriarPix/CartaoPaymentIntent) com ApplicationFeeAmount+TransferData. Persist intent data.
4. Response: `PagamentoResponse` — `ToResponseTreinador` (sem ClientSecret) ou `ToResponseAluno` (com ClientSecret, no fluxo do aluno).

### Pagamento (aluno)
- **Pix**: aluno abre dialog → `GET /aluno/pagamentos/{id}` polling 30s → exibe QR + copia/cola. Webhook `payment_intent.succeeded` marca Pago.
- **Cartão**: aluno abre dialog → `<Elements stripe={stripePromise} options={{clientSecret}}>` → `<PaymentElement />` (Stripe.js coleta dados card-side, NUNCA passa pelo backend) → `confirmPayment({elements, confirmParams: {return_url: window.location.href}, redirect: "if_required"})`. Sucesso sem redirect (sem 3DS) chama `onPago()`. Falha → `error.message` em AlertBanner.

### Webhook (Stripe → backend)
- `POST /webhooks/stripe` — público, rate limit `webhook`. LimitedStream 64KB DoS guard.
- Header `Stripe-Signature` validado via `EventUtility.ConstructEvent` (HMAC-SHA256 com `Stripe:WebhookSecret`).
- 4 eventos relevantes: `payment_intent.{succeeded,payment_failed,canceled}` + `account.updated`. Outros → 200 log+ignore.
- **Idempotência**: cada handler curto-circuita se `Pagamento.Status != Pendente` (Stripe entrega at-least-once). `ConfirmarOnboarding` curto-circuita se já `OnboardingCompleto=true`.
- **Cross-account defense**: `ValidarConnectAccountAsync` compara `event.AccountId` vs `ContaRecebimento.StripeConnectAccountId` do treinador dono da assinatura. Mismatch → log warning + ignore (defesa contra replay de webhook de outro account assinado pelo mesmo secret).

## ENDPOINTS
| Método/Rota | Auth | Handler | Sucesso | Erros |
|-------------|------|---------|---------|-------|
| POST /treinador/onboarding | Treinador | IniciarOnboardingTreinadorHandler | 200 `{url}` | 403, 404, 500 |
| GET /treinador/onboarding/status | Treinador | VerificarOnboardingTreinadorHandler | 200 `{onboardingCompleto, contaConfigurada}` | 403, 404 |
| POST /treinador/pagamentos/cobrar/{id}?metodo=Pix\|Cartao | Treinador | GerarCobrancaMensalHandler | 200 `PagamentoResponse` (treinador, sem ClientSecret) | 403, 404, 409, 422, 500 |
| GET /aluno/pagamentos/{id} | Aluno | ObterStatusPagamentoHandler | 200 `PagamentoResponse` (aluno, COM ClientSecret) | 403, 404 |
| GET /aluno/pagamentos/assinatura/{id} | Aluno | ListarPagamentosAssinaturaAlunoHandler | 200 `PagamentoResponse[]` | 403, 404 |
| POST /internal/processar-renovacoes | X-Internal-Key | inline `PagamentosEndpoints.cs:77` | 200 `{processadas, falhas}` | 401 |
| POST /webhooks/stripe | nenhum (Stripe-Signature) | ProcessarWebhookStripeHandler | 200 (ok/ignorado) | 400 (assinatura inválida ou payload >64KB) |

⚠ `DomainException` → **422** (UnprocessableEntity, `GlobalExceptionHandler`), NÃO 400. Só `ValidationException` (FluentValidation, formato) → 400.

## INFRA / ROTEAMENTO
- nginx (`nginx/nginx.conf:55`): `location /webhooks/ { proxy_pass http://backend:8080; }` ANTES do `location /` (frontend). Necessário pra header `Stripe-Signature` cru no backend.
- ⚠ NÃO usar `/api/backend/[...path]` (proxy SPA) pra webhook — esse só repassa `content-type`/`accept` e injeta Bearer. Webhook Stripe deve apontar pra `https://<host>/webhooks/stripe`.
- compose homolog (`docker-compose.homolog.yml`) + local (`docker-compose.yml`/`.env.example`) mapeiam `Stripe__*` + `Internal__ApiKey`. NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY no container do frontend.
- Painel Stripe: webhook endpoint = `https://homologacao.forzion.tech/webhooks/stripe`, eventos `payment_intent.succeeded`/`payment_intent.payment_failed`/`payment_intent.canceled`/`account.updated`; signing secret = `STRIPE_WEBHOOK_SECRET`. Connect Express ativado.

## CRON DE RENOVAÇÃO
- Workflow `.github/workflows/billing-renewal.yml` — cron `0 8 * * *` UTC (Daily 08:00 UTC); `workflow_dispatch` pra trigger manual.
- Single step: `curl -f -X POST -H "X-Internal-Key: ${{ secrets.INTERNAL_API_KEY }}" https://${{ secrets.HOMOLOG_HOST }}/internal/processar-renovacoes`.
- `INTERNAL_API_KEY` deve casar exato com env `Internal:ApiKey` do backend.
- Sem fallback interno (não há `IHostedService`/`BackgroundService` pra renovação) — se workflow morre, renovação para. Monitorar via Slack/email `actions failed` notifications.

## FRONTEND
- Componentes (`src/components/pagamento/`): `PagamentoCartao.tsx` (carrega Stripe.js via `loadStripe(NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY)`, wrap `<Elements options={{clientSecret}}>` + `<CartaoForm>` com `useStripe`/`useElements`/`confirmPayment`), `PagamentoPix.tsx` (QR code + polling 30s + clipboard copy).
- Páginas (`src/app/(treinador)/`): `treinador/pagamentos/page.tsx` (status onboarding + botão Configurar), `treinador/onboarding/retorno/page.tsx` (verify-status callback).
- Páginas (`src/app/(aluno)/`): `aluno/pagamentos/page.tsx` (histórico + botão Pagar pendentes).
- API client (`src/lib/api/pagamento.ts`): `iniciarOnboarding`, `verificarOnboarding`, `gerarCobranca`, `obterPagamento`, `listarPagamentosAssinatura`, `obterMinhaAssinatura`.
- E2E (`frontend/e2e/specs/critical/`): `checkout-stripe.spec.ts` (F3 — seeded payment via `garantirPagamentoPendente()` helper), `treinador-onboarding-stripe.spec.ts` (F29 — onboarding flow + status check). Stripe test cards em `frontend/e2e/utils/stripe.ts`.

## SEGURANÇA
- `Internal:ApiKey`: `CryptographicOperations.FixedTimeEquals` (constant-time) + length check antes (evita ArgumentException). Vazio → 401.
- Webhook: assinatura HMAC-SHA256 obrigatória; payload raw 64KB max; rota `/webhooks/` direta no nginx (NÃO `/api/backend`).
- Cross-account: webhook valida `event.AccountId` ≡ `ContaRecebimento.StripeConnectAccountId` antes de mutar Pagamento.
- ClientSecret: incluído em `ToResponseAluno` (necessário pra Stripe.js confirmPayment); OMITIDO em `ToResponseTreinador` (treinador não deve confirmar pagamento do aluno).
- StripeConnectAccountId: nunca exposto em response público (só usado server-side em CriarPaymentIntent + webhook validation).
- Stripe.js coleta dados de cartão client-side via `<PaymentElement>` — nunca tocam o backend forzion. PCI-DSS SAQ-A escopo.
- Rate limits: `internal` 5 req/min, `webhook` (vide `Program.cs`), `read`/`write` padrão.

## TESTES
- Unit (xUnit, sem Docker): Domain (`PagamentoTests` — status transitions + DomainException guards), Application (`ProcessarWebhookStripeHandlerTests`, `GerarCobrancaMensalHandlerTests`, `StripeWebhookParserTests`, `IniciarOnboardingTreinadorHandlerTests`, `VerificarOnboardingTreinadorHandlerTests`), Application/Properties (`MoneyCentavosProperties` — 8 CsCheck invariants), Api (`WebhookEndpointsTests`).
- Integration (Docker): `ConcurrentBillingRaceTests` (F12 — 2 tasks paralelas + Testcontainers Postgres), `AssinaturaAlunoRepositoryTests` etc.
- Frontend: `PagamentoCartao.test.tsx` (F6b+F7 — useStripe partial mock realistic), `(aluno)/__tests__/pagamento.test.tsx` (F6c — 4 fluxos via MSW).
- E2E: `checkout-stripe.spec.ts` + `treinador-onboarding-stripe.spec.ts` (Fase 4 — F3/F29).
- Snapshots: `ResponseDtoSnapshots.PagamentoResponse_Aluno_IncluiClientSecret` + `PagamentoResponse_Treinador_OmiteClientSecret` (F33 — breaking change no DTO falha aqui).

## CLI / DEV LOCAL
- **Stripe CLI** (≥1.40.0): `stripe listen --forward-to localhost:8080/webhooks/stripe` pra forwardar webhooks Stripe real (test mode) pra backend local. `stripe trigger payment_intent.succeeded` simula evento.
- ⚠ **`stripe-projects` plugin é OUT-OF-SCOPE** desta validação — destina-se a provisionar serviços 3ros (Hugging Face, DB, etc.) via Stripe payment, NÃO pra validar/operar integração Stripe deste app. Skip.
- Sem stripe-cli local + sem `stripe listen`, webhook NÃO chega em localhost — flows que dependem dele (PaymentIntent succeeded → MarcarPago) precisam Stripe Test Mode com endpoint público (ngrok/cloudflare tunnel) OU CI E2E.

## DICAS / GOTCHAS
- ApiKey passada explícita em `RequestOptions` por chamada — evita poluição global se múltiplos accounts no futuro.
- `intent.NextAction?.PixDisplayQrCode` lança `InvalidOperationException` se Stripe não retornar dados Pix (geralmente conta sem capability `pix` aprovada). Caller (GerarCobrancaMensalHandler) catch + MarcarFalhou.
- Pix `ExpiresAfterSeconds=3600` (1h). Stripe envia `payment_intent.canceled` ao expirar — único caminho pra MarcarExpirado.
- Sem cleanup proativo de pagamentos zombie (pendente sem StripePaymentIntentId após crash). `GerarCobrancaMensal` re-uso de pendente lida com isso: marca Falhou + cria novo na próxima tentativa (idempotente via tx serializable).
- TestMode vs Live: Stripe não tem "mode flag" — chave `sk_test_*` vs `sk_live_*` discrimina. Cada ambiente tem chave separada. WebhookSecret é POR-endpoint (homolog≠prod obrigatório).
