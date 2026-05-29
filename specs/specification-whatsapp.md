# specification-whatsapp — subsistema de WhatsApp (forzion.tech)

DOC PARA AGENTES. Fonte de verdade das notificações WhatsApp (Meta Cloud API). Formato denso, agent-oriented. Consultar antes de alterar envio de WhatsApp, gate Null/real, config Meta, handlers/templates, decorator de ambiente, webhook de status ou paridade com e-mail. Custo/modelo de cobrança Meta vive AQUI (não em [specification-stripe]). Tabela `whatsapp_delivery_logs` + `telefone` (alunos/treinadores) → [specification-db], não duplicar.

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA de mudança em: provedor, gate Null/real, chaves de config, payload Meta (text/template), handlers/notifiers/templates, decorator, webhook, paridade email→WhatsApp.
- Vive em `specs/` (versionado; commitar). NÃO `.specs/`.
- Mudança de tabela → [specification-db]. Novo domain event → [specification-model]. Handler de e-mail correlato → [specification-email].

## STACK & GATE
- Provedor: **Meta WhatsApp Cloud API** (Graph API), integração DIRETA (sem BSP → sem markup).
- `IWhatsAppNotifier` (Application/Interfaces):
  - `bool Habilitado` — true no Meta, false no Null (simetria com `IEmailService`; handlers gateiam cedo).
  - `Task SendAsync(phone, message, ct)` — texto livre (sessão; só entrega dentro da janela 24h).
  - `Task SendTemplateAsync(phone, WhatsAppTemplateMessage msg, ct)` — **template aprovado** (entrega fora da janela). Caminho padrão das notificações business-initiated.
- `WhatsAppTemplateMessage` (record, Application/Interfaces): `Name`, `IReadOnlyList<string> BodyParameters` (posicionais `{{1}}`...), `LanguageCode="pt_BR"`.
- Impls (Infrastructure/Notifications/WhatsApp):
  - `MetaWhatsAppCloudNotifier` — `Habilitado=true`. POST `messages` (Bearer). `SendTemplateAsync` monta payload `type:template` (components body). `SendAsync` monta `type:text`. Telefone normalizado por `PhoneNumberNormalizer` (null → skip + warn). Falha HTTP/exceção = LOG only, sem rethrow.
  - `NullWhatsAppNotifier` — `Habilitado=false`; no-op (warn no ctor, debug por mensagem).
  - `EnvironmentWhatsAppDecorator` — embrulha o real. `Habilitado` delega. Em não-prod (`WhatsAppSettings.MarcarComoTeste`) aplica redirect/allowlist de telefone; em prod passthrough.
- GATE (DI, `InfrastructureExtensions`): `WhatsApp:PhoneNumberId` E `WhatsApp:AccessToken` não-vazios → `MetaWhatsAppCloudNotifier` (typed HttpClient, base `graph.facebook.com/{ApiVersion}/{PhoneNumberId}/`, timeout 15s); senão → `NullWhatsAppNotifier`. **Sempre** embrulhado no `EnvironmentWhatsAppDecorator` (helper `EnvolverComWhatsAppDecorator`, análogo ao e-mail).
- Handlers de domain event registrados manual no DI; despachados no `UnitOfWork.CommitAsync`.

## CONFIG (chaves)
| Chave | Onde lida | Função | Ausente |
|-------|-----------|--------|---------|
| `WhatsApp:PhoneNumberId` | `InfrastructureExtensions` (gate) | seleciona Meta real; compõe BaseAddress | → Null |
| `WhatsApp:AccessToken` | idem | Bearer token Meta | → Null |
| `WhatsApp:ApiVersion` | idem | versão Graph API | default `v21.0` |
| `WhatsApp:AppSecret` | `WebhookEndpoints` (POST) | HMAC SHA256 do webhook (`X-Hub-Signature-256`) | webhook POST → 400 |
| `WhatsApp:WebhookVerifyToken` | `WebhookEndpoints` (GET) | handshake de verificação Meta (`hub.verify_token`) | GET → 403 |
| `WhatsApp:MarcarComoTeste` | `WhatsAppSettings` → decorator | liga redirect/allowlist (não-prod) | default `false` (prod passthrough) |
| `WhatsApp:RedirecionarDestinatariosPara` | idem | CSV telefones E.164; redireciona p/ 1º alvo | vazio → sem redirect |
| `WhatsApp:AllowlistTelefones` | idem | CSV telefones isentos de redirect | vazio → ninguém isento |
| `App:FrontendBaseUrl` | `AppSettings` | base dos links nas mensagens | default vazio |

- `WhatsAppSettings` (Application/Settings) bind seção `WhatsApp`. Defaults prod-safe.
- Deploy: env vars no compose (`WhatsApp__*`, hoje comentadas; descomentar + setar no `/opt/forzion/.env`). Local: User Secrets.

## COMPONENTES
- `MetaWhatsAppCloudNotifier` / `NullWhatsAppNotifier` / `EnvironmentWhatsAppDecorator`.
- `PhoneNumberNormalizer` (static) — E.164 dígitos sem `+`, DDI BR `55` default; null/inválido → null.
- `WhatsAppTemplates` (static, catálogo central — análogo a `EmailTemplates`) — 15 factories → `WhatsAppTemplateMessage`. Money pt-BR. **Templates NÃO mais hardcoded nos handlers.**
- Handlers de domain event (Infrastructure/Notifications/WhatsApp) — ver FLUXOS. Padrão: `if(!whatsAppNotifier.Habilitado) return;` → resolve destinatário (repo) → check telefone null (skip) → `SendTemplateAsync(WhatsAppTemplates.X(...))`.
- Webhook: `WhatsAppDeliveryLog` (Domain) + `IWhatsAppDeliveryLogRepository` + `ProcessarWebhookWhatsAppHandler` (Infrastructure).

## FLUXOS — notificações WhatsApp (template messages)

### Aluno
| Evento | Handler | Template |
|--------|---------|----------|
| `PagamentoCriadoEvent` | `PagamentoCriadoWhatsAppNotifierHandler` | `cobranca_disponivel` |
| `PagamentoFalhouEvent` | `PagamentoFalhouWhatsAppNotifierHandler` | `cobranca_falhou` — **toda tentativa** (paridade; skip `<2` removido) |
| `PagamentoEstornadoEvent` | `PagamentoEstornadoWhatsAppNotifierHandler` | `pagamento_estornado` |
| `AssinaturaAlunoMarcadaInadimplenteEvent` | `...InadimplenteWhatsAppNotifierHandler` | `assinatura_inadimplente` |
| `AssinaturaAlunoReativadaEvent` | `AssinaturaAlunoReativadaWhatsAppHandler` | `assinatura_reativada` (regularização pós-inadimplência) |
| `AssinaturaAlunoCanceladaEvent` | `AssinaturaAlunoCanceladaWhatsAppAlunoHandler` | `assinatura_cancelada` |
| `AlunoRegistradoEvent` | `AlunoRegistradoWhatsAppHandler` | `bem_vindo_aluno` |
| `AssinaturaAlunoCriadaEvent` | `AssinaturaAlunoCriadaWhatsAppHandler` | `assinatura_criada` |
| `AlunoInativadoEvent` | `AlunoInativadoWhatsAppHandler` | `aluno_inativado` |
| `VinculoAprovadoEvent` | `VinculoAprovadoWhatsAppHandler` | `vinculo_aprovado` (era chamada direta em AprovarVinculoHandler) |

### Treinador
| Evento | Handler | Template |
|--------|---------|----------|
| `TreinadorAprovadoEvent` | `TreinadorAprovadoWhatsAppHandler` | `treinador_aprovado` |
| `TreinadorReprovadoEvent` | `TreinadorReprovadoWhatsAppHandler` | `treinador_reprovado` |
| `TreinadorInativadoEvent` | `TreinadorInativadoWhatsAppHandler` | `treinador_inativado` |
| `AssinaturaAlunoCanceladaEvent` | `AssinaturaAlunoCanceladaWhatsAppTreinadorHandler` | `aluno_cancelou_assinatura` |
| `PagamentoEmDisputaEvent` | `PagamentoEmDisputaWhatsAppTreinadorHandler` | `pagamento_em_disputa` |
| `VinculoPendenteCriadoEvent` | `VinculoPendenteCriadoWhatsAppHandler` | `novo_aluno_pendente` (era chamada direta em RegistrarAlunoHandler) |

- **Arquitetura padronizada**: TODAS as notificações são event-handlers (chamadas diretas em `AprovarVinculo`/`RegistrarAluno` removidas). `VinculoPendenteCriadoEvent` (novo) é emitido em `VinculoTreinadorAluno.Criar` (status AguardandoAprovacao) → ver [specification-model].
- `VinculoPendenteCriadoEvent` tem TAMBÉM handler de e-mail p/ treinador (`VinculoPendenteCriadoEmailTreinadorHandler`) → fecha o gap reverso. Ver [specification-email].
- Destinatário: `aluno.Telefone` / `treinador.Telefone`. **Sem fallback** (`contas` não tem telefone) → telefone null = skip silencioso.
- **Gating por tier** (`IPlanoNotificationPolicy`): handlers **OPERACIONAIS** (pagamento criado/falhou/estornado/disputa, assinatura criada/cancelada×2, vínculo aprovado/pendente, inadimplência, aluno-inativado) checam `canais.WhatsApp` antes de enviar — WhatsApp só enviado se treinador tem tier≥ProPlus. **UNGATED** (sempre enviam, sem consulta a plano): `TreinadorAprovadoWhatsAppHandler`, `TreinadorReprovadoWhatsAppHandler`, `TreinadorInativadoWhatsAppHandler`, `AlunoRegistradoWhatsAppHandler` (bem-vindo). Cross-ref `TierPlanoExtensions` [specification-model], `IPlanoNotificationPolicy` [specification-backend].

### Webhook de status (Meta → app)
- GET `/webhooks/whatsapp`: handshake. `hub.mode=subscribe` + `hub.verify_token == WhatsApp:WebhookVerifyToken` → retorna `hub.challenge` (200). Senão 403.
- POST `/webhooks/whatsapp`: verifica `X-Hub-Signature-256` = `sha256=` + HMAC-SHA256(raw body, `WhatsApp:AppSecret`) (constant-time). Parse `entry[].changes[].value.statuses[]` (id/status/recipient_id/timestamp). Idempotência por (`meta_message_id`, `event_type`). Persiste em `whatsapp_delivery_logs`. Payloads sem `statuses` (ex.: msgs inbound) → 200 ignorado. Sem AppSecret → 400; assinatura inválida → 400.
- Endpoint em `Api/Endpoints/Pagamentos/WebhookEndpoints.cs` (AllowAnonymous + rate-limit "webhook"). nginx já roteia `/webhooks/` → backend (ver [specification-email]).

## SEPARAÇÃO POR AMBIENTE
- `EnvironmentWhatsAppDecorator` (análogo a `EnvironmentEmailDecorator`). Discriminador `WhatsAppSettings.MarcarComoTeste`: `false` (prod) → passthrough; `true` (não-prod) → `ResolverDestinatario`: se `RedirecionarDestinatariosPara` vazio OU telefone ∈ `AllowlistTelefones` (comparação só-dígitos) → mantém; senão redireciona p/ 1º alvo + log. Aplica a text e template.
- ⚠️ Meta Cloud API não tem sandbox de entrega real; alternativa adicional = usar test number Meta (limitado a destinatários pré-cadastrados) no AccessToken de hmg.

## CUSTO & MODELO META (per-message)
- Desde 01/07/2025: por mensagem, cobra quando *template* é entregue. Categorias marketing / utility / authentication / service. Utility grátis na janela 24h; service grátis.
- Notifier usa `type:template` → entrega **fora da janela** (corrigido vs `type:text` anterior). ⚠️ **DEPENDÊNCIA EXTERNA**: cada template (15 nomes abaixo) precisa ser **criado e aprovado no Meta Business Manager** (categoria utility/auth, idioma pt_BR, body com variáveis posicionais na ordem do catálogo). Sem aprovação, a Meta rejeita o envio. Ação manual de ops — não automatizável via código.
- Templates (snake_case): aluno `cobranca_disponivel`,`cobranca_falhou`,`pagamento_estornado`,`assinatura_inadimplente`,`assinatura_cancelada`,`bem_vindo_aluno`,`assinatura_criada`,`aluno_inativado`,`vinculo_aprovado`; treinador `treinador_aprovado`,`treinador_reprovado`,`treinador_inativado`,`aluno_cancelou_assinatura`,`pagamento_em_disputa`,`novo_aluno_pendente`.
- Brasil base (USD/msg, ~2026): marketing ~$0.0625 · auth ~$0.0315 · utility menor + volume. Confirmar rate card no WhatsApp Manager.

## SEGURANÇA
- `AccessToken`/`PhoneNumberId`/`AppSecret`/`WebhookVerifyToken` backend-only (env/secret), nunca frontend.
- `PhoneNumberNormalizer` aplica E.164 (DDI BR 55). ⚠️ Ainda SEM fallback de telefone: `contas` não tem coluna `telefone` (só `alunos`/`treinadores`) → telefone ausente = no-op. Fallback exigiria `contas.telefone` (migration; fora deste escopo).
- Webhook: HMAC SHA256 obrigatório + handshake; payload raw auditável em `whatsapp_delivery_logs`.
- Envio: falha = log only (sem rethrow) → não vaza p/ usuário, não quebra transação.

## TESTES
- Unit (xUnit, sem Docker): `MetaWhatsAppCloudNotifierTests`, `NullWhatsAppNotifierTests`, `ProcessarWebhookWhatsAppHandlerTests`, `WhatsAppDeliveryLogTests`, e handler tests dos 5 adaptados + 10 novos (Infrastructure/Notifications/WhatsApp/ e Email/). Cobrem: Habilitado false no-op, entidade não encontrada, telefone null, happy path (assert template Name + body params).
- Suite não-integração: 1481 testes verdes. E2E/Infra (Testcontainers) exigem Docker → CI.

## GAPS / ROADMAP — status
Paridade email→WhatsApp **FECHADA** (todos os eventos de e-mail relevantes têm WhatsApp; verify/reset e health-report admin permanecem N/A por serem channel-bound/internos). Itens estruturais FECHADOS: type:template, `EnvironmentWhatsAppDecorator`, padronização event-handlers, `Habilitado`, catálogo central, E.164, webhook de status.

Pendências (NÃO de código):
- **Aprovar os 15 templates no Meta Business Manager** (ops) — pré-requisito de entrega real.
- **Fallback de telefone**: requer coluna `contas.telefone` (migration + UI cadastro) — escopo separado se desejado.

## DICAS / GOTCHAS
- Sem warning "Serviço de WhatsApp não configurado" no startup = real ativo (Null loga no ctor).
- Mensagem some sem erro? (1) telefone null → skip debug; (2) template não aprovado/idioma errado na Meta → `LogWarning` com body Meta; (3) gate caiu p/ Null.
- Webhook não chega? Conferir `WebhookVerifyToken` (handshake) e `AppSecret` (assinatura) + rota nginx `/webhooks/`.
- Referências: [specification-model] (evento `VinculoPendenteCriadoEvent`), [specification-backend] (dispatch), [specification-email] (paralelo + handler reverso), [specification-stripe] (eventos pagamento), [specification-db] (`whatsapp_delivery_logs`, `telefone`).
