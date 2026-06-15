# specification-design-review — checklist de design-time / pre-flight (forzion.tech)

DOC PARA AGENTES. Checklist a rodar ANTES de codar (design-time), gêmeo PRE-CÓDIGO de [specification-coding] (que é pós-código/review). Pega a CLASSE de falha que teste unitário + code-review estruturalmente NÃO veem: corrida, fraqueza de defense-in-depth, abuso fora do fluxo feliz, PII em repouso, drift de spec. Origem: auditoria 2026-06-14 — 3 HIGH (race de rotação refresh, webhook agindo em payload raw não-verificado, sem enforcement de `Livemode`) que passavam em todos os testes porque não quebram comportamento nem feature; só sob concorrência/abuso/latência.

Formato denso. Cada bloco tem o PORQUÊ (achado real) pra não virar dogma. Cada decisão pondera os 3 eixos (segurança+performance+usabilidade — AGENTS regra 10).

## QUANDO RODAR (gatilho)
Rodar quando a feature/alteração toca QUALQUER: **auth/sessão/token**, **webhook/integração externa**, **concorrência/idempotência/lock**, **dado pessoal/PII**, **pagamento/dinheiro**. Não-trivial só (ajuste cosmético/copy não dispara).

Saída = bloco(s) relevante(s) respondido(s) DENTRO de `.specs/features/[feat]/design.md` (não doc novo). Cada "sim, há risco" vira teste adversarial planejado JUNTO da task (não depois — AGENTS DoD#1) ou decisão 3-eixos registrada. Sem achado ⇒ uma linha "design-review: nada aplicável em §X".

## MANUTENÇÃO
- Atualizar quando auditoria/review achar CLASSE nova de falha latente (não pontual) — adicionar o bloco + o achado, igual [specification-coding §MANUTENÇÃO].
- NÃO duplicar regras de correção de [specification-coding] (este é o gatilho PRE; lá é o checklist PÓS). Concorrência canônica: [specification-concurrency]. Postura/headers/segredo: [specification-security].

## 1. THREAT-MODEL LEVE ("o que pode dar errado?")
Antes de desenhar o fluxo feliz, responder em 5 perguntas (STRIDE-lite, sem cerimônia):
- **Quem é o atacante e o que ele controla?** Input do body, header, query, timing das requisições, ORDEM/concorrência, valor (amount/id) — listar o que NÃO é confiável.
- **Pior caso se ele contornar o frontend / a sequência esperada?** Frontend é UX, não fronteira (=[specification-coding §4]).
- **Há caminho de ABUSO fora do fluxo feliz?** Evento cross-mode, id de outro tenant, replay, double-submit. [achado H3: `Event.Livemode` nunca checado → evento test-mode assinado processado igual a live, porque "ninguém manda test pra prod" no uso normal — exatamente o caso que só um threat-model levanta.]
- **Algum valor sensível vem do cliente e deveria ser derivado no servidor?** amount, currency, ownerId, timestamps de auditoria. [achado M4: refund full-amount sem assert server-side `≤ capturado`; ok hoje só porque callers são owner-scoped.]
- **A escolha "óbvia" enfraquece um eixo?** (ex.: e-mail-OTP pedido → viola segurança vs TOTP). Gray area → AskUserQuestion com recomendação fundamentada em fonte (OWASP/context7), não intuição (=AGENTS regra 10).

## 2. CONCORRÊNCIA & IDEMPOTÊNCIA
Para todo write que decide com base no estado lido:
- **Read-then-write é atômico?** Padrão `if (estado ok) { ...; commit }` com leitura ANTES = janela de corrida. Dois requests concorrentes passam o check antes de qualquer commit. [achado H1: rotação de refresh lia `UsadoEm` então marcava+inseria sucessor, ReadCommitted sem lock → 2 `/auth/refresh` simultâneos = 2 cadeias válidas, derrota reuse-detection (a defesa headline da spec). Passava em todo teste serial.]
- **Mecanismo escolhido** (ponderar perf no caminho quente vs consistência): `UPDATE ... WHERE <condição> ` + rowcount (leve, 1 statement); concurrency token otimista (idiomático EF, exige coluna); tx Serializable (consistente, mas retry 40001 + latência). Canônico: [specification-concurrency §ordering].
- **Retry/duplicata é idempotente?** Chamada de saída repetida (rede/retry) não pode dobrar efeito → idempotency-key estável; consumidor de evento dedup por id.
- **Ordem de eventos importa?** Webhook fora de ordem / terminal antes de pendente → short-circuit por estado terminal.
- Planejar teste de corrida (Barrier + Postgres real, modelo `ConcurrentBillingRaceTests`) JUNTO da task.

## 3. FRONTEIRA DE INTEGRAÇÃO / WEBHOOK
Para todo receptor de webhook / chamada a serviço externo:
- **Agir SOBRE o objeto verificado, nunca re-parsear o raw.** Verificação de assinatura e dado processado têm que ser o MESMO objeto. [achado H2: `ConstructEvent` (Stripe) verificava e retornava `bool`; handler descartava o `Event` verificado e re-parseava a string raw → verificação desacoplada do dado. Funcionava porque a assinatura ainda barrava a entrada; fraqueza latente, não bug.]
- **Falha de verificação = fail-closed por tipo CONCRETO.** Catch da exceção concreta (ex.: `WebhookVerificationException`), nunca match por nome/substring; qualquer exceção de verify rejeita. [achado M2: catch Svix tratava só tipos cujo nome continha "Verification"/"Webhook" → outro tipo vazava 500.]
- **Asserir contexto: mode/livemode + tenant/conta.** Evento tem que casar com o ambiente (`Livemode == esperado`) e com o recurso/tenant esperado (account scoping Connect). [achado H3.]
- **Idempotência de evento:** dedup por id do evento (replay não re-credita/re-reembolsa); índice único + tratamento de 23505.
- **Replay window:** confirmar que o verificador impõe tolerância de timestamp (Stripe `ConstructEvent` e Svix `verify` impõem por padrão — verificar via context7, não assumir).
- **Idempotency-key em chamadas de SAÍDA** (create/refund) pra retry virar no-op no provedor.
- **Segredo de env**, nunca hardcoded/logado; `Null*` impl quando não configurado deve fail-safe (não "sucesso" silencioso que pula cobrança/verificação).
- Efeito externo irreversível: ordem e outbox = [specification-coding §1].

## 4. AUTHZ / IDOR (institucionaliza a força existente)
- **Ownership re-derivado do TOKEN no handler, não confiar em id do cliente.** Endpoint que recebe `alunoId/treinoId/pagamentoId` valida contra `userContext.PerfilId/ContaId` (JWT), defense-in-depth ALÉM do gate de role. (Postura atual do repo é forte aqui — manter, não regredir.)
- **Planejar teste negativo cross-tenant** (usuário A NÃO acessa recurso de B) JUNTO da task — é a cobertura que prova a defesa.
- **Function-level (BFLA):** endpoint admin/internal restrito (policy + chave constante-time); internal não alcançável externamente.
- Guard arquitetural opcional (arch-test) p/ flagar handler resource-scoped sem `IUserContext` — guard contra omissão FUTURA.

## 5. DADOS & PII (LGPD)
- **PII em repouso minimizada por padrão.** Não persistir payload raw de provider (telefone/email) — gravar só campos parseados + recipient mascarado. [achado M3.]
- **Sem PII raw em log Error/Critical** — esses persistem em `error_logs` FORA do escopo de anonimização. Mascarar (espelhar `MascararEmail`). [achado M5: `LogError(...{Phone})` raw persistia telefone.]
- **Auditoria de acesso a dado pessoal atribuída ao ATOR real** (admin que exporta ≠ titular). [achado M6: export admin logado como o próprio titular → acesso não-atribuível.]
- LGPD: export self-scoped + completo; erasure atômica + cascade (incl. read-models/delivery logs/sessões); reter o que é obrigação fiscal/legal. Sensível em repouso hasheado (refresh/reset/verify tokens).
- Segredo nunca em appsettings versionado / seed / log.

## 6. DECISÃO POR 3 EIXOS (AGENTS regra 10)
Toda escolha de mecanismo/lib/parâmetro pondera EXPLICITAMENTE **segurança + performance + usabilidade** — os três. Registrar o trade-off no design.md (tabela "Tech Decisions"). Não andar pra trás em nenhum eixo sem trade-off consciente. Postura de segurança ancora em fonte autoritativa (OWASP/docs oficiais via context7), não em intuição.

## 7. GATE DE SAÍDA (DoD do design — antes de abrir tasks)
- [ ] Bloco(s) §1–§6 relevante(s) respondido(s) em `design.md` (ou "nada aplicável").
- [ ] Cada risco vira teste adversarial PLANEJADO na task (race / negative-authz / signature-reject / livemode-reject), escrito junto da implementação (AGENTS DoD#1; sem TDD cerimonial).
- [ ] `specification-*` da(s) área(s) tocada(s) RE-LIDA(s) antes de planejar (AGENTS regra 2).
- [ ] Decisões 3-eixos registradas.
- **PÓS-CÓDIGO (anti-drift):** atualizar a `specification-*` da área na MESMA branch se o código divergiu (AGENTS DoD#9a) — specs versionadas = estado REAL. [achado SD1: `specification-security §8` listava "sem Idempotency-Key" muito depois das keys existirem.]
