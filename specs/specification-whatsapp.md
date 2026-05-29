# specification-whatsapp — subsistema de WhatsApp (forzion.tech)

DOC PARA AGENTES. Fonte de verdade das notificações WhatsApp (Meta Cloud API). Formato denso, agent-oriented. Consultar antes de alterar envio de WhatsApp, gate Null/real, config Meta, handlers/templates ou paridade com e-mail. Custo/modelo de cobrança Meta vive AQUI (não em [specification-stripe]). Estrutura de tabelas não se aplica (WhatsApp não persiste; destinatário vem de `alunos.telefone`/`treinadores.telefone` → [specification-db], não duplicar).

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA de qualquer mudança relevante em: provedor, gate Null/real, chaves de config, payload Meta (text↔template), handlers/notifiers WhatsApp, templates de mensagem, fluxos, paridade email→WhatsApp.
- Vive em `specs/` (versionado; commitar). NÃO confundir com `.specs/` (gitignorado).
- Mudança de paridade/cobertura → atualizar a seção GAPS aqui + cruzar com [specification-email] (FLUXOS).
- Mudança de tabela (`telefone`) → [specification-db], não aqui.

## STACK & GATE
- Provedor: **Meta WhatsApp Cloud API** (Graph API), integração DIRETA (sem BSP → sem markup de intermediário).
- `IWhatsAppNotifier` (Application/Interfaces): `Task SendAsync(string phoneNumber, string message, CancellationToken ct)`.
  - ⚠️ **Assimetria vs e-mail**: NÃO tem `Habilitado` (que `IEmailService` tem). Handlers NÃO gateiam por flag — confiam no `NullWhatsAppNotifier` (no-op) + check de `telefone` null. Ver GAPS.
- Impls (Infrastructure/Notifications/WhatsApp):
  - `MetaWhatsAppCloudNotifier` — typed HttpClient. POST `messages` (relativo à base). Payload `{ messaging_product:"whatsapp", recipient_type:"individual", to:<phone>, type:"text", text:{preview_url:false, body:message} }`. Telefone sanitizado: remove `+ - espaço ( )`. Falha HTTP → `LogWarning(status, body)`; `HttpRequestException` → `LogError`. **Sem rethrow** (não quebra fluxo de negócio — igual e-mail).
  - `NullWhatsAppNotifier` — no-op; `LogWarning` no ctor ("Serviço de WhatsApp não configurado..."), `LogDebug` por mensagem ignorada.
- GATE (DI, `InfrastructureExtensions` L168-186): `WhatsApp:PhoneNumberId` E `WhatsApp:AccessToken` ambos não-vazios → `AddHttpClient<MetaWhatsAppCloudNotifier>` (BaseAddress `https://graph.facebook.com/{ApiVersion}/{PhoneNumberId}/`, header `Bearer {AccessToken}`, timeout 15s) + `AddScoped<IWhatsAppNotifier, MetaWhatsAppCloudNotifier>`; senão → `AddScoped<IWhatsAppNotifier, NullWhatsAppNotifier>`.
- Handlers de domain event registrados manual em `InfrastructureExtensions` L158-162; despachados no `UnitOfWork.CommitAsync` (re-entrância tratada — ver [specification-backend]).

## CONFIG (chaves)
| Chave | Onde lida | Função | Ausente |
|-------|-----------|--------|---------|
| `WhatsApp:PhoneNumberId` | `InfrastructureExtensions` (gate) | seleciona Meta real; compõe BaseAddress | → NullWhatsAppNotifier |
| `WhatsApp:AccessToken` | idem | Bearer token Meta | → NullWhatsAppNotifier |
| `WhatsApp:ApiVersion` | idem | versão Graph API na BaseAddress | default `v21.0` |
| `App:FrontendBaseUrl` | `AppSettings` (bound DI) | base dos links nas mensagens (`/aluno/pagamentos`) | default vazio (links quebram) |

- Gate exige AMBOS PhoneNumberId+AccessToken; faltando qualquer um → Null (silencioso).
- Deploy: env vars no compose (hoje COMENTADAS, integração opcional). `docker-compose.yml` L42-45 / `.env.example` L43-46: `WhatsApp__PhoneNumberId` ← `${WHATSAPP_PHONE_NUMBER_ID}`, `WhatsApp__AccessToken` ← `${WHATSAPP_ACCESS_TOKEN}`, `WhatsApp__ApiVersion` ← `${WHATSAPP_API_VERSION:-v21.0}`. Descomentar + setar no `/opt/forzion/.env` da VM p/ ativar.
- Local (dev): User Secrets (`dotnet user-secrets set "WhatsApp:AccessToken" ...`). Sem secrets → Null roda.
- ⚠️ AccessToken Meta é permanente (System User token) — backend-only, nunca frontend.

## COMPONENTES
- `MetaWhatsAppCloudNotifier` / `NullWhatsAppNotifier` (acima).
- Handlers de domain event (Infrastructure/Notifications/WhatsApp) — 5 (ver FLUXOS). Cada um: resolve destinatário → checa `telefone` null (skip silencioso via `LogDebug`) → monta mensagem hardcoded → `SendAsync`.
- 2 chamadas DIRETAS de `SendAsync` dentro de handlers da Application (NÃO event-based) — ver FLUXOS. Inconsistência arquitetural; ver GAPS.
- ⚠️ **Templates hardcoded** em cada handler/call-site. NÃO existe catálogo central tipo `EmailTemplates`. Valores monetários: `CultureInfo "pt-BR"`, `"N2"` → `R$ 149,90`. Links sempre p/ portal forzion (`App:FrontendBaseUrl`), NUNCA Stripe.

## FLUXOS — notificações WhatsApp atuais (7)

### Event-based — destinatário Aluno (5; Infrastructure/Notifications/WhatsApp)
| Evento | Handler | Resolução | Condição | Mensagem (resumo) |
|--------|---------|-----------|----------|-------------------|
| `PagamentoCriadoEvent` | `PagamentoCriadoWhatsAppNotifierHandler` | assinatura→aluno | telefone presente | "Nova cobrança disponível. Valor R$ X. Método cartão/Pix. Acesse {portal}/aluno/pagamentos" |
| `PagamentoFalhouEvent` | `PagamentoFalhouWhatsAppNotifierHandler` | assinatura→aluno | **`TentativasFalhasConsecutivas >= 2`** (pula 1ª) + telefone | tom progressivo: 2ª="Segunda tentativa falhou..."; 3+="Última tentativa antes do bloqueio..." |
| `PagamentoEstornadoEvent` | `PagamentoEstornadoWhatsAppNotifierHandler` | assinatura→aluno | telefone | "Cobrança estornada. Valor R$ X. Devolução até 10 dias úteis." |
| `AssinaturaAlunoMarcadaInadimplenteEvent` | `AssinaturaAlunoMarcadaInadimplenteWhatsAppNotifierHandler` | `AlunoId` direto do evento | telefone | URGENTE: "Conta restrita por inadimplência. Regularize: {portal}" |
| `AssinaturaAlunoCanceladaEvent` | `AssinaturaAlunoCanceladaWhatsAppAlunoHandler` | `AlunoId` direto | telefone | "Assinatura cancelada. Para reativar, fale com seu treinador. Histórico: {portal}" |

- Mesmo evento `AssinaturaAlunoCanceladaEvent` tem handler de e-mail p/ ALUNO **e** p/ TREINADOR; WhatsApp só p/ aluno (treinador segue e-mail — comentado no handler como canal preferido p/ decisão financeira). Ver GAPS (decisão de paridade reverte isso).

### Chamadas diretas — NÃO event-based (2; Application)
| Caso de uso | Arquivo | Destinatário | Quando | Mensagem |
|-------------|---------|--------------|--------|----------|
| Aprovar vínculo | `AprovarVinculoHandler` L116-122 | `aluno.Telefone` | após `tx.CommitAsync` | "Seu cadastro foi aprovado pelo seu treinador. Acesse o app para ver suas fichas." |
| Registrar aluno | `RegistrarAlunoHandler` L99-105 | `treinador.Telefone` | após `unitOfWork.CommitAsync` | "Novo aluno aguardando aprovação: {Nome}. Acesse o app para aprovar o vínculo." |

- ⚠️ `RegistrarAluno`→treinador NÃO tem contraparte de e-mail (gap REVERSO: WhatsApp existe, e-mail não).
- ⚠️ Destinatário SEM fallback: usa só `aluno.Telefone`/`treinador.Telefone`. `Telefone` é opcional e pode nascer `null` (e-mail tem fallback p/ `Conta.Email`; WhatsApp não) → notificação pode silenciosamente não disparar.

## CUSTO & MODELO META (per-message)
- Desde **01/07/2025**: cobrança **por mensagem** (não mais por conversa). Cobra quando *template* é entregue.
- Categorias: **marketing** (sem desconto volume), **utility** (desconto por volume; grátis dentro da janela de atendimento 24h aberta), **authentication** (OTP; desconto volume), **service** (sessão/não-template; grátis).
- Free entry points (click-to-WhatsApp ad / CTA FB Page) → janela estende p/ 72h, conversa grátis.
- Brasil (base Meta, USD/msg, ~2026): marketing ~$0.0625 · authentication ~$0.0315 · utility menor + escala por volume. Confirmar rate card no WhatsApp Manager.
- ⚠️ **ACHADO CRÍTICO — `type:text`**: `MetaWhatsAppCloudNotifier` envia mensagem de **sessão (free-form)**, que só é entregue DENTRO de uma janela de atendimento de 24h aberta pelo cliente. As 7 notificações atuais são **business-initiated** (cobrança, aprovação, inadimplência...) e tipicamente NÃO têm janela aberta → **Meta rejeita** (erro #131047 "re-engagement message" ou similar). Logo a integração atual, mesmo configurada, **provavelmente não entrega notificações frias**. Correção (follow-up): migrar p/ **template messages** (`type:"template"`, templates aprovados no painel Meta, categoria utility/auth) — ver GAPS.

## SEPARAÇÃO POR AMBIENTE
- ⚠️ **GAP**: NÃO existe `EnvironmentWhatsAppDecorator`. E-mail tem guardrail não-prod (`EnvironmentEmailDecorator`: `MarcarComoTeste`, prefixo, redirect, allowlist — ver [specification-email]). WhatsApp **não tem equivalente** → em homolog/dev, se configurado, envia p/ número REAL do destinatário. Meta Cloud API não tem sandbox de entrega real (só test number limitado a destinatários pré-cadastrados). Recomendação (follow-up): decorator análogo (redirect/allowlist de telefone em não-prod) OU restringir AccessToken de hmg a test number Meta.

## SEGURANÇA
- `WhatsApp:AccessToken` / `WhatsApp:PhoneNumberId` backend-only (env/secret/User Secrets), nunca frontend.
- Telefone sanitizado (`+ - espaço ( )`) mas SEM normalização/validação E.164 ou DDI BR (`55`) → número sem código de país pode falhar na Meta. Recomendar validação E.164 (follow-up).
- Falha de envio = log only (sem rethrow) → não vaza p/ usuário, não quebra transação.
- ⚠️ Sem webhook de status WhatsApp (e-mail tem webhook Resend → `email_delivery_logs`). Sem rastreabilidade de delivered/read/failed além do log de aplicação. Recomendar webhook Meta (`messages.statuses`) se rastreabilidade for requisito.

## TESTES
- Unit (xUnit, sem Docker): `MetaWhatsAppCloudNotifierTests`, `NullWhatsAppNotifierTests`, + handlers `PagamentoCriado`/`PagamentoFalhou`/`PagamentoEstornado`/`AssinaturaAlunoCancelada`/`AssinaturaAlunoMarcadaInadimplente` (`Tests/Infrastructure/Notifications/WhatsApp/`).
- ⚠️ Lacuna: as 2 chamadas diretas (`AprovarVinculo`/`RegistrarAluno` WhatsApp) NÃO têm teste dedicado do envio WhatsApp.

## GAPS / ROADMAP — paridade email→WhatsApp
Princípio (decisão do usuário): toda funcionalidade com e-mail deve ter WhatsApp, exceto channel-bound/interno. Estado atual: **e-mail = 19 features · WhatsApp = 7**. Cruzamento com [specification-email] (FLUXOS):

### Aluno
| Evento | E-mail | WhatsApp hoje | Status | Ação-alvo |
|--------|:---:|:---:|--------|-----------|
| `AlunoRegistradoEvent` (bem-vindo) | ✅ | ❌ | GAP | + handler WhatsApp |
| `VinculoAprovadoEvent` | ✅ (event) | ✅ (direta) | ✓ arq divergente | migrar p/ event-handler |
| `AssinaturaAlunoCriadaEvent` | ✅ | ❌ | GAP | + handler |
| `PagamentoCriadoEvent` | ✅ | ✅ | ✓ | — |
| `PagamentoFalhouEvent` | ✅ (toda) | ✅ (só 2ª+) | divergente | **paridade total** (toda tentativa) |
| `PagamentoEstornadoEvent` | ✅ | ✅ | ✓ | — |
| `AssinaturaAlunoMarcadaInadimplenteEvent` | ✅ | ✅ | ✓ | — |
| `AssinaturaAlunoCanceladaEvent` | ✅ | ✅ | ✓ | — |
| `AlunoInativadoEvent` | ✅ | ❌ | GAP | + handler |
| Verificação e-mail / reset senha | ✅ | ❌ | **N/A** | channel-bound (link por e-mail) — não portar |

### Treinador (decisão: TODOS os eventos ganham WhatsApp)
| Evento | E-mail | WhatsApp hoje | Status | Ação-alvo |
|--------|:---:|:---:|--------|-----------|
| `TreinadorAprovadoEvent` | ✅ | ❌ | GAP | + handler |
| `TreinadorReprovadoEvent` | ✅ | ❌ | GAP | + handler |
| `TreinadorInativadoEvent` | ✅ | ❌ | GAP | + handler |
| `AssinaturaAlunoCanceladaEvent` (treinador) | ✅ | ❌ | GAP | + handler treinador |
| `PagamentoEmDisputaEvent` (URGENTE) | ✅ | ❌ | GAP | + handler (prioridade) |
| Novo aluno pendente (`RegistrarAluno`) | ❌ | ✅ (direta) | gap REVERSO | avaliar + e-mail; migrar WhatsApp p/ event-handler |

### Admin
| Health reports (manual/agendado) | ✅ | ❌ | **N/A** | interno (e-mail/dashboard) — não portar |

### Itens estruturais (não-funcionais, follow-up)
1. **`type:text` → template messages** — sem isso, notificações frias não entregam. PRÉ-REQUISITO p/ qualquer notificação business-initiated funcionar. Criar templates Meta aprovados (utility/auth) + refatorar notifier p/ `type:"template"` com componentes/variáveis.
2. **`EnvironmentWhatsAppDecorator`** — guardrail não-prod (redirect/allowlist de telefone), paridade com `EnvironmentEmailDecorator`.
3. **Padronizar event-handlers** — migrar `AprovarVinculo` (usa `VinculoAprovadoEvent`, já existe) e `RegistrarAluno` (criar evento, ex. `AlunoRegistradoPendenteEvent`, + handler) de chamada direta → domain-event handler.
4. **`Habilitado` em `IWhatsAppNotifier`** — simetria com `IEmailService`; permite handlers gatearem cedo.
5. **Catálogo central de templates** — análogo a `EmailTemplates` (ou mapeamento p/ template names Meta).
6. **Validação E.164 / fallback de telefone** — evitar no-op silencioso por telefone ausente/mal-formado.
7. **Webhook de status Meta** — rastreabilidade delivered/read/failed (paridade com webhook Resend).

Execução do roadmap = follow-up via skill `tlc-spec-driven` (tasks atômicas + state file). Atualizar esta seção na mesma tarefa.

## DICAS / GOTCHAS
- Sem warning "Serviço de WhatsApp não configurado" no startup = Null NÃO foi registrado → integração real ativa (Null loga no ctor).
- Mensagem some sem erro? Checar: (1) telefone null → skip `LogDebug`; (2) `type:text` rejeitado fora de janela → `LogWarning` com body Meta; (3) gate caiu p/ Null (faltou PhoneNumberId/AccessToken).
- Links nas mensagens dependem de `App:FrontendBaseUrl` — vazio = link quebrado.
- Cobrança Meta só com template entregue; `type:text` em janela aberta = grátis (service). Migração p/ template muda o perfil de custo (utility/auth pagos fora de janela).
- Referências: modelo tático [specification-model]; camadas/dispatch [specification-backend]; e-mail paralelo [specification-email]; eventos de pagamento [specification-stripe]; `telefone` [specification-db].
