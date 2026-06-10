# specification-stripe — subsistema de pagamento (forzion.tech)

DOC PARA AGENTES. Fonte de verdade do processo de pagamento (Stripe Connect Express + PaymentIntent Pix/Cartão + webhook + billing mensal). Formato denso, agent-oriented. Consultar antes de alterar onboarding de treinador, cobrança mensal, processamento de webhook, cálculo de taxa de plataforma, fluxo de pagamento Pix/Cartão ou roteamento (nginx). Estrutura de tabelas vive em [specification-db]: `pagamentos`, `assinaturas_aluno`, `conta_recebimento`, `pacotes` — NÃO duplicar aqui.

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA de qualquer mudança relevante em: SDK version, chaves de config, métodos do `IStripeService`, handlers, fluxos (onboarding/cobrança/webhook), endpoints, roteamento (nginx), cron de renovação, defesas de segurança (cross-account, idempotência, rate limit).
- Vive em `specs/` (versionado; commitar). NÃO confundir com `.specs/` (gitignorado).
- Mudança de tabela → atualizar [specification-db], não aqui.

## STACK & GATE
- SDK: Stripe.net `51.1.0` (NuGet no Infrastructure). Pinada em `forzion.tech.Infrastructure.csproj`.
- `IStripeService` (Application/Interfaces): 12 métodos:
  - `CriarContaConnectAsync(email, nome, ct)` → `accountId` (conta Express, `Country=BR`, capabilities `card_payments`+`transfers` — BR exige ambas; exige Connect habilitado na conta, ver §Connect Express por ambiente)
  - `GerarLinkOnboardingAsync(accountId, urlRetorno, urlCancelamento, ct)` → URL
  - `CriarPixPaymentIntentAsync(valor, accountId, pagamentoId, taxaPlataformaPercent, ct)` → `PixPaymentResult(intentId, qrCode, qrCodeUrl, expiracao)` (fluxo aluno — Connect + fee split)
  - `CriarCartaoPaymentIntentAsync(valor, accountId, pagamentoId, taxaPlataformaPercent, ct)` → `CartaoPaymentResult(intentId, clientSecret)` (fluxo aluno — Connect + fee split)
  - `CriarPixPlataformaPaymentIntentAsync(valor, pagamentoTreinadorId, ct)` → `PixPaymentResult` — **plano do treinador**: valor CHEIO p/ conta da plataforma (sem `accountId`/`TransferData`/`ApplicationFeeAmount`/`taxaPercent`). Idempotency key `pagamento-{guid_n}`.
  - `CriarCartaoPlataformaPaymentIntentAsync(valor, pagamentoTreinadorId, ct)` → `CartaoPaymentResult` — idem, cartão.
  - `ContaEstaAtivadaAsync(accountId, ct)` → `bool` (poll `account.ChargesEnabled`)
  - `ValidarWebhookAsync(payload, assinaturaHeader)` → `bool` (`EventUtility.ConstructEvent` HMAC-SHA256)
  - `ListarEventosDesdeAsync(desdeUtc, ct)` → `IReadOnlyList<StripeEventSummary>` (reconciliação — ver §RECONCILIAÇÃO)
  - `CriarReembolsoAsync(paymentIntentId, reverterTransferencia, ct)` → reembolso total (CDC 7d — ver §CANCELAMENTO/REEMBOLSO). `reverterTransferencia=true` ⇒ `RefundCreateOptions{ PaymentIntent, ReverseTransfer=true, RefundApplicationFee=true }` (charge destino do aluno); `false` ⇒ só `{ PaymentIntent }` (charge direto-plataforma do treinador). `Amount` não enviado = total.
  - `CancelarPaymentIntentAsync(paymentIntentId, ct)` → `PaymentIntentService.CancelAsync`. Cancela PI não-capturado (ex.: Pix pendente ao treinador trocar p/ modo Externo). Best-effort: lança se PI já terminal — caller (`AlterarModoPagamentoTreinadorHandler`) faz try/catch + LogWarning.
  - `EnviarEvidenciaDisputaAsync(disputeId, DisputaEvidencia, ct)` → `Dispute.Update(disputeId, { Evidence })` — resposta automática a chargeback (ver §CHARGEBACKS). **disputeId, NÃO chargeId/paymentIntentId.** `DisputaEvidencia(EmailCliente, DataAtivacao, DataUltimaAtividade, DataUltimoPagamento)` → `CustomerEmailAddress`, `ServiceDate` (yyyy-MM-dd), `UncategorizedText` (datas concatenadas).
- Impl única: `StripeService` (Infrastructure/Services). Sem `NullStripeService` — `StripeSettings.ValidateOnStart` exige `SecretKey`/`WebhookSecret` não-vazios (boot falha sem config).
- `RequestOptions { ApiKey }` passada em cada chamada (sem estado global).
- Métodos `Create*PaymentIntent`: usam `MoneyCentavos.ValorETaxaCentavos(valor, taxaPercent)` (Application/UseCases/Pagamentos) — extração de F16 (truncamento via `(long)`; sum preservation ≤1 centavo). NÃO usar Math.Round / banker's rounding.
- Connect flow: `ApplicationFeeAmount=taxaCentavos` + `TransferData.Destination=stripeAccountId` (taxa fica na plataforma, restante vai pro treinador).
- Metadata: `["pagamento_id"]=pagamento.Id.ToString()` — útil pra reconciliação.
- `Stripe-Idempotency-Key` header passado em `CriarPix/CartaoPaymentIntentAsync` via `PaymentIntentRequestOptions(pagamentoId)` — key estável `pagamento-{guid_n}`. Belt-and-suspenders sobre F12 serializable tx: retry de network/transport NÃO cria 2º PaymentIntent (Stripe responde idêntico até 24h depois com mesma key). `CriarContaConnectAsync`/`GerarLinkOnboardingAsync` ficam sem key (idempotency menos crítico).

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
- `ProcessarWebhookStripeHandler` (Application/UseCases/Pagamentos/ProcessarWebhookStripe) — os 3 `payment_intent.*` BIFURCAM por `evento.TipoMetadata == "plano_treinador"` (metadata `tipo`): plano do treinador → `ProcessarPagamentoTreinador{Pago,Falhou,Transicao}Async` (sem cross-account); aluno → caminho Connect abaixo. **Aluno**: `payment_intent.succeeded` → MarcarPago + RegistrarPagamentoRegularizado + (se Pendente) Ativar() + AgendarProximaCobranca(+1mês); `payment_intent.payment_failed` → MarcarFalhou + AssinaturaAluno.RegistrarPagamentoFalho; `payment_intent.canceled` → MarcarExpirado (Stripe envia auto após `ExpiresAfterSeconds=3600` no Pix). **Plano treinador**: succeeded → `PagamentoTreinador.MarcarPago` + (se `Finalidade==Cadastro`) `FinalizarCadastroAsync` no MESMO commit (AssinaturaTreinador.Ativar + AgendarProximaCobranca + Treinador.ConfirmarPagamentoPlano + `Conta.EmitirRegistro` que dispara a verificação de e-mail adiada); failed → MarcarFalhou + AssinaturaTreinador.RegistrarPagamentoFalho; canceled → MarcarExpirado. Comuns aos dois fluxos: `account.updated` (chargesEnabled=true) → ContaRecebimento.ConfirmarOnboarding; `account.updated` (chargesEnabled=false) → carrega `contaRecebimento` e só `LogCritical` se `OnboardingCompleto==true` (conta estava live e caiu); onboarding incompleto/conta nula = silencioso (false é normal durante onboarding) — ambos retornam `JaConsistente`; `charge.refunded` → MarcarEstornado (refund manual treinador via Dashboard); `charge.dispute.created` → MarcarEmDisputa + assinatura Ativa → Inadimplente + `EnviarEvidenciaDisputaAsync` (resposta automática). Default → log + ignore. Retorna `ProcessarEventoResultado` (Aplicado/JaConsistente/Ignorado) p/ reconciliação.
- `StripeWebhookParser` (Application/UseCases/Pagamentos/ProcessarWebhookStripe) — parse `JsonNode`: type, paymentIntentId, accountId, chargesEnabled, amountRefundedCents (charge.refunded), motivoDisputa (charge.dispute.created), **tipoMetadata** (`data.object.metadata.tipo`, só p/ `payment_intent.*` — discrimina plano-treinador), **disputeId** (`data.object.id` em `charge.dispute.created` — o Dispute object; exigido por `EnviarEvidenciaDisputaAsync`). Para `charge.refunded`/`charge.dispute.created`, `paymentIntentId` é lido de `data.object.payment_intent` (Charge/Dispute apontam pro PI subjacente). **Parse defensivo (T5)**: todos os lookups usam `TryGetStringValue`/`TryGetLongValue` (retornam null em vez de lançar quando Stripe expandir o campo como objeto em vez de string) — evita poison-retry (400 em loop).
- `IniciarOnboardingTreinadorHandler` (Application/UseCases/Treinadores/IniciarOnboarding) — idempotente: cria ContaRecebimento se ausente, cria Stripe Connect account se StripeConnectAccountId vazio, gera link onboarding sempre.
- `VerificarOnboardingTreinadorHandler` (Application/UseCases/Treinadores/VerificarOnboarding) — short-circuit se OnboardingCompleto; senão poll `account.ChargesEnabled` + ConfirmarOnboarding local. Retorna `OnboardingStatusResponse(OnboardingCompleto, ContaConfigurada)`.
- `AlterarModoPagamentoTreinadorHandler` (Application/UseCases/Treinadores/AlterarModoPagamento) — troca `ModoPagamentoAluno` pós-signup (tx serializable). Guards (ordem): modo_inalterado → (se→Plataforma) onboarding completo senão `configure_stripe_primeiro` → `Treinador.AlterarModoPagamento` (cooldown 90d). **→Externo**: cancela todas `AssinaturaAluno` não-Cancelada (`ListarNaoCanceladasPorTreinadorAsync`), coleta os PaymentIntents Pix pendentes e os cancela via `CancelarPaymentIntentAsync` **APÓS o commit** (efeito externo pós-persistência, best-effort + LogWarning; espelha `CancelarMinhaAssinaturaAlunoHandler` — nada de Stripe dentro da tx). Connect fica ociosa. **→Plataforma**: cria `AssinaturaAluno` (Pendente) p/ cada vínculo ativo com pacote **ativo e preço>0** sem assinatura (cobrança segue fluxo normal; sem Pix imediato); vínculos sem pacote ativo/cobrável contam em `VinculosIgnorados`. **Supressão de notificação (bulk administrativo)**: `ClearDomainEvents()` nas assinaturas canceladas/criadas → sem e-mail/WhatsApp por-aluno nem auto-spam ao treinador. Resposta `AlterarModoPagamentoResponse(Modo, AlteradoEm, AssinaturasCriadas, VinculosIgnorados)`. Endpoint `POST /treinador/modo-pagamento`. Frontend `treinador/pagamentos`.
- `GerarCobrancaMensalHandler` (Application/UseCases/Pagamentos/GerarCobrancaMensal) — **F12 protege race**. Wrap em `IsolationLevel.Serializable` tx: SELECT pendente → se pendente.StripePaymentIntentId não-null → retorna idempotente; senão MarcarFalhou + Pagamento.Criar + commit. Fora tx: chama StripeService.CriarPix/CartaoPaymentIntent + DefinirDadosPix/Cartao + commit. Catch → MarcarFalhou + rethrow.
- `Pagamento` (Domain/Entities) — agregado: Status (Pendente → Pago/Falhou/Expirado; Pago → Estornado/EmDisputa, transições guard'd via DomainException); `DefinirDadosPix`/`DefinirDadosCartao` setam StripePaymentIntentId; PixExpiracao info-only (não auto-marca Expirado — vem do webhook). `MarcarEstornado()` preserva `DataPagamento` + dispara `PagamentoEstornadoEvent` (e-mail + WhatsApp pro aluno). `MarcarEmDisputa(motivo)` preserva `DataPagamento` + dispara `PagamentoEmDisputaEvent` (e-mail urgente + alert log Critical pro treinador; aluno NÃO notificado — já sabe). Refund NÃO cascateia em cancelamento de assinatura; disputa **força** Ativa → Inadimplente via `AssinaturaAluno.MarcarInadimplentePorDisputa` (congelamento imediato, não espera contador).
- `AssinaturaAluno` (Domain/Entities) — agregado: Status (Pendente → Ativa → Inadimplente/Cancelada), `DataProximaCobranca`. `Ativar()` e `AgendarProximaCobranca(novaData, agora)` chamadas no webhook payment_intent.succeeded. `MarcarInadimplentePorDisputa(agora)` força Ativa → Inadimplente com `TentativasFalhasConsecutivas = LimiteTentativasFalhas` (sinalização) — chamado em `charge.dispute.created` (fraude/desistência drástica do aluno).
- `ContaRecebimento` (Domain/Entities) — agregado por treinador: StripeConnectAccountId + OnboardingCompleto. `ConfigurarStripeConnect(accountId)` + `ConfirmarOnboarding()` idempotentes.

### Plano do treinador (billing direto-plataforma — commits dbf0074/9cc27e4)
Cobrança do treinador pelo próprio plano (cadastro/renovação/troca) — NÃO usa Connect; valor cheio vai pra plataforma. Entidade e fluxo separados do billing do aluno.
- `PagamentoTreinador` (Domain/Entities) — agregado: Status (Pendente → Pago/Falhou/Expirado; Pago → Estornado/EmDisputa), `Finalidade` (enum `FinalidadePagamentoTreinador`: `Cadastro=0`/`Renovacao=1`/`TrocaPlano=2`), `Metodo`, `StripePaymentIntentId`. `Criar(treinadorId, assinaturaTreinadorId, valor, finalidade, agora, metodo)`; `DefinirDadosPix`/`DefinirDadosCartao`; `MarcarPago`/`MarcarFalhou`/`MarcarExpirado`. `MarcarPago` dispara `PagamentoTreinadorPagoEvent`. **T4**: `MarcarEstornado(agora)` — só de Pago, SEM evento; o handler `ProcessarEstornoTreinadorAsync` adicionalmente chama `assinatura.MarcarInadimplentePorDisputa` para congelar acesso do treinador. `MarcarEmDisputa(agora)` — só de Pago, SEM evento; handler `ProcessarDisputaTreinadorAsync` idem. Sem `PagamentoTreinadorEstornadoEvent`/`EmDisputaEvent` — Arthur monitora via log (sem e-mail de notificação para estorno/disputa do plano da plataforma).
- `IPagamentoTreinadorRepository` (Application/Interfaces/Repositories) — `ObterPorStripePaymentIntentIdAsync`, `ObterPendentePorAssinaturaAsync`, `AdicionarAsync`. Impl `PagamentoTreinadorRepository` (Infrastructure).
- `IniciarPagamentoPlanoHandler` (Application/UseCases/Treinadores/IniciarPagamentoPlano) — fluxo CADASTRO: gate `Treinador.Status==AguardandoPagamento` + AssinaturaTreinador Pendente. Tx serializable (mesma estratégia G-PAY-1 do GerarCobrancaMensal): re-uso de pendente OU marca zumbi Falhou + `PagamentoTreinador.Criar(Cadastro)`; chama `CriarPix/CartaoPlataformaPaymentIntentAsync(valor, pagamento.Id)` + DefinirDados; single commit. Valor vem da assinatura (nunca do caller).
- `GerarCobrancaPlanoTreinadorHandler` (Application/UseCases/Treinadores/GerarCobrancaPlanoTreinador) — RENOVAÇÃO mensal: chamado pelo cron `/internal/processar-renovacoes-treinador` por assinatura a vencer. Gate: rejeita Cancelada/Pendente. Tx serializable (G-PAY-1). **Aplica downgrade agendado ANTES de cobrar**: se `PlanoPlataformaIdAgendado` setado → `AplicarPlanoAgendado(planoAgendado.Preco)` (promove agendado→atual + valor); plano agendado com `Preco≤0` (Free) → `assinatura.Cancelar` SEM cobrança (encerra); plano agendado excluído da plataforma → `LimparPlanoAgendado` + prossegue com plano atual. Depois: re-uso pendente/zumbi (G-PAY-1) + `PagamentoTreinador(Renovacao, valor=assinatura.Valor)` + PaymentIntent plataforma.
- `TrocarPlanoTreinadorHandler` (Application/UseCases/Treinadores/TrocarPlanoTreinador) — TROCA de plano. **Valor SEMPRE server-derived** (`PlanoPlataforma.Preco`/proração; nunca do caller). Gates: plano existe+ativo+≠Elite; assinatura ≠Cancelada; ≠mesmo plano. Ramifica 3-vias:
  - **Inadimplente** (`ProcessarInadimplenteAsync`): regulariza trocando — `PagamentoTreinador(TrocaPlano, valor=novoPlano.Preco CHEIO, PlanoAlvoId)` + PaymentIntent plataforma (tx serializable, mesmo re-uso/zumbi de G-PAY-1). Webhook pago → handler aplica `RegistrarPagamentoRegularizado` + `TrocarPlanoImediato` + `AgendarProximaCobranca(+1mês)`.
  - **Upgrade** (`novoPlano.Preco > assinatura.Valor`, `ProcessarUpgradeAsync`): proração COBRADA AGORA = `Round((novoPlano.Preco − Valor) × diasRestantes / 30, 2, AwayFromZero)` (`diasRestantes = max(0, DataProximaCobranca − agora)`). Proração ≤0 → `TrocarPlanoImediato` sem cobrança (commit direto). Proração >0 → `PagamentoTreinador(TrocaPlano, valor=proração, PlanoAlvoId)` + PaymentIntent (tx serializable; re-uso idempotente se pendente mesma TrocaPlano+PlanoAlvo+intent≠null; senão Falhou+novo).
  - **Downgrade** (`novoPlano.Preco ≤ Valor`, `ProcessarDowngradeAsync`): SEM cobrança imediata — `AssinaturaTreinador.AgendarDowngrade(novoPlanoId)` (set `PlanoPlataformaIdAgendado`); aplicado na próxima renovação por `GerarCobrancaPlanoTreinadorHandler.AplicarPlanoAgendado`. Response `Downgrade(DataProximaCobranca)`.
  - Frontend `treinador/plano/page.tsx`.
- `PagamentoTreinadorPagoHandler` (Infrastructure/Handlers) — handler de `PagamentoTreinadorPagoEvent`, ramifica por `Finalidade`: `Cadastro` → no-op (já finalizado INLINE no webhook, atomicidade); `Renovacao` → `RegistrarPagamentoRegularizado` (zera contador/reativa se inadimplente) + `AgendarProximaCobranca(+1mês)`; `TrocaPlano` → carrega `PlanoAlvoId`, se inadimplente `RegistrarPagamentoRegularizado` + reinicia ciclo, `TrocarPlanoImediato(novoPlano)`. Lookups faltando/guard fail → log warn + return (não relança; webhook não deve retentar indefinidamente).

## FLUXOS

### Onboarding Stripe Connect (treinador)
1. Treinador autenticado: `POST /treinador/onboarding {urlRetorno, urlCancelamento}` → `IniciarOnboardingTreinadorHandler`.
2. Handler garante ContaRecebimento + (re-)gera link onboarding Stripe Express (`AccountLink.Create`). Retorna URL.
3. Frontend redireciona via `window.location.href = res.data.url`.
4. Treinador completa form na Stripe → Stripe redireciona pra `urlRetorno` (frontend `/treinador/onboarding/retorno`).
5. Pagina retorno: `GET /treinador/onboarding/status` → `VerificarOnboardingTreinadorHandler` → mostra "Cadastro concluído!" se OnboardingCompleto.
6. Em paralelo: webhook `account.updated` com `chargesEnabled=true` marca OnboardingCompleto idempotente (defesa-em-depth).
- **Gate de onboarding (OnboardingCompleto)** bloqueia o caminho de dinheiro em 3 pontos: `AprovarVinculoHandler` (aceitar aluno → `treinador_sem_onboarding`, gate PRIMÁRIO antes de qualquer efeito), `CriarAssinaturaAlunoHandler` e `GerarCobrancaMensalHandler` (`treinador_sem_conta_stripe`). `VinculoAprovadoCriarAssinaturaAlunoHandler` mantém skip como defense-in-depth. Sem onboarding o treinador loga/usa o app mas NÃO aceita alunos nem cobra.

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
- 6 eventos relevantes: `payment_intent.{succeeded,payment_failed,canceled}` + `account.updated` + `charge.refunded` + `charge.dispute.created`. Outros → 200 log+ignore.
- **Gate plano-treinador**: os 3 `payment_intent.*` ramificam por `metadata.tipo == "plano_treinador"` (parser extrai como `TipoMetadata`) → `ProcessarPagamentoTreinador*Async` (sobre `PagamentoTreinador`, sem cross-account); ausente/outro → fluxo aluno (Connect). Plano-treinador NÃO tem `account` (direto-plataforma).
- **Idempotência**: cada handler curto-circuita por estado terminal — `Status != Pendente` para os 3 `payment_intent.*` (vale p/ Pagamento e PagamentoTreinador); `Status == Estornado` para `charge.refunded`; `Status == EmDisputa` para `charge.dispute.created`. Stripe entrega at-least-once. `ConfirmarOnboarding` curto-circuita se já `OnboardingCompleto=true`.
- **Roteamento charge.refunded / charge.dispute.created (T4)**: handler tenta primeiro carregar `Pagamento` (aluno) por `PaymentIntentId`; não encontrado → tenta `PagamentoTreinador`. Ambos os tipos são tratados: `ProcessarEstornoTreinadorAsync` e `ProcessarDisputaTreinadorAsync` chamam `MarcarEstornado`/`MarcarEmDisputa` no `PagamentoTreinador` + `assinatura.MarcarInadimplentePorDisputa` na `AssinaturaTreinador`.
- **FinalizarCadastroAsync — retry por exceção (T1)**: quando `Finalidade==Cadastro` e `payment_intent.succeeded` chega, `FinalizarCadastroAsync` é chamado atomicamente ANTES do commit. Qualquer falha de domínio (`Ativar`/`ConfirmarPagamentoPlano`) ou ausência de entidade (assinatura/treinador/conta não encontrados) **lança `InvalidOperationException`** — impede o `CommitAsync`, força 500 na resposta do webhook, e o Stripe retenta. Garante que `PagamentoTreinador.Pago` nunca é persistido sem assinatura ativada (sem estado parcial).
- **Cross-account defense**: `ValidarConnectAccountAsync` compara `event.AccountId` vs `ContaRecebimento.StripeConnectAccountId` do treinador dono da assinatura. **Distinção connect-null vs mismatch (T2)**: (a) `ContaRecebimento` existe mas `StripeConnectAccountId` é null → **lança `InvalidOperationException`** (drift de configuração: aluno pagou via account mas treinador sem Connect — nunca deve ocorrer; força 500 + retry Stripe para não deixar pagamento preso em Pendente); (b) mismatch de account → log warning + retorna `JaConsistente` (defesa contra replay de webhook de outro account). **Retorno (T3)**: `ValidarConnectAccountAsync` retorna `(bool Valido, AssinaturaAluno? Assinatura)` — quando válido, entrega a `AssinaturaAluno` já carregada (evita segundo round-trip no `ProcessarPagamentoPagoAsync`/`FalhouAsync`). Não aplicada em `charge.refunded`/`charge.dispute.created` (sem vetor útil — refund/dispute invertem dinheiro do próprio destino).

## ENDPOINTS
| Método/Rota | Auth | Handler | Sucesso | Erros |
|-------------|------|---------|---------|-------|
| POST /treinador/onboarding | Treinador | IniciarOnboardingTreinadorHandler | 200 `{url}` | 400 (URL fora do domínio), 403, 404, 422, 500 (`Stripe:UrlBase` ausente = misconfig + LogError) |
| POST /treinador/modo-pagamento | Treinador | AlterarModoPagamentoTreinadorHandler | 200 `{modo, alteradoEm}` | 403, 404, 422 (`modo_inalterado`/`cooldown_modo_pagamento`/`configure_stripe_primeiro`) |
| GET /treinador/onboarding/status | Treinador | VerificarOnboardingTreinadorHandler | 200 `{onboardingCompleto, contaConfigurada}` | 403, 404 |
| POST /treinador/pagamentos/cobrar/{id}?metodo=Pix\|Cartao | Treinador | GerarCobrancaMensalHandler | 200 `PagamentoResponse` (treinador, sem ClientSecret) | 403, 404, 409, 422, 500 |
| GET /aluno/pagamentos/{id} | Aluno | ObterStatusPagamentoHandler | 200 `PagamentoResponse` (aluno, COM ClientSecret) | 403, 404 |
| GET /aluno/pagamentos/assinatura/{id} | Aluno | ListarPagamentosAssinaturaAlunoHandler | 200 `PagamentoResponse[]` | 403, 404 |
| POST /internal/processar-renovacoes | X-Internal-Key | inline lambda em `PagamentosEndpoints` (loop GerarCobrancaMensalHandler) | 200 `{processadas, falhas}` | 401 |
| POST /internal/processar-renovacoes-treinador | X-Internal-Key | inline lambda em `PagamentosEndpoints` (loop GerarCobrancaPlanoTreinadorHandler) | 200 `{processadas, falhas}` | 401 |
| POST /webhooks/stripe | nenhum (Stripe-Signature) | ProcessarWebhookStripeHandler | 200 (ok/ignorado) | 400 (assinatura inválida ou payload >64KB) |

⚠ Mapa de status `DomainException`→422 / `ValidationException`→400 (`GlobalExceptionHandler`): canônico em [specification-email] §ENDPOINTS.

## INFRA / ROTEAMENTO
- Roteamento nginx do bloco `location /webhooks/` → backend (ANTES de `location /`): canônico em [specification-email] §INFRA/ROTEAMENTO. Aplica igual ao Stripe — necessário pra header `Stripe-Signature` cru. Webhook Stripe aponta pra `https://<host>/webhooks/stripe` (NUNCA `/api/backend/[...path]`, que descarta o header e injeta Bearer).
- compose homolog (`docker-compose.homolog.yml`) + local (`docker-compose.yml`/`.env.example`) mapeiam `Stripe__*` + `Internal__ApiKey`. NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY no container do frontend.

### Stripe Test Mode vs Live Mode (chaves por ambiente)
**Canônico**: Stripe não tem sandbox/mode-flag — `sk_test_*` vs `sk_live_*` discrimina; cada ambiente usa par próprio + WebhookSecret por-endpoint (hmg≠prod obrigatório).

| Ambiente | Mode | SecretKey prefix | PublishableKey prefix | Webhook endpoint | Webhook signing secret |
|----------|------|------------------|------------------------|------------------|------------------------|
| **Local dev** | Test | `sk_test_*` | `pk_test_*` | `stripe listen --forward-to localhost:8080/webhooks/stripe` (CLI) | gerado pelo `stripe listen` |
| **Homolog** | Test | `sk_test_*` | `pk_test_*` | `https://homologacao.forzion.tech/webhooks/stripe` | painel Stripe → Test → Webhooks → endpoint hmg |
| **Prod** | Live | `sk_live_*` | `pk_live_*` | `https://app.forzion.tech/webhooks/stripe` | painel Stripe → Live → Webhooks → endpoint prod |

**Test mode em hmg cobra ZERO** (cartões `4242…` e2e, PaymentIntent+webhook fluem, Connect Express test sem KYC). **⚠ NUNCA `sk_live_*` em hmg** (cobrança real + KYC real + reverter exige refund manual).

### Setup webhook (per ambiente, hmg e prod)
1. Login Stripe → toggle **Test mode** (hmg) ou **Live mode** (prod) no canto superior direito do painel.
2. Developers → Webhooks → Add endpoint.
3. URL: `https://<host>/webhooks/stripe` (hmg=homologacao, prod=app).
4. Events: `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.canceled`, `account.updated`, `charge.refunded`, `charge.dispute.created`.
5. Copy signing secret → `STRIPE_WEBHOOK_SECRET` no `/opt/forzion/.env` da VM correspondente.
6. `docker compose -f docker-compose.<env>.yml restart` na VM pra pegar novo env.

### Connect Express por ambiente
- **PRÉ-REQUISITO (1 vez por conta Stripe): habilitar o Connect** no Dashboard antes do 1º onboarding, senão `CriarContaConnectAsync` falha com `StripeException: You can only create new accounts if you've signed up for Connect`. Em **Test mode**: https://dashboard.stripe.com/connect → "Get started" (instantâneo). Modelo do forzion = **marketplace** (dois lados + comissão via destination charge + application fee); "platform" também habilita o Connect, mas marketplace descreve melhor.
- **Capabilities BR (gotcha)**: contas Express em `Country=BR` exigem `card_payments` **junto** de `transfers` — pedir `transfers` sozinho falha `You cannot request the 'transfers' capability without the 'card_payments' capability for accounts in BR`. `CriarContaConnectAsync` solicita as duas.
- Hmg (Test): test accounts liberam instant, sem KYC. Usar pra E2E checkout-stripe.spec.ts.
- Prod (Live): Stripe exige Connect Express **profile review** (1-3 dias, aprovação humana) antes do primeiro account real. Iniciar antes do go-live.

### Variáveis do `/opt/forzion/.env` por ambiente
**Hmg** (Test):
```
STRIPE_SECRET_KEY=sk_test_...
STRIPE_PUBLISHABLE_KEY=pk_test_...
STRIPE_WEBHOOK_SECRET=whsec_...        # signing secret do endpoint hmg em Test mode
STRIPE_URL_BASE=https://homologacao.forzion.tech
STRIPE_TAXA_PLATAFORMA=5
INTERNAL_API_KEY=<openssl rand -hex 32>
```

**Prod** (Live):
```
STRIPE_SECRET_KEY=sk_live_...
STRIPE_PUBLISHABLE_KEY=pk_live_...
STRIPE_WEBHOOK_SECRET=whsec_...        # signing secret do endpoint prod em Live mode
STRIPE_URL_BASE=https://app.forzion.tech
STRIPE_TAXA_PLATAFORMA=5
INTERNAL_API_KEY=<openssl rand -hex 32, valor SEPARADO do hmg>
```

⚠ `INTERNAL_API_KEY` POR-ambiente (não compartilhar). Sincronizar com GitHub secret `INTERNAL_API_KEY` do environment correspondente pro workflow `billing-renewal.yml`.

## CRON DE RENOVAÇÃO
- Workflow `.github/workflows/billing-renewal.yml` — cron `0 8 * * *` UTC (Daily 08:00 UTC); `workflow_dispatch` pra trigger manual.
- Single step: `curl -f -X POST -H "X-Internal-Key: ${{ secrets.INTERNAL_API_KEY }}" https://${{ secrets.HOMOLOG_HOST }}/internal/processar-renovacoes`.
- `INTERNAL_API_KEY` deve casar exato com env `Internal:ApiKey` do backend.
- Sem fallback interno (não há `IHostedService`/`BackgroundService` pra renovação) — se workflow morre, renovação para.
- **Monitoring:** step `if: failure()` cria GitHub Issue automática com label `billing-renewal-failed` (via `actions/github-script@v7`). GitHub notifica Arthur por e-mail/Slack se inscrito.
- **Plano do treinador**: workflow paralelo `billing-renewal-treinador.yml` (cron `0 8 * * *` UTC) → `POST /internal/processar-renovacoes-treinador` → loop `GerarCobrancaPlanoTreinadorHandler`. Mesmo padrão X-Internal-Key + Issue on failure.

## RECONCILIAÇÃO PERIÓDICA (safety net webhook)
- Workflow `.github/workflows/billing-reconciliation.yml` — cron `0 4 * * 1` UTC (Segunda 04:00 UTC, weekly); `workflow_dispatch` com input `desde_utc`.
- Chama `POST /internal/reconciliar-pagamentos` com X-Internal-Key (same FixedTimeEquals pattern). Body `{}` = últimos 7 dias; override via `desdeUtc` ISO-8601.
- Backend handler `ReconciliarPagamentosStripeHandler` (Application):
  1. `IStripeService.ListarEventosDesdeAsync(desdeUtc)` — `EventService.ListAutoPagingAsync` filtrando por `payment_intent.{succeeded,payment_failed,canceled}` + `account.updated` + `charge.refunded` + `charge.dispute.created`, cap 1000 eventos.
  2. Por evento: chama `ProcessarWebhookStripeHandler.ProcessarEventoAsync(evento, ct)` — método refatorado pra ser reutilizável sem signature verification (eventos vêm autenticados via nossa API key).
  3. Resultado por evento: `Aplicado` | `JaConsistente` | `Ignorado`. Reconciliator agrega → `{ TotalEventos, Replayed, JaConsistentes, Erros }`.
  4. Idempotência preservada: handler interno checa estado terminal por tipo de evento (`Pagamento.Status != Pendente` para `payment_intent.*`; `Status == Estornado` para `charge.refunded`; `Status == EmDisputa` para `charge.dispute.created`).
- Pega webhooks perdidos (Stripe retry policy desistiu OU rede falhou OU backend estava down).
- **Monitoring:** step `if: failure()` cria GitHub Issue com label `billing-reconciliation-failed` (mesmo pattern do billing-renewal).

## FRONTEND
- Componentes (`src/components/pagamento/`): `PagamentoCartao.tsx` (carrega Stripe.js via `loadStripe(NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY)`, wrap `<Elements options={{clientSecret}}>` + `<CartaoForm>` com `useStripe`/`useElements`/`confirmPayment`), `PagamentoPix.tsx` (QR code + polling 30s + clipboard copy).
- Páginas (`src/app/(treinador)/`): `treinador/pagamentos/page.tsx` (status onboarding + botão Configurar), `treinador/onboarding/retorno/page.tsx` (verify-status callback).
- Páginas (`src/app/(aluno)/`): `aluno/pagamentos/page.tsx` (histórico + botão Pagar pendentes).
- API client (`src/lib/api/pagamento.ts`): `iniciarOnboarding`, `verificarOnboarding`, `gerarCobranca`, `obterPagamento`, `listarPagamentosAssinatura`, `obterMinhaAssinatura`.
- E2E (`frontend/e2e/specs/critical/`): `checkout-stripe.spec.ts` (F3 — seeded payment via `garantirPagamentoPendente()` helper), `treinador-onboarding-stripe.spec.ts` (F29 — onboarding flow + status check). Stripe test cards em `frontend/e2e/utils/stripe.ts`.

## NOTIFICAÇÃO + INADIMPLÊNCIA
- `Pagamento.Criar` dispara `PagamentoCriadoEvent(PagamentoId, AssinaturaAlunoId, Valor, MetodoPagamento, OcorridoEm)`. Handlers `PagamentoCriadoEmailHandler` + `PagamentoCriadoWhatsAppNotifierHandler` notificam aluno com link `/aluno/pagamentos`. Resolução de destinatário: assinatura → aluno; e-mail = `Aluno.Email` (preferência) ou `Conta.Email` (fallback); telefone = `Aluno.Telefone` (skip se null).
- `AssinaturaAluno.RegistrarPagamentoFalho(agora)` chamado pelo webhook `payment_intent.payment_failed`. Incrementa `TentativasFalhasConsecutivas`, dispara `PagamentoFalhouEvent(AssinaturaAlunoId, AlunoId, TentativasFalhasConsecutivas, OcorridoEm)`. Quando contador atinge `LimiteTentativasFalhas` (= 3) e status era Ativa, transiciona pra **Inadimplente** e dispara `AssinaturaAlunoMarcadaInadimplenteEvent(AssinaturaAlunoId, AlunoId, TreinadorId, Tentativas, OcorridoEm)`. Assinatura Cancelada → no-op; Pendente → conta sem transicionar.
- `AssinaturaAluno.RegistrarPagamentoRegularizado(agora)` chamado pelo webhook `payment_intent.succeeded`. Zera contador + reativa (Inadimplente → Ativa). Idempotente. Cancelada permanece Cancelada.
- Notificação progressiva (handlers `PagamentoFalhouEmail` + `PagamentoFalhouWhatsAppNotifier`):
  - 1ª falha: e-mail "tente outro método"; WhatsApp **skip** (não spammar).
  - 2ª falha: e-mail "atualize seu cartão"; WhatsApp envia.
  - 3ª falha: handler de `AssinaturaAlunoMarcadaInadimplenteEvent` envia e-mail "conta restrita" + WhatsApp emergência.
- Templates: `EmailTemplates.CobrancaFalhou(nome, valor, tentativas, link)` (tom escala com `tentativas`) + `EmailTemplates.AssinaturaInadimplente(nome, tentativas, link)`.
- Link sempre `AppSettings.FrontendBaseUrl + "/aluno/pagamentos"`. NUNCA Stripe direto (anti-phishing).

### Enforcement backend
- `RequireAssinaturaAtivaFilterBase` (`forzion.tech.Api/Filters/`) — base abstrata `IEndpointFilter` (T8). **GETs liberados automaticamente** (preserva histórico/LGPD — sem checar assinatura em leitura). Injeta `IUserContext` via `RequestServices`; delega checagem a `EstaInadimplenteAsync(services, userContext, ct)` (abstract). Se inadimplente → **403 ProblemDetails** com `code = CodigoErro` (abstract), title `"Assinatura inadimplente"`, detail `"Regularize seu pagamento para continuar usando esta funcionalidade."`.
- `RequireAssinaturaAtivaFilter : RequireAssinaturaAtivaFilterBase` — impl aluno (`CodigoErro = "ASSINATURA_INADIMPLENTE"`). `TipoConta != Aluno` → false. Resolve aluno via `IAlunoRepository.ObterPorContaIdAsync`, depois `IAssinaturaAlunoRepository.ListarPorAlunoAsync` (última não-Cancelada). Inadimplente → 403.
- `RequireAssinaturaTreinadorAtivaFilter : RequireAssinaturaAtivaFilterBase` — impl treinador (`CodigoErro = "ASSINATURA_TREINADOR_INADIMPLENTE"`). `TipoConta != Treinador` → false. Atualmente retorna false (placeholder — billing treinador implementa consulta a `IAssinaturaTreinadorRepository` aqui quando enforcement ativo).
- Aplicado em: `POST /aluno/execucoes` (registrar nova execução de treino). Expandir conforme necessidade — princípio: bloquear **consumo de feature paga**, NUNCA bloquear leitura (LGPD/CDC: histórico que já pagou deve continuar visível) nem `/aluno/pagamentos` (precisa pra regularizar).
- Ambos Transient em DI (`forzion.tech.Api/Extensions/DependencyInjectionExtensions.cs`).

### UI bloqueio (frontend)
- `AlunoInadimplenteBanner` (`frontend/src/components/aluno/`) — MUI Alert persistente top-anchored, com CTA `Regularizar agora` → `/aluno/pagamentos`. Variant `warning` default.
- `AlunoInadimplenteGate` — client wrapper que faz fetch `pagamentoApi.obterMinhaAssinatura()` on mount, renderiza banner se `status === "Inadimplente"`. Silent-fail em erro.
- Integrado em `(aluno)/layout.tsx`.
- Axios interceptor em `frontend/src/lib/api/client.ts` detecta 403 + `code === "ASSINATURA_INADIMPLENTE"` e dispatch `CustomEvent("forzion:assinatura-inadimplente")` no `window`. `AppLayout` ouve + mostra `<Snackbar><Alert severity="error">` toast 8s. Bridge via CustomEvent mantém client.ts UI-free.

### Recuperação (regularização)
- Aluno acessa `/aluno/pagamentos` (sempre liberado), paga pendente. Webhook `payment_intent.succeeded` → handler chama `RegistrarPagamentoRegularizado` → zera contador + status volta pra Ativa → banner some no próximo load + endpoints liberam.

## CANCELAMENTO / REEMBOLSO (CDC art. 49 — 7 dias)
- **Janela de 7 dias = `DataPagamento` do 1º pagamento Pago, NÃO `DataInicio`** (CR — `DataInicio` é setado em `Criar` no status Pendente, ANTES do pagamento, e `Ativar` não o reseta → contaria do cadastro). A regra de arrependimento conta do recebimento do dinheiro: busca o 1º pagamento `Pago`; se existe e `(agora - pago.DataPagamento).TotalDays ≤ 7` → reembolsa; se não há pagamento pago → sem reembolso.
- **Helper compartilhado `ReembolsoArrependimentoService`** (Application/Services; concreto, sem interface, `AddScoped`): `ReembolsarSeDentroDoPrazoAsync(agora, paymentIntentId, dataPagamento, reverterTransferencia, ct)` centraliza a regra dos 7 dias (do `dataPagamento`) + `CriarReembolsoAsync` em try/catch + `LogCritical`. Os 2 handlers (aluno/treinador) só resolvem o 1º pagamento Pago no repo específico e delegam ao helper (CR — antes a lógica estava duplicada). Aluno passa `reverterTransferencia: true`, treinador `false`.
- `CancelarMinhaAssinaturaAlunoHandler` (Application/UseCases/AssinaturaAlunos) injeta `ReembolsoArrependimentoService` + `IPagamentoRepository` (além de assinaturaRepo/uow/timeProvider/logger). Ordem: `assinatura.Cancelar(agora)` → `CommitAsync` → **só então** o reembolso (CR#1: emitir o refund antes do commit arriscava dinheiro estornado sem cancelamento persistido caso o commit falhasse). Pós-commit: busca `ListarPorAssinaturaAlunoAsync` → 1º `Status==Pago` com `StripePaymentIntentId` ordenado por `CreatedAt` (mais antigo) → delega ao helper com `(pago.StripePaymentIntentId, pago.DataPagamento, reverterTransferencia: true)`.
- **G1 (destination charge):** pagamento do aluno é criado na plataforma com `TransferData.Destination=contaTreinador` + `ApplicationFeeAmount`. Refund simples (só `PaymentIntent`) deixaria o dinheiro no treinador e poderia falhar `balance_insufficient` → por isso `reverterTransferencia: true` ⇒ `ReverseTransfer=true` + `RefundApplicationFee=true`. (Plano do treinador é charge direto-plataforma → refund simples, `false`.)
- **Falha Stripe NÃO bloqueia o cancelamento**: o helper faz `try/catch` → `LogCritical` + prossegue (CDC garante o cancelamento; reembolso manual se Stripe falhar). Fora dos 7 dias do pagamento (ou sem pagamento pago) → cancela sem reembolso.
- Status `Estornado` é **assíncrono**: o refund dispara `charge.refunded` → webhook `MarcarEstornado` → `PagamentoEstornadoEvent`. O handler de cancelamento NÃO muta `Pagamento.Status` síncrono.
- **Treinador self-service (R2)**: `CancelarMinhaAssinaturaTreinadorHandler` (Application/UseCases/Treinadores/CancelarMinhaAssinaturaTreinador) injeta `IAssinaturaTreinadorRepository` + `IVinculoTreinadorAlunoRepository` + `IPagamentoTreinadorRepository` + `ReembolsoArrependimentoService` + uow/timeProvider/logger. Endpoint `POST /treinador/plano/cancelar` (JWT treinador, sem body). Fluxo: (1) `ObterAtualPorTreinadorAsync` → null/Cancelada ⇒ `Error(NotFound)` (404 `assinatura_nao_encontrada`); (2) gate D2 `TemVinculosAtivosAsync(treinadorId)` true ⇒ `AssinaturaTreinadorErrors.OffboardingNecessario` (422 `assinatura_treinador.offboarding_necessario`, sem cascata de cancelamento dos alunos); (3) `assinatura.Cancelar(agora)` → `CommitAsync`; (4) **pós-commit** (espelha CR#1 do aluno): `ObterPagoPorAssinaturaAsync` (1º `Pago` com `StripePaymentIntentId`, ordem `CreatedAt`) → delega ao helper com `(pago.StripePaymentIntentId, pago.DataPagamento, reverterTransferencia: false)` — charge direto-plataforma, **sem** reverse transfer (G1). Falha Stripe = `LogCritical` + prossegue. Resposta 200 = `{ canceladaEm: <DateTime> }`.
- **Gate de offboarding inclui pendentes (CR)**: `TemVinculosAtivosAsync` bloqueia vínculos `Ativo` **e** `AguardandoAprovacao` (antes só `Ativo`, deixando pedidos órfãos ao cancelar). Método usado só pelo cancel do treinador (não compartilhado com anonimização LGPD, que tem gate próprio `conta.offboarding_necessario`).
- **G2 (repo do pagamento Pago)**: `IPagamentoTreinadorRepository` ganhou `ObterPagoPorAssinaturaAsync(assinaturaTreinadorId, ct)` (antes só `ObterPendentePorAssinaturaAsync`); retorna o `PagamentoTreinador` `Status==Pago` mais antigo com `StripePaymentIntentId`.

## CHARGEBACKS / DISPUTAS
- Disputa (chargeback) = aluno (ou plataforma) contesta cobrança junto ao banco do cartão. Stripe envia `charge.dispute.created` ao backend.
- `StripeWebhookParser` extrai `data.object.payment_intent` (lookup) + `data.object.reason` (motivo). Handler `ProcessarWebhookStripeHandler.ProcessarDisputaCriadaAsync` — **roteamento duplo (T4)**:
  1. Carrega `Pagamento` (aluno) por `PaymentIntentId`. Não encontrado → tenta `PagamentoTreinador`; encontrado → `ProcessarDisputaTreinadorAsync` (`PagamentoTreinador.MarcarEmDisputa` + `AssinaturaTreinador.MarcarInadimplentePorDisputa`; sem notificação e-mail). Nenhum dos dois → log warn + JaConsistente.
  2. Idempotência: `Status == EmDisputa` (redelivery) → NÃO re-marca nem comita, e NÃO re-enfileira evidência (a linha outbox `fx:evidencia_disputa` da 1ª disputa já garante entrega+retry; chave única bloquearia) → `JaConsistente`. [Antes (CR#5) re-enviava direto; superseded pelo outbox.]
  3. Guard: `Status != Pago` → log warn + no-op (disputa só faz sentido sobre cobrança capturada). NÃO lança DomainException (Stripe retentaria indefinidamente).
  4. `Pagamento.MarcarEmDisputa(motivo)` — transição `Pago → EmDisputa`, preserva `DataPagamento` (auditoria), dispara `PagamentoEmDisputaEvent(PagamentoId, AssinaturaAlunoId, Valor, MotivoDisputa, OcorridoEm)`.
  5. **Drástico**: carrega `AssinaturaAluno` e chama `MarcarInadimplentePorDisputa(agora)` — força `Ativa → Inadimplente`, equipara `TentativasFalhasConsecutivas` ao threshold como sinalização. NÃO chama `RegistrarPagamentoFalho` (incrementa contador gradual; disputa exige congelamento imediato).
  6. Commit único. Cancelada/Inadimplente/Pendente → no-op idempotente.
- Handlers de `PagamentoEmDisputaEvent`:
  - `PagamentoEmDisputaEmailTreinadorHandler` (Infrastructure/Notifications/Email) — resolve `Assinatura → Treinador → Conta.Email`, envia e-mail **URGENTE** via `EmailTemplates.PagamentoEmDisputa(nomeTreinador, nomeAluno, valor, motivo)`. CTA aponta para `https://dashboard.stripe.com/disputes` (evidência complementar — prints WhatsApp, fichas — só via Dashboard).
- **Resposta automática a chargeback (R9) — VIA OUTBOX**: ao marcar a disputa, o handler deriva o payload (`DerivarPayloadEvidencia{Aluno,Treinador}Async`: resolve email + dataAtivacao — leituras movidas para ANTES do commit) e `enfileirador.Enfileirar("fx:evidencia_disputa", payload, "fx:evidencia_disputa:{aluno|treinador}:{pagamento.Id}")` ANTES do `CommitAsync` → linha outbox persiste atômica com a transição `EmDisputa`. O worker (`EvidenciaDisputaEfeitoHandler`) chama `IStripeService.EnviarEvidenciaDisputaAsync(disputeId, DisputaEvidencia, ct)` = `Dispute.Update(disputeId, { Evidence })` (**disputeId, não chargeId**; overwrite-idempotente) com **retry** — exceção propaga (não engole). Substitui o antigo envio pós-commit best-effort `EnviarEvidenciaDisputaAsync` em try/catch+LogCritical (CR#7) que perdia evidência se o Stripe falhasse. Evidências sem nova coluna (D9): `EmailCliente` (Conta.Email — aluno via `IAlunoRepository → ContaId → Conta`; treinador via `Treinador.ContaId → Conta`), `DataAtivacao` (`assinatura.DataInicio` → `ServiceDate`), `DataUltimoPagamento` (`pagamento.DataPagamento`); `DataUltimaAtividade` null (CR#9). Sem `disputeId` → não enfileira. Detalhe do mecanismo outbox: `specification-backend §3.1`.
  - `PagamentoEmDisputaAlertHandler` (Infrastructure/Notifications/Alerts) — `LogLevel.Critical` com campos estruturados (`PagamentoId`, `AssinaturaAlunoId`, `Valor`, `MotivoDisputa`). Arthur acompanha via agregador de log.
- **Aluno NÃO é notificado**: cliente que abriu disputa já sabe (foi ele que iniciou). Spammar com e-mail redundante adiciona ruído sem valor.
- Treinador responde a disputa em **7-21d** no Stripe Dashboard c/ evidências (entrega de fichas, execuções, prints WhatsApp); sem resposta no prazo → reversão do valor + fee ~US$15. A evidência automática (R9) já submete o básico (email + ServiceDate + datas de uso/pagamento) via `Dispute.Update`; o Dashboard é só pra complementar.
- Reconciliação: `ListarEventosDesdeAsync` inclui `charge.dispute.created` no filtro — disputa nunca passa despercebida se webhook morrer.
- Pós-disputa: se treinador ganha (evidências aceitas), valor permanece, mas estado `Pagamento.EmDisputa` é terminal (sem auto-volta para Pago). Operação de "fechar disputa" deve ser feita manualmente no admin se for relevante para histórico — não há fluxo automatizado por enquanto.

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
- Sem cleanup proativo de pagamentos zombie (pendente sem StripePaymentIntentId após crash). `GerarCobrancaMensal`/`IniciarPagamentoPlano` re-uso de pendente lida com isso: marca Falhou + cria novo na próxima tentativa (idempotente via tx serializable).
- Test/Live + WebhookSecret por-endpoint: ver §Stripe Test Mode vs Live Mode (canônico).
