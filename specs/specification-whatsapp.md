# specification-whatsapp — subsistema de WhatsApp (forzion.tech)

DOC PARA AGENTES. Fonte de verdade das notificações WhatsApp (Meta Cloud API). Formato denso, agent-oriented. Consultar antes de alterar envio de WhatsApp, gate Null/real, config Meta, handlers/templates, decorator de ambiente, webhook de status ou paridade com e-mail. Custo/modelo de cobrança Meta vive AQUI (não em [specification-stripe]). Tabela `whatsapp_delivery_logs` + `telefone` (alunos/treinadores) → [specification-db], não duplicar.

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA de mudança em: provedor, gate Null/real, chaves de config, payload Meta (text/template), handlers/notifiers/templates, decorator, webhook, paridade email→WhatsApp.
- Mudança de tabela → [specification-db]. Novo domain event → [specification-model]. Handler de e-mail correlato → [specification-email].

## STACK & GATE
- Meta Cloud API (ver AGENTS.md STACK): detalhe = Graph API, integração DIRETA sem BSP (sem markup).
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
- Deploy: env vars no compose. `docker-compose.homolog.yml` mapeia `WhatsApp__*` ← `${WHATSAPP_*}` (PhoneNumberId, AccessToken, ApiVersion, AppSecret, WebhookVerifyToken, MarcarComoTeste [default `true` em hmg], RedirecionarDestinatariosPara, AllowlistTelefones) → setar no `/opt/forzion/.env`. `docker-compose.yml` (local dev) mantém só PhoneNumberId/AccessToken/ApiVersion comentadas. Local sem Docker: User Secrets. Chaves documentadas em `.env.example`.

## COMPONENTES
- `MetaWhatsAppCloudNotifier` / `NullWhatsAppNotifier` / `EnvironmentWhatsAppDecorator`.
- `PhoneNumberNormalizer` (static) — E.164 dígitos sem `+`, DDI BR `55` default; null/inválido → null.
- `WhatsAppTemplates` (static, catálogo central — análogo a `EmailTemplates`) — 16 factories → `WhatsAppTemplateMessage`. Money pt-BR. **Templates NÃO mais hardcoded nos handlers.**
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
- **Gating por tier** (`IPlanoNotificationPolicy`): WhatsApp é PAGO (Meta cobra por template entregue) → **TODOS** os handlers de notificação checam `canais.WhatsApp` antes de enviar; só envia se treinador tem tier≥ProPlus. Inclui operacionais (pagamento criado/falhou/estornado/disputa, assinatura criada/cancelada×2, vínculo aprovado/pendente, inadimplência, aluno-inativado) **E** ciclo de vida do treinador (`TreinadorAprovadoWhatsAppHandler`/`Reprovado`/`Inativado`, gate por `ResolverPorTreinadorAsync(domainEvent.TreinadorId)` após o check de telefone) **E** bem-vindo do aluno (`AlunoRegistradoWhatsAppHandler`). Ordem do gate: `Habilitado → resolve destinatário → telefone null=skip → gate tier → SendTemplateAsync` (gate DEPOIS do telefone, não consulta plano sem destinatário). **NÃO há mais handler UNGATED no WhatsApp.** Cross-ref `TierPlanoExtensions` [specification-model], `IPlanoNotificationPolicy` [specification-backend].
- **Bem-vindo do aluno — resolução do tier via vínculo pendente**: `AlunoRegistradoEvent` é emitido em `Aluno.Criar` (antes do vínculo existir) e NÃO carrega `TreinadorId`. O handler roda no `CommitAsync`, quando o vínculo `AguardandoAprovacao` já foi persistido no mesmo UoW → resolve o treinador via `IVinculoTreinadorAlunoRepository.ObterPendentePorAlunoAsync(alunoId)` → `ResolverPorTreinadorAsync(vinculo.TreinadorId)`. Vínculo null (não esperado — aluno sempre nasce com vínculo) → skip defensivo + `LogDebug`. NÃO usa `ResolverPorAlunoAsync` (semântica compartilhada com aluno-inativado/inadimplente) nem estende o evento. **Assimetria intencional vs e-mail**: o e-mail correlato (ciclo de vida do treinador + bem-vindo) permanece UNGATED (grátis) → ver [specification-email] §Gating-por-tier. Aluno/treinador de tier baixo recebe o e-mail, não o WhatsApp (sem gap de comunicação).
- **Gating por MODO DE PAGAMENTO (Externo)**: ortogonal ao gating por tier; idêntico ao e-mail. `ModoPagamentoAluno==Externo` ⇒ nenhuma `AssinaturaAluno`/`Pagamento` criada (gate no PONTO DE CRIAÇÃO) ⇒ eventos de billing nunca emitidos ⇒ notifiers WhatsApp de pagamento/assinatura estruturalmente não disparam. Não afeta bem-vindo/vínculo/ciclo de conta. Mecanismo canônico + lista de eventos + teste: [specification-email] §Gating-MODO (par WhatsApp).
- **Verificação de conta adiada no cadastro PAGO**: o WhatsApp não tem fluxo de verificação de conta (verify é channel-bound a e-mail — ver § GAPS). Mas a regra correlata existe no e-mail: treinador plano pago só recebe a verificação após `payment_intent.succeeded`/`plano_treinador` (`ContaRegistradaEvent` adiado p/ `FinalizarCadastroAsync`). Detalhe em [specification-email] §Verificação; pagamento em [specification-stripe].

### Webhook de status (Meta → app)
- GET `/webhooks/whatsapp`: handshake. `hub.mode=subscribe` + `hub.verify_token == WhatsApp:WebhookVerifyToken` → retorna `hub.challenge` (200). Senão 403.
- POST `/webhooks/whatsapp`: verifica `X-Hub-Signature-256` = `sha256=` + HMAC-SHA256(raw body, `WhatsApp:AppSecret`) (constant-time). Parse `entry[].changes[].value.statuses[]` (id/status/recipient_id/timestamp). Idempotência por (`meta_message_id`, `event_type`). Persiste em `whatsapp_delivery_logs`. Payloads sem `statuses` (ex.: msgs inbound) → 200 ignorado. Sem AppSecret → 400; assinatura inválida → 400.
- Endpoint em `Api/Endpoints/Pagamentos/WebhookEndpoints.cs` (AllowAnonymous + rate-limit "webhook"). nginx já roteia `/webhooks/` → backend (ver [specification-email]).

## SEPARAÇÃO POR AMBIENTE
- `EnvironmentWhatsAppDecorator` = mesmo mecanismo de `EnvironmentEmailDecorator` (discriminador `MarcarComoTeste`, `ResolverDestinatario`, redirect/allowlist) — ver [specification-email] §SEPARAÇÃO. **Deltas**: alvo é telefone (não e-mail); `AllowlistTelefones` compara **só-dígitos**; aplica a text E template. Sem banner (só redirect).
- ⚠️ Meta Cloud API não tem sandbox de entrega real; alternativa = test number Meta (destinatários pré-cadastrados) no AccessToken de hmg.

## CUSTO & MODELO META (per-message)
- Desde 01/07/2025: por mensagem, cobra quando *template* é entregue. Categorias marketing / utility / authentication / service. Utility grátis na janela 24h; service grátis.
- Notifier usa `type:template` → entrega **fora da janela** (corrigido vs `type:text` anterior). ⚠️ **DEPENDÊNCIA EXTERNA**: cada template (16 nomes abaixo) precisa ser **criado e aprovado no Meta Business Manager** (ação manual de ops, não automatizável via código). Regra de aprovação/re-submissão única em §TEMPLATES.
- Os 16 templates (nomes + copy + ordem de vars) no §TEMPLATES — COPY pt_BR.
- Brasil base (USD/msg, valores ilustrativos ~2026): marketing $0.0625 · auth $0.0315 · utility menor + volume. Confirmar rate card atual no WhatsApp Manager.

## TEMPLATES — COPY pt_BR P/ APROVAÇÃO META
Fonte das vars: `WhatsAppTemplates.cs` (ordem dos `{{n}}` = ordem do array, IMUTÁVEL — `SendTemplateAsync` envia N params posicionais; divergir qtd/ordem → Meta rejeita). Todos **categoria Utility**, idioma **Português (BR) / `pt_BR`**, sem header/botões. ⚠️ `Money()` devolve só o número (`149,90`, sem `R$`) → texto inclui `R$ ` antes da var. `metodo` já vem palavra (`cartão de crédito`/`Pix`). ⚠️ A copy vive SÓ no painel Meta + nesta tabela — `WhatsAppTemplates.cs` é factory de `(name, params[])`, NÃO contém texto.
- **Regra de aprovação/re-submissão (CANÔNICA)**: cada template precisa ser criado e aprovado no Meta Business Manager (utility/auth, pt_BR, vars posicionais na ordem do catálogo) antes do primeiro envio — sem aprovação a Meta rejeita. Copy é editável; **qtd/ordem de vars NÃO**. Editar a copy de um template já aprovado (ex.: #7 `bem_vindo_aluno`) exige **re-submeter na Meta** (re-aprovação manual de ops; qtd/ordem de vars inalteradas). Gotchas Meta: template não pode ser só-variável nem var colada no início/fim sem texto (bodies abaixo ok); evitar vars fora de ordem sequencial no texto (template 15 reescrito p/ {{2}} antes de {{3}}).

| # | template | vars (ordem) | body proposto | samples |
|---|----------|--------------|---------------|---------|
| 1 | `cobranca_disponivel` | nome, valor, método, link | `Olá {{1}}! Sua cobrança de R$ {{2}} já está disponível para pagamento via {{3}}. Acesse: {{4}}` | João · 149,90 · Pix · https://forzion.tech/portal |
| 2 | `cobranca_falhou` | nome, valor, tentativas, link | `Olá {{1}}, não conseguimos processar seu pagamento de R$ {{2}} (tentativa {{3}}). Regularize em: {{4}}` | João · 149,90 · 2 · https://forzion.tech/portal |
| 3 | `pagamento_estornado` | nome, valor, link | `Olá {{1}}, seu pagamento de R$ {{2}} foi estornado. Detalhes em: {{3}}` | João · 149,90 · https://forzion.tech/portal |
| 4 | `assinatura_inadimplente` | nome, link | `Olá {{1}}, sua assinatura está com pagamento em atraso e foi marcada como inadimplente. Regularize para manter o acesso: {{2}}` | João · https://forzion.tech/portal |
| 5 | `assinatura_cancelada` | nome, link | `Olá {{1}}, sua assinatura foi cancelada. Caso queira reativar, acesse: {{2}}` | João · https://forzion.tech/portal |
| 6 | `assinatura_reativada` | nome, link | `Olá {{1}}, sua assinatura foi reativada e seu acesso está liberado novamente. Acesse: {{2}}` | João · https://forzion.tech/portal |
| 7 | `bem_vindo_aluno` | nome | `Bem-vindo(a) à forzion.tech, {{1}}! Seu cadastro foi realizado com sucesso. Aguarde a aprovação do seu treinador para acessar seus treinos.` | João |
| 8 | `assinatura_criada` | nome, pacote, valor | `Olá {{1}}, sua assinatura do pacote {{2}} no valor de R$ {{3}} foi criada com sucesso.` | João · Plano Mensal · 149,90 |
| 9 | `aluno_inativado` | nome | `Olá {{1}}, sua conta de aluno na forzion.tech foi inativada. Em caso de dúvida, fale com seu treinador.` | João |
| 10 | `vinculo_aprovado` | nome | `Olá {{1}}, seu vínculo com o treinador foi aprovado. Você já pode acessar seus treinos na forzion.tech!` | João |
| 11 | `treinador_aprovado` | nomeTreinador | `Olá {{1}}, seu cadastro de treinador na forzion.tech foi aprovado. Bem-vindo(a)!` | Carlos |
| 12 | `treinador_reprovado` | nomeTreinador | `Olá {{1}}, seu cadastro de treinador na forzion.tech não foi aprovado neste momento.` | Carlos |
| 13 | `treinador_inativado` | nomeTreinador | `Olá {{1}}, sua conta de treinador na forzion.tech foi inativada.` | Carlos |
| 14 | `aluno_cancelou_assinatura` | nomeTreinador, nomeAluno, valor | `Olá {{1}}, o aluno {{2}} cancelou a assinatura de R$ {{3}}.` | Carlos · João · 149,90 |
| 15 | `pagamento_em_disputa` | nomeTreinador, nomeAluno, valor | `Olá {{1}}, o pagamento do aluno {{2}} no valor de R$ {{3}} entrou em disputa. Acompanhe o caso.` | Carlos · João · 149,90 |
| 16 | `novo_aluno_pendente` | nomeTreinador, nomeAluno | `Olá {{1}}, o aluno {{2}} solicitou vínculo e está aguardando sua aprovação na forzion.tech.` | Carlos · João |

## SEGURANÇA
- `AccessToken`/`PhoneNumberId`/`AppSecret`/`WebhookVerifyToken` backend-only (env/secret), nunca frontend.
- `PhoneNumberNormalizer` aplica E.164 (DDI BR 55). ⚠️ Ainda SEM fallback de telefone: `contas` não tem coluna `telefone` (só `alunos`/`treinadores`) → telefone ausente = no-op. Fallback exigiria `contas.telefone` (migration; fora deste escopo).
- Webhook: HMAC SHA256 obrigatório + handshake; payload raw auditável em `whatsapp_delivery_logs`.
- Envio: falha = log only (sem rethrow) → não vaza p/ usuário, não quebra transação.

## TESTES
- Unit (xUnit, sem Docker): `MetaWhatsAppCloudNotifierTests`, `NullWhatsAppNotifierTests`, `ProcessarWebhookWhatsAppHandlerTests`, `WhatsAppDeliveryLogTests`, e handler tests dos 5 adaptados + 10 novos (Infrastructure/Notifications/WhatsApp/ e Email/). Cobrem: Habilitado false no-op, entidade não encontrada, telefone null, happy path (assert template Name + body params).
- Suíte não-integração: verde. E2E/Infra (Testcontainers) exigem Docker → CI.

## GAPS / ROADMAP — status
Paridade email→WhatsApp **FECHADA** (todos os eventos de e-mail relevantes têm WhatsApp; verify/reset e health-report admin permanecem N/A por serem channel-bound/internos). Itens estruturais FECHADOS: type:template, `EnvironmentWhatsAppDecorator`, padronização event-handlers, `Habilitado`, catálogo central, E.164, webhook de status.

Pendências (NÃO de código):
- **Aprovar os 16 templates no Meta Business Manager** (ops) — pré-requisito de entrega real. Copy pronta em § TEMPLATES — COPY pt_BR.
- **Fallback de telefone**: requer coluna `contas.telefone` (migration + UI cadastro) — escopo separado se desejado.

## DICAS / GOTCHAS
- Gate Null/real: sem warning no startup = real ativo (Null loga no ctor). Simétrico ao e-mail [specification-email].
- Mensagem some sem erro? (1) telefone null → skip debug; (2) template não aprovado/idioma errado na Meta → `LogWarning` com body Meta; (3) gate caiu p/ Null.
- Webhook não chega? Conferir `WebhookVerifyToken` (handshake) e `AppSecret` (assinatura) + rota nginx `/webhooks/`.
- Referências: [specification-model] (evento `VinculoPendenteCriadoEvent`), [specification-backend] (dispatch), [specification-email] (paralelo + handler reverso), [specification-stripe] (eventos pagamento), [specification-db] (`whatsapp_delivery_logs`, `telefone`).
