# specification-coding — armadilhas de correção recorrentes (forzion.tech)

DOC PARA AGENTES. Padrões de CORREÇÃO transversais que reviews repetidamente pegam neste codebase — não estilo, não arquitetura macro (isso é [specification-backend]). Ler antes de escrever handler, integração externa, validação, ou mapeamento de erro — e antes de qualquer code-review (é o checklist que os reviews repetidamente precisam). Formato denso. Cada regra tem o PORQUÊ (incidente real) pra não virar dogma vazio.

## MANUTENÇÃO
- Atualizar quando um review/code-review pegar um bug de CLASSE nova (não pontual) — adicionar a regra + o incidente.
- Não duplicar regras de [specification-git] (commit/worktree) nem [specification-tests].

## 1. TRANSAÇÃO ↔ EFEITO EXTERNO IRREVERSÍVEL
- **Efeito externo irreversível (refund Stripe, e-mail, WhatsApp, webhook de saída) vai DEPOIS de `CommitAsync`** — nunca antes. Ordem: mutar agregado → `CommitAsync` → efeito externo. Falha do efeito = `LogCritical` + prossegue (estado já persistido). [incidente CR#1: refund emitido antes do commit → commit falha → dinheiro estornado sem cancelamento persistido + retry tenta refund 2× = `charge_already_refunded`.]
- Corolário (ATUALIZADO — outbox existe): efeito que PRECISA ser garantido vai por **outbox transacional** (`specification-backend §3.1`), não "LogCritical + manual". Padrão: enfileira o efeito (`IOutboxEnfileirador.Enfileirar("fx:<nome>", payload, chave)` ou domain-event durável) ANTES do `CommitAsync` → persiste atômico → worker entrega com retry. O "LogCritical + ação manual" só sobra para efeitos NÃO migrados ao outbox.
  - Aplicado: **evidência de disputa Stripe** — antes era `EnviarEvidenciaDisputaAsync` pós-commit em try/catch+LogCritical (perdia evidência se Stripe falhasse). Agora enfileirada `fx:evidencia_disputa` antes do commit da transição `EmDisputa`; re-PUT idempotente; redelivery não re-enfileira (chave única). Caminho aluno E treinador.
- **Persistência durável commita ANTES de responder sucesso; só o efeito best-effort vai a background.** Endpoint "fire-and-forget" que joga persist+commit+envio inteiros num `Task.Run` retorna 200 antes do commit → crash/recycle perde o dado. Separar: o write durável é awaitado com o **CT da request** (nunca `CancellationToken.None`) e commita dentro da request; só o envio externo (e-mail/notif, best-effort) destaca-se p/ background (scope novo — serviços scoped morrem com a request). Em fluxo anti-enumeração (forgot-password/resend) isso é trade-off consciente: latência externa off-path nos 2 ramos; persistência rápida in-path. [incidente CR4-pr163: reset/verification token gerado dentro de `Task.Run(CT.None)` → token perdido em recycle pós-200. Pre-flight do trade-off: [specification-design-review] §3.]
- Domain events de efeito colateral best-effort despacham no `CommitAsync` (padrão do `UnitOfWork`). Domain-event de **mutação crítica** (que não pode se perder) é marcado durável no `OutboxDurabilityRegistry` → re-dispatchado pelo worker com retry; o handler deve ser **idempotente** (guard defense-in-depth, pois o outbox já dá exactly-once transacional). Aplicado: `PagamentoTreinadorPagoEvent`→renovação (guard: `Status==Ativa && DataProximaCobranca>agora`; restrito a Ativa pra não pular regularização de Inadimplente) e `VinculoAprovadoEvent`→criar assinatura aluno (guard: `ObterPorVinculoIdAsync` já existe → não cria 2ª).

## 2. TEMPO & AUDITORIA
- **Timestamp de prova legal/auditoria = relógio do SERVIDOR (`TimeProvider.GetUtcNow().UtcDateTime`), NUNCA valor vindo do cliente.** Cliente pode forjar (retroativo/futuro). O valor do cliente, se útil, vai em campo informativo (`observacao`), não como o timestamp autoritativo. [incidente CR#1-revisão R8: `ConsentimentoDadosSaudeEm` do payload usado como `CreatedAt` do log de consentimento LGPD art. 11 — prova falsificável.]
- **Janela temporal de negócio (CDC 7 dias, SLA, expiração) mede-se a partir do EVENTO DE NEGÓCIO real, não da criação do registro.** Cuidado com campos setados em `Criar` (estado Pendente) que NÃO são resetados na transição de estado. [incidente CR#3: janela CDC media de `AssinaturaTreinador.DataInicio` (setado em `Criar`, status Pendente, antes do pagamento) — `Ativar` não reseta → cancelar 9 dias após cadastro mas 3 dias após pagar negava reembolso devido. Fix: medir do `DataPagamento` do 1º pagamento Pago.]
- Sempre UTC (`DateTimeKind.Utc`); ISO do cliente parseado pode vir `Unspecified` — normalizar.

## 3. INVARIANTES & FALHAS SILENCIOSAS
- **Não engolir `Result.IsFailure` de escrita que É a invariante que a feature garante.** `if (x.IsSuccess) {...}` sem `else` num write crítico = invariante violável em silêncio. Se a operação é o ponto da feature (ex.: registrar consentimento exigido), falha dela → `Result.Failure` (aborta) ou no mínimo `LogCritical`. [incidente CR#2: log de consentimento de saúde com `if (IsSuccess)` sem else → aluno criado com dado sensível sem registro de consentimento.]
- `catch` que engole exceção precisa de motivo explícito (efeito externo pós-commit que não deve bloquear — §1). Caso contrário, propagar.

## 4. VALIDAÇÃO & DEFENSE-IN-DEPTH
- **Validar input relevante à segurança/compliance no SERVIDOR mesmo que o frontend já trave.** Frontend é UX, não fronteira de confiança. [incidente: consentimento exigido via FluentValidation no Application, não só checkbox no React.]
- **Campos de texto livre podem carregar dado sensível.** Ao decidir "coleta dado de saúde?" incluir os campos livres (`ObservacoesAdicionais`), não só os estruturados. [incidente CR#5: `ColetaDadosSaude` ignorava `ObservacoesAdicionais` → "tenho diabetes" em texto livre não exigia consentimento.]
- Gates de offboarding/bloqueio: considerar TODOS os estados que deixam órfão, não só o estado óbvio. [incidente CR#4: gate de cancelamento só bloqueava vínculo `Ativo`, não `AguardandoAprovacao` → pedidos pendentes órfãos.]

## 5. ERROS & CONTRATO API
- **Codes de erro namespaceados por agregado** (`assinatura_treinador.offboarding_necessario`, não `offboarding_necessario` cru). [incidente CR#7: code cru divergia dos irmãos e o frontend hardcodava a string.]
- HTTP status vem do `ErrorType` (`Business`→422, `Validation`→400, `NotFound`→404, `Conflict`→409) via `ToProblemResult`/`ResultExtensions` — não setar status à mão por code.
- **`Error` sempre via factory com `Type` explícito** (`Error.Validation`/`Conflict`/`NotFound`/`Business`). `new Error(...)` cru e `=> new(...)` proibidos (construtor implícito vira Business silencioso) — gate `ResultPatternConventionTests`. Classificar pela natureza: input→Validation, estado/já-X/em-uso→Conflict, não-achado→NotFound, política→Business (regra canônica em [specification-backend] §2). `code` NUNCA muda ao reclassificar `Type` (FE chaveia por code).
- **Invariante quebrada (estado impossível) = `EstadoInconsistenteException`→500, nunca `DomainException` cru (422) nem `Result.Failure` disfarçado de sucesso.** `Result.Failure` jamais sinaliza caminho feliz — estado terminal de sucesso vira flag no response (ex.: `AssinaturaEncerrada`), não code-sentinela lido pelo caller. Gate proíbe `throw new DomainException` em `Application/UseCases/**`.
- `code` do `Error` sai como extension member do ProblemDetails → chega na RAIZ de `response.data.code`. Frontend lê via helper central (`extractApiError`/`extractApiErrorInfo`), NUNCA leitura inline `(err as {...}).response.data.code` espalhada por página. [incidente CR#8.]
- Contrato backend↔frontend: shape de 200/4xx + casing (camelCase) fixado nos DOIS lados; binding JSON do backend é case-insensitive (PascalCase ↔ camelCase ok).

## 6. REUSO / DEDUP
- Lógica de negócio idêntica entre dois agregados (ex.: reembolso CDC aluno vs treinador — só difere `reverterTransferencia` + repo) → UM serviço/helper de Application, não copy-paste por handler. [incidente CR#6: `ReembolsoArrependimentoService`.] Diferença pequena vira parâmetro; não generalizar a ponto de criar abstração prematura.
- Download blob, leitura de erro, formatação monetária → helper compartilhado (`lib/utils/downloadBlob`, `MoneyCentavos`, etc.), não reimplementar por tela.

## 7. PROCESSO (cross-ref)
- Worktree de sub-agent nasce em base errada por default — SEMPRE verificar/resetar para a branch-alvo antes de codar. Ver [specification-git] §WORKTREE.
- Commit/merge: Conventional, header ≤100, type válido (commitlint ativo). Ver [specification-git].
- Claim cross-file ("o frontend não envia X", "isso está quebrado") → VERIFICAR lendo o arquivo real antes de agir; finders/agents erram por leitura parcial. [incidentes: 2 falsos positivos refutados por leitura direta nesta feature.]

## 8. COMENTÁRIOS (ruído que review repete)
Agentes tendem a over-comentar. Incluir SÓ o "porquê" não-óbvio (invariante sutil, workaround com motivo, decisão contraintuitiva, gotcha de plataforma) — NUNCA o óbvio nem paráfrase do código. Regra de ESCRITA: remover o ruído ANTES de apresentar o código, não revisão pós-fato. O subset abaixo barrado por hook; paráfrase/óbvio o hook NÃO pega → passar o olho.
- **Barrado por hook (pre-commit)**: andaime/ref de tarefa (`// T2B.3:`, `// TCR1:`, `// T7:`); divisor decorativo unicode (`// ── X ──`, `// ══`, `// ——`). Fonte/sequência: [specification-git] §PRE-COMMIT HOOK / gate de comentário.
- **Convenção do repo (NÃO é violação — não tentar "limpar")**: divisor ASCII `// --- X ---` permitido SÓ em arquivos de teste (idiom existente, pervasivo); PROIBIDO em produção. XML doc (`/// <summary>`, `/// <inheritdoc/>`) permitido em interface/contrato público (`I*.cs`, DTO público, migration EF gerada); em implementação/método privado PROIBIDO — lá, se precisar do "porquê", `//` de uma linha.
- Inline em fim de linha que repete o código: proibido. Preferir nome claro a comentário.

## 9. CÁLCULO MONETÁRIO (CENTAVOS)
- **Todo cálculo de dinheiro (taxa, líquido, comissão, split) usa a disciplina CANÔNICA de centavos inteiros** (`MoneyCentavos`: `(long)(valor×100)` = floor; `taxa = (long)(valorCentavos×percent/100)`). NUNCA aritmética nova em reais com `Math.Round`/`MidpointRounding` — diverge do floor canônico por ±1 centavo e o usuário vê estimate ≠ extrato. Ao adicionar um cálculo monetário, DERIVAR da fórmula que já existe (ex.: líquido = `bruto_centavos − taxaCentavos`, mesma usada na comissão mensal), não inventar. [incidente CR-pr191: `liquidoEstimado` calculado como `Math.Round(bruto×(1−taxa/100),2,ToZero)` em reais → 94.90 vs 94.91 da comissão canônica (taxa 5%, bruto 99.90); rotular "estimado" NÃO justifica divergir do invariante.]
- **Teste de dinheiro usa valor QUEBRADO que força meio-centavo, não valor redondo.** `100→90` (taxa 10%) passa em qualquer fórmula — não prova nada. O caso que separa floor-de-centavo da fórmula naive precisa de fração: ex. taxa 10% + `99.95` (→ 89.96 canônico vs 89.95 naive); taxa 5% + `99.90` (→ 94.91 vs 94.90). Asserção tautológica em valor redondo = green falso. [mesmo incidente: o teste original só cobria `100→90`, mascarou o bug.]

Cross-ref: [specification-backend] (Result/UnitOfWork/DI), [specification-stripe] (refund/webhook), [specification-lgpd] (consentimento/auditoria), [specification-tests], [specification-git].
