# specification-concurrency — concorrência, idempotência & ordering (forzion.tech)

DOC PARA AGENTES. CASA ÚNICA de concorrência — hoje espalhado incidente-a-incidente em [specification-coding §1]. Race, idempotência, optimistic locking, ordering transação↔efeito. Crítico onde dinheiro+webhook concorrem (Stripe redelivery, cobrança recorrente). Ler antes de webhook handler, outbox, efeito externo, contador/saldo, ou invariante "no máximo 1 X". Formato denso, incident-grounded (cada regra aponta artefato/incidente REAL do repo).

## MANUTENÇÃO
- Atualizar quando review pegar race de classe nova. NÃO duplicar a regra de ordering de [specification-coding §1] (canônico) — referenciar.

## 1. ORDERING TRANSAÇÃO ↔ EFEITO (cross-ref — canônico em coding §1)
- Mutar agregado → `CommitAsync` → efeito externo. Efeito que PRECISA ser garantido ⇒ outbox transacional ANTES do commit (`IOutboxEnfileirador.Enfileirar`), persiste atômico, worker entrega com retry. Detalhe + incidentes (refund antes do commit, evidência de disputa) em [specification-coding §1]. Aqui só o vínculo com idempotência (§2).

## 2. IDEMPOTÊNCIA (efeito exactly-once sob redelivery)
- **Webhook de ENTRADA (Stripe/Resend-Svix/Meta)** — redelivery é ESPERADO (provider re-envia o mesmo evento). Handler DEVE ser idempotente: guard por id do evento já processado OU chave única no efeito persistido. [incidente: delivery-log idempotente sob redelivery concorrente — commit ab82b9e.]
- **Outbox de SAÍDA** (`outbox_efeitos`) — entrega garantida + retry; `UQ(chave_idempotencia)` ([specification-db]) impede re-enfileiramento sob redelivery; efeito em si re-aplicável (re-PUT idempotente). [incidente: `fx:evidencia_disputa` — re-PUT idempotente, redelivery não re-enfileira — coding §1.]
- **Domain-event DURÁVEL** — outbox já dá exactly-once transacional, MAS o handler ainda guarda defense-in-depth (idempotente), pois o guard é barato e protege contra bug futuro. Padrão de guard: existência (`ObterPor...Async` já existe → não cria 2º) OU estado+tempo (`Status==Ativa && DataProximaCobranca>agora`). [incidentes: `PagamentoTreinadorPagoEvent`→renovação, `VinculoAprovadoEvent`→criar assinatura — coding §1.] EXCEÇÃO consciente: `MensagemSuporteCriadaEvent`→e-mail ao suporte é durável mas SEM guard de idempotência (envio de e-mail não tem chave única) — at-least-once: retry raro pode duplicar o e-mail à caixa de suporte (dano baixo, aceito). Registrado no `OutboxDurabilityRegistry` (chave `evt:MensagemSuporteCriada:{id}`); único par durável de NOTIFICAÇÃO (demais e-mails são best-effort, fora do outbox).

## 3. OPTIMISTIC CONCURRENCY (escrita simultânea no MESMO agregado)
- **Token `xmin` (PG system column) mapeado via EF** — `treinadores` JÁ usa (`AdicionarConcurrencyTokenTreinador`, [specification-db]): UPDATE concorrente do mesmo treinador aborta com `DbUpdateConcurrencyException` em vez de last-write-wins silencioso. Sem coluna física — mapeia o `xmin` do PG.
- **Quando aplicar a OUTRO agregado**: quando duas operações podem mutar o mesmo registro em paralelo e last-write-wins corromperia invariante (ex. troca de plano + cobrança concorrente). Mapear `xmin` como rowversion; tratar `DbUpdateConcurrencyException` → retry com recarga OU abortar com erro de negócio. NÃO engolir a exceção (= last-write-wins de volta).

## 4. CHAVE ÚNICA COMO GUARDA DE CORRIDA (check-then-act é race)
- Invariante "no máximo 1 X" defendida por **UNIQUE constraint no banco**, não só check-then-insert na aplicação. Duas escritas concorrentes passam o `if (!existe)` ANTES de qualquer insert → ambas inserem; só a constraint do banco barra a 2ª.
- JÁ no schema ([specification-db]): `pagamentos UQ(assinatura_aluno_id WHERE status='Pendente')` (1 cobrança pendente/assinatura), `pagamentos_treinador UQ(... WHERE status='Pendente')`, `treino_alunos UQ(treino_id WHERE status='Ativo')` (1 ficha ativa/treino), `assinaturas_aluno UQ(vinculo_id)` (1 assinatura/vínculo), `outbox UQ(chave_idempotencia)`.
- Tratar a violação de UNIQUE (`DbUpdateException`/PG 23505) como caminho de negócio esperado (já existe → no-op/erro claro), não como 500.
- **EXCEÇÃO consciente — rotação de refresh token** (`RefreshTokenService.RotacionarAsync`, [specification-security] §2): a rotação single-use é check-then-act (lê `UsadoEm==null` → `MarcarUsado` + cria sucessor) SEM guarda de banco — dois `POST /auth/refresh` com o MESMO refresh raw no mesmo instante passam ambos o check → dupla rotação (2 sucessores válidos na família), e a reuse-detection NÃO dispara (ambos viram o token não-usado). Defesa PRIMÁRIA é client-side: o interceptor (`frontend/src/lib/api/client.ts`) coalesce N 401s concorrentes de UM cliente numa única chamada de refresh (design `.specs/features/sessao-refresh` §FR-7). Race residual server-side (clientes/processos distintos, ou lock do cliente burlado) é ACEITA como leeway window — postura padrão de refresh-rotation (Auth0/OWASP toleram janela de graça vs serialização estrita): ambos os sucessores são legítimos (mesma família/conta), não concedem ao atacante nada sem o raw já em mãos; sev BAIXA. Hardening futuro se a janela incomodar: `xmin` (§3) em `refresh_tokens` → 2ª `MarcarUsado` aborta com `DbUpdateConcurrencyException`. NÃO há UNIQUE aplicável (cada sucessor tem hash distinto; o invariante violado é "≤1 sucessor por token-pai", não duplicidade de linha).

## 5. CONTADOR / SALDO
- Incremento concorrente (`tentativas_falhas_consecutivas`, qualquer saldo): `UPDATE ... SET x = x + 1` atômico no banco, NÃO read-modify-write na app (`x = obj.x + 1; save` perde incrementos sob concorrência).

## 6. REDELIVERY CONCORRENTE (não só sequencial)
- Mesmo webhook chegando 2× em PARALELO (não retry serial): idempotência precisa segurar sob concorrência REAL — UNIQUE constraint (+ tratar 23505) ou lock, não só "já processei?" check que sofre a mesma race do §4. [incidente ab82b9e: delivery-log sob redelivery concorrente.]

## 7. ENFORCEMENT
- **Teste de integração concorrente** (Testcontainers, [specification-tests §6]): disparar 2 chamadas do handler EM PARALELO e asseverar efeito único (não só retry sequencial). Esse é o gate real de idempotência.
- **UNIQUE constraints** no schema = gate de banco ([specification-db]). **`xmin` token** = revisão de diff (sem gate hard).
- Cross-ref gap: Stripe NÃO usa `Stripe-Idempotency-Key` nos Create (protegido por tx + UNIQUE app-side) — [specification-security §8].

## 8. REFERÊNCIAS
[specification-coding §1] (ordering — CANÔNICO), [specification-db] (xmin/UQ parciais/outbox), [specification-stripe] (webhook/refund/PaymentIntent), [specification-backend] (outbox/UnitOfWork/eventos), [specification-tests §6] (Testcontainers/dublês), [specification-security §7-8] (webhook signing / gap idempotency).
