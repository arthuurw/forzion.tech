# specification-fiscal — emissão de NFS-e (forzion.tech)

DOC PARA AGENTES. Fonte de verdade do módulo fiscal: emissão de NFS-e Nacional (SEFIN/gov.br), cancelamento, reconciliação, dados fiscais do treinador. Formato denso, agent-oriented. Consultar ANTES de alterar qualquer coisa fiscal/NFS-e. Reflete o estado REAL implementado (T1–T25). Estrutura de tabelas vive em [specification-db]; cert/mTLS/segredo em [specification-security]; logs/alertas em [specification-observability]; guarda x apagamento em [specification-lgpd]; eventos de `PagamentoTreinador` em [specification-stripe]. NÃO duplicar.

## MANUTENÇÃO DESTE ARQUIVO
- Atualizar NA MESMA TAREFA de qualquer mudança em: enums/máquina-de-estado da nota, contrato `IEmissorNfseService`, settings `Nfse:*`, handlers (emissão/comissão/cancelamento/reconciliação/e-mail), endpoints, workflows cron, pipeline DPS/assinatura.
- Mudança de tabela → [specification-db]. Mudança de evento de pagamento do treinador → [specification-stripe].

## CONTEXTO LEGAL / CADÊNCIA
- Tomador da nota = o **treinador** (a forzion presta serviço a ele). Dois fluxos, NOTAS DISTINTAS (códigos de serviço diferentes; DPS Nacional é 1-serviço):
  - **Fluxo 1 — assinatura SaaS** (`TipoNotaFiscal.AssinaturaSaaS`): 1 `PagamentoTreinador.Pago` → 1 NFS-e imediata. Receita pontual identificável; doc imediato ao treinador.
  - **Fluxo 2 — comissão marketplace** (`ComissaoMarketplace`): mensal AGREGADA por treinador (soma dos `ApplicationFeeAmount` do período). Norma marketplace; menos notas; reconciliação simples.
- **Sandbox-first**: `Nfse:Ambiente=Restrita` default em dev/hmg — emissão fiscal é IRREVERSÍVEL; Produção só com pré-reqs (cert A1 e-CNPJ, Inscrição Municipal, credenciamento gov.br/nfse, adesão do município, códigos de serviço+alíquota do contador). Dev/teste roda 100% em Restrita SEM esses itens via `Habilitado=false`.

## STACK & GATE
- Provedor: **API gov direta** (SEFIN Nacional), não terceiro — grátis, sem intermediário no dado fiscal, é o canal obrigatório. Custo só do cert A1.
- Confiabilidade: **reusa o outbox de efeitos** (`fx:emitir_nfse`/`fx:cancelar_nfse`) — entrega garantida + idempotente, mesmo mecanismo da evidência de disputa ([specification-stripe] R9). Emissão NÃO pode sumir em falha transitória.
- `IEmissorNfseService` (Application/Interfaces): `EmitirAsync(DpsInput, ct)→NfseResultado` · `ConsultarAsync(chaveAcesso, ct)→NfseStatus` · `CancelarAsync(chaveAcesso, motivo, ct)→NfseResultado`.
  - DTOs (mesmo arquivo): `DpsPrestador(Cnpj, InscricaoMunicipal?, CodigoMunicipioIbge, RegimeTributario)`; `DpsInput(Prestador, DadosFiscais Tomador, CodigoServico, Aliquota, Valor, Competencia, NumeroDpsEstavel)`; `NfseResultado(Sucesso, ChaveAcesso?, NumeroNfse?, DataEmissao?, DanfseRef?, CodigoErro?, MotivoErro?)`; `NfseStatus(Situacao, NumeroNfse?, DataEmissao?, DanfseRef?, CodigoErro?, MotivoErro?)`; `enum NfseSituacao {Autorizada, Cancelada, Rejeitada, Processando, NaoEncontrada}`.
  - `NfseResultado.Sucesso=false` ⇒ **rejeição do gov** (4xx; `CodigoErro`/`MotivoErro` preenchidos, SEM retry). Exceção (timeout/5xx) ⇒ propaga p/ retry do outbox. Distinção é o contrato.
- Impls (Infrastructure/Services):
  - `EmissorNfseNacionalService` — `Habilitado=true`. Monta DPS XML → assina (xmldsig enveloped, `X509Certificate2` A1) → GZip+Base64 → transmite via `HttpClient` nomeado `"nfse"` (mTLS) → parseia retorno. Wrapper fino, SEM retry custom (retry = outbox).
  - `NullEmissorNfseService` — `Habilitado=false`; no-op + LogWarning. Padrão `Null*` do projeto. Registrado **Singleton** (não Scoped) → o `LogWarning` do construtor "Emissor de NFS-e não configurado" sai **1x no startup**, não por-resolução (issue #180: Scoped logava ~30x conforme escopos do seed/boot resolviam o serviço). `EmitirAsync`/`CancelarAsync` mantêm o warn por-chamada (sinal real de tentativa com emissão off). Stateless ⇒ singleton seguro.
- GATE (DI `InfrastructureExtensions`): `Nfse:Habilitado=true` → `EmissorNfseNacionalService` (Scoped) + `HttpClient "nfse"` com cert; senão → `NullEmissorNfseService` (Singleton).

## CONFIG (chaves `Nfse:*`, classe `NfseSettings`)
| Chave | Função | Ausente / default |
|-------|--------|-------------------|
| `Habilitado` | gate Nacional vs Null | `false` → Null (boot OK sem cert) |
| `Ambiente` | `Restrita`(0)\|`Producao`(1) | `Restrita`. tpAmb DPS: Restrita→2, Producao→1 |
| `UrlBase` | base SEFIN | — |
| `CnpjPrestador` | CNPJ da forzion (prestador) | obrigatório se `Habilitado` |
| `InscricaoMunicipal` | IM do prestador | obrigatório se `Habilitado` |
| `CodigoMunicipioIbge` | município emissor (7 díg) | obrigatório se `Habilitado` |
| `SerieDps` | série do DPS | obrigatório se `Habilitado` |
| `CodigoServicoAssinatura` | cód serviço fluxo 1 (software 1.05) | obrigatório se `Habilitado` |
| `CodigoServicoComissao` | cód serviço fluxo 2 (intermediação 10.x) | — |
| `AliquotaIss` | alíquota ISS | `>0` se `Habilitado` |
| `CertificadoPath` | path do .pfx A1 | obrigatório se `Habilitado` |
| `CertificadoSenha` | senha do .pfx (SECRET) | obrigatório se `Habilitado`. **NUNCA logada** |
| `RegimeTributario` | regime | `SimplesNacional` |
| `TribISSQN` | tributação do ISSQN no DPS | `1` |
| `TpRetISSQN` | tipo de retenção do ISSQN no DPS | `1` |
| `PrazoCancelamentoDias` | janela de cancelamento (fast-path local) | `90` |

- CAVEAT `TribISSQN`/`TpRetISSQN` (R7): default `"1"` = comportamento legado (tributável + não retido). Valor correto depende de regime/município do prestador — VALIDAR com contador em Produção Restrita antes do go-live. Configurável p/ permitir override sem deploy.

- `ValidateOnStart` (espelha `StripeSettings`): se `Habilitado` → todos obrigatórios não-vazios + cert carregável; senão boot FALHA. `Habilitado=false` → boot OK.
- Local/secret: `CertificadoPath`/`CertificadoSenha` via User Secrets ou env. Senha = segredo — ver [specification-security].

## DOMÍNIO

### `NotaFiscal` (Domain/Entities) — agregado
- Factories `Result<T>`: `CriarAssinatura(treinadorId, pagamentoTreinadorId, valor, agora)` (Tipo=AssinaturaSaaS); `CriarComissao(treinadorId, competenciaInicio, competenciaFim, valor, agora)` (Tipo=ComissaoMarketplace). Ambas: `valor<=0` rejeitado; nascem `Pendente`; setam `NumeroDps = NumeroDpsEstavel()`.
- `NumeroDpsEstavel()` (idempotência da reemissão; gov dedup): `AS-{PagamentoTreinadorId}` (assinatura) | `CM-{TreinadorId}-{yyyyMM}` (comissão).
- Máquina de estado (`enum NotaFiscalStatus {Pendente=0, Emitida=1, Erro=2, BloqueadaDadosFiscais=3, CancelamentoSolicitado=4, Cancelada=5, CancelamentoExpirado=6}`), transições guard'd (`Result` + `NotaFiscalErrors`):
  - `MarcarEmitida(chave, numero, dataEmissao, danfseRef?, agora)`: {Pendente|Erro}→Emitida; exige chave; seta ChaveAcesso/NumeroNfse/DataEmissao/DanfseRef; limpa erro; **emite `NotaFiscalEmitidaEvent(Id, TreinadorId, chave, agora)`**.
  - `MarcarErro(codigo, motivo, agora)`: {Pendente|Erro}→Erro (retry permitido).
  - `MarcarBloqueadaDadosFiscais(agora)`: Pendente→BloqueadaDadosFiscais; emite `NotaFiscalBloqueadaDadosFiscaisEvent`.
  - `RegistrarCancelamentoPendentePreEmissao(motivo, agora)` (R8): {Pendente|Erro|BloqueadaDadosFiscais}→(sem mudar Status) seta `CancelamentoPendentePreEmissao=true` + `MotivoCancelamentoPendente`. Para estorno/disputa que chega ANTES da emissão.
  - `SolicitarCancelamento(agora)`: Emitida→CancelamentoSolicitado; limpa `CancelamentoPendentePreEmissao`.
  - `MarcarCancelada(agora)`: CancelamentoSolicitado→Cancelada.
  - `MarcarCancelamentoExpirado(agora)`: CancelamentoSolicitado→CancelamentoExpirado.
- Campos `CancelamentoPendentePreEmissao` (bool, default false) + `MotivoCancelamentoPendente` (string?, ≤500) — migração `AdicionarCancelamentoPendentePreEmissaoNfse`.
- ChaveAcesso só é setada em `MarcarEmitida` — invariante: nota sem chave nunca foi transmitida com sucesso (base da reconciliação).

### `DadosFiscais` (Domain/ValueObjects) — VO owned por `Treinador`
- `Criar(tipoDoc, documento, razaoSocial, EnderecoFiscal, inscricaoMunicipal?)` valida dígito CPF/CNPJ, CEP, código IBGE, UF. `enum TipoDocumentoFiscal {Cpf=0, Cnpj=1}`. `EnderecoFiscal{Logradouro, Numero, Complemento?, Bairro, CodigoMunicipioIbge, Uf, Cep}`.
- `Treinador.DadosFiscais? DadosFiscais` (nullable até preenchido) + `Result DefinirDadosFiscais(...)`. Padrão VO `Email`. Persistido owned na MESMA tabela `treinadores` (sem join).

## PERSISTÊNCIA
- Tabela `notas_fiscais` + colunas owned `dados_fiscais_*` em `treinadores`. Migration `CriarNotasFiscaisEDadosFiscaisTreinador` (schema-agnostic, search_path). Detalhe de colunas/índices em [specification-db].
- Idempotência por índice UNIQUE: `ix_notas_fiscais_pagamento_treinador_id_unique` (fluxo 1), `ix_notas_fiscais_treinador_tipo_competencia_unique` (fluxo 2). Check `ck_notas_fiscais_valor_nao_negativo`.
- `INotaFiscalRepository`: `AdicionarAsync`, `ObterPorIdAsync`, `ObterPorPagamentoTreinadorAsync`, `ListarPorTreinadorAsync`(keyset), `ListarPorStatusAsync`(keyset; **tracked** — sem `AsNoTracking`, p/ reconciliação persistir; R1), `ListarAdminAsync`(filtro+keyset), `ExisteComissaoAsync(treinador, competencia)`, `ListarTreinadoresComComissaoAsync(treinadorIds, competencia)` (batch — evita N+1 no lote de comissão; R-menor d).

## FLUXOS

### Fluxo 1 — emissão da assinatura (por pagamento)
- `EmitirNfseAssinaturaHandler` (Application, domain-event handler de `PagamentoTreinadorPagoEvent`): valor>0; dados fiscais presentes → `CriarAssinatura` + `Enfileirar("fx:emitir_nfse", payload, "fx:emitir_nfse:assinatura:{pagamentoTreinadorId}")` ANTES do commit. Dados fiscais ausentes → `MarcarBloqueadaDadosFiscais` + enfileira notificação. Valor 0 (Free/proração) → no-op.
- **Durável**: registrado em `OutboxDurabilityRegistry.RegistrarHandlerAdicional` (par com `PagamentoTreinadorPagoHandler`) — roda no worker com retry, NÃO best-effort. Dispatcher pula handler durável in-memory (sem double-run).
- `EmitirNfseEfeitoHandler` (Infrastructure/worker, `IOutboxEfeitoHandler`): drena `fx:emitir_nfse` → `EmissorNfseNacionalService.EmitirAsync` → sucesso `MarcarEmitida`(+chave+DANFSe); rejeição `MarcarErro`; exceção propaga (retry). Aceita nota em `Erro` (reprocessamento, sem reset).

### Fluxo 2 — comissão mensal agregada
- `GerarNfseComissaoMensalHandler` (Application, chamado pelo cron): por treinador com fee no período (keyset/lote), soma via `IPagamentoRepository.ListarComissaoPorTreinadorNoPeriodoAsync` (join `pagamentos`×`assinatura_alunos`, `Pago` + `DataPagamento ∈ [inicio, fimExclusivo)`, `GROUP BY treinador_id`). `CriarComissao` + enqueue `fx:emitir_nfse:comissao:{treinadorId}:{yyyyMM}`. Sem fee → sem nota.
- **Soma por pagamento** espelhando o canônico `MoneyCentavos.ValorETaxaCentavos` (R9): `floor(floor(valor*100)*taxa/100)` por pagamento (trunca p/ centavos ANTES do percentual, igual ao `ApplicationFeeAmount` que o Stripe cobrou em CADA PaymentIntent), somado no grupo. Translável p/ SQL (`FLOOR` + aritmética). Paridade coberta por teste de integração vs `CalcularTaxaCentavos`. Truncar o agregado divergiria ~1 centavo/pagamento (inaceitável p/ doc fiscal).
- CAVEAT: `ApplicationFeeAmount` NÃO é persistido (derivado). A agregação recomputa com `PaymentSettings.TaxaPlataformaPercent` ATUAL → se a taxa mudou historicamente, diverge do fee cobrado à época. Exato só persistindo o fee no `Pagamento` (fora de escopo).
- SEM índice dedicado p/ a query (tradeoff perf consciente: leitura fria 1×/mês vs write-path quente de todo pagamento; tabela ainda pequena — [specification-performance §2]). Idempotência: `ExisteComissaoAsync` + UNIQUE + chave outbox.
- Endpoint `POST /internal/gerar-nfse-comissao` (X-Internal-Key) + workflow `gerar-nfse-comissao.yml` (cron mensal + Issue on failure). Molde `billing-renewal.yml`.

### Cancelamento (estorno/disputa do treinador)
- Eventos `PagamentoTreinadorEstornadoEvent`/`PagamentoTreinadorEmDisputaEvent` (criados aditivamente; `PagamentoTreinador.MarcarEstornado/EmDisputa` passam a disparar — ver [specification-stripe]).
- `CancelarNfseHandler` (Application, UMA classe p/ os DOIS eventos; roteamento explícito `IDomainEventHandlerBase.HandleAsync` p/ evitar CS8705): localiza nota por `pagamentoTreinadorId`. Roteamento por status: **Emitida** → `SolicitarCancelamento` + enqueue `fx:cancelar_nfse:{notaId}`; **pré-emissão** {Pendente|Erro|Bloqueada} (R8) → `RegistrarCancelamentoPendentePreEmissao` + commit (intenção persistida, sem enqueue ainda); demais (já Cancelada/Solicitado/Expirado) → no-op logado. Durável.
- R8 conclusão da intenção: `EmitirNfseEfeitoHandler`, após emissão BEM-SUCEDIDA e commit, se `CancelamentoPendentePreEmissao` → `SolicitarCancelamento` + enqueue `fx:cancelar_nfse:{notaId}`. Garante que estorno-antes-de-emitir não deixa nota emitida para cobrança estornada.
- `CancelarNfseEfeitoHandler` (worker): guard `PrazoCancelamentoDias` LOCAL (fast-path) ANTES de chamar o gov → sucesso `MarcarCancelada`; rejeição do gov (ex. E8001 prazo) OU prazo local estourado → `MarcarCancelamentoExpirado` + `LogCritical` (estado terminal não-cancelado, exige ajuste fiscal manual do contador). Endpoint gov: `POST /nfse/{chave}/eventos`, evento `101101`, XML assinado+gzip+b64 (mesmo pipeline da emissão).

### Reconciliação (P3)
- `ReconciliarNfseHandler` (Application): varre status NÃO-terminais `{CancelamentoSolicitado, Erro, Pendente}` por keyset; consulta o gov SÓ quando há `ChaveAcesso` (sem chave = nunca transmitida → nada a comparar). Transições: gov `Cancelada`+local `CancelamentoSolicitado`→`MarcarCancelada`; gov `Autorizada`+local `Pendente`/`Erro`→`MarcarEmitida`. Demais divergências = no-op logado. Idempotente (terminais saem do conjunto varrido). Falha de uma nota é absorvida (não aborta o batch; molde `ReconciliarPagamentosStripeHandler`). Resposta `(Consultadas, Atualizadas, SemAlteracao, Erros)`.
- Endpoint `POST /internal/reconciliar-nfse` (X-Internal-Key) + workflow `reconciliar-nfse.yml` (cron diário 06:00 UTC + Issue on failure). Molde `billing-reconciliation.yml`.

### E-mail DANFSe ao treinador (P3)
- `NfseEmitidaEmailHandler` (Infrastructure/Notifications/Email, domain-event handler de `NotaFiscalEmitidaEvent`): lê a nota via `INotaFiscalRepository.ObterPorIdAsync` (evento só carrega id+chave) → treinador → conta; envia ao e-mail de login. Template `EmailTemplates.NfseEmitida(nomeTreinador, numeroNfse, valor, dataEmissao, linkNotas)`; link `{FrontendBaseUrl}/treinador/notas-fiscais` (NÃO DanfseRef cru). `Habilitado` check; null em qualquer ponto → LogWarning + no-op.
- **UNGATED por tier** (sem `IPlanoNotificationPolicy`): documento fiscal é OBRIGAÇÃO, não perk de plano — igual aos e-mails de billing do próprio treinador. Ver [specification-email].
- **Best-effort in-memory** (NÃO outbox): perda transitória é recuperável (portal T19 + reconciliação T24); só o e-mail de suporte é durável.

## ENDPOINTS
| Método/Rota | Auth | Função |
|-------------|------|--------|
| `PUT /treinador/dados-fiscais` | `IUserContext` (próprio) | grava dados fiscais; CPF/CNPJ inválido → 422 |
| `GET /treinador/dados-fiscais` | `IUserContext` (próprio) | lê dados fiscais |
| `GET /treinador/notas-fiscais` | `IUserContext` (próprias) | lista (keyset) |
| `GET /treinador/notas-fiscais/{id}/danfse` | `IUserContext` (próprias) | download DANFSe |
| `GET /admin/notas-fiscais` | admin | lista + filtro (status/treinador) + paginação |
| `POST /admin/notas-fiscais/{id}/reprocessar` | admin | re-enfileira nota em `Erro` |
| `POST /internal/gerar-nfse-comissao` | X-Internal-Key | dispara fluxo 2 |
| `POST /internal/reconciliar-nfse` | X-Internal-Key | dispara reconciliação |

- **DANFSe cross-tenant**: nota de outro treinador devolve o MESMO 404 (`NaoEncontrada`) que nota inexistente — não vazar existência (404 ≠ 403).
- **Reprocessar**: só status `Erro` (senão `ReprocessamentoInvalido` 422); chave de outbox por tentativa `fx:emitir_nfse:reprocessar:{notaId}:{ticks}` (cada reprocessamento é efeito novo; índice único do outbox barraria reuso da chave original).
- Internal endpoints: `AllowAnonymous` + `InternalApiKeyValidator.ChaveInternaValida` (FixedTimeEquals) + `RequireRateLimiting("internal")`. Detalhe em [specification-security].

## SEGURANÇA (resumo — canônico em [specification-security])
- Cert A1 (.pfx) = cert cliente mTLS via `HttpClientHandler.ClientCertificates` (`X509KeyStorageFlags.EphemeralKeySet`, sem persistir chave). Mesmo A1 assina o XML e faz o TLS.
- GOTCHA: efêmero p/ cert cliente só funciona em runtime **Linux** (OpenSSL); no **Windows** o schannel recusa no handshake → dev local sobe com `Habilitado=false` (deploy é Debian `aspnet:8.0`).
- `CertificadoSenha` NUNCA logada. Endpoints internos protegidos por X-Internal-Key (FixedTimeEquals) + rate-limit.

## OBSERVABILIDADE (resumo — canônico em [specification-observability])
- `MarcarErro` → LogWarning (nota visível em Erro; pagamento intacto). `CancelamentoExpirado` → LogCritical (terminal não-cancelado, ajuste manual). Reconciliação loga `de→para` por nota + sumário. Workflows cron abrem Issue on failure.

## LGPD (resumo — canônico em [specification-lgpd])
- Dados fiscais de nota EMITIDA sobrevivem à anonimização (guarda fiscal ~5 anos): obrigação fiscal > apagamento. Exceção legítima documentada.

## TESTES
- Unit (xUnit, sem Docker): Domain (`NotaFiscalTests` transições, `DadosFiscaisTests` dígito verificador), Application (`EmitirNfseAssinaturaHandlerTests`, `GerarNfseComissaoMensalHandlerTests`, `CancelarNfseHandlerTests`, `ReconciliarNfseHandlerTests`), Infra (`NullEmissorNfseServiceTests`, `EmissorNfseNacionalServiceTests` com `HttpMessageHandler` mockado, `NfseEmitidaEmailHandlerTests`, `NfseSettingsTests`), Api (`TreinadorEndpointsTests`/`AdminEndpointsTests`/`InternalEndpointsTests` NFS-e), DI smoke (`HandlerDiRegistrationTests`).
- Integration (Testcontainers, só CI): `NotaFiscalRepository` round-trip + UNIQUE, `EmitirNfseEfeitoHandler`/`CancelarNfseEfeitoHandler` worker+DB, query de comissão.
- Frontend (vitest): `dados-fiscais`, `notas-fiscais` (treinador), `admin/notas-fiscais`.

## DICAS / GOTCHAS
- Gate Null/Nacional: sem warning no startup = Nacional ativo (Null loga no ctor). Simétrico a e-mail/WhatsApp.
- DPS XML CONFERIDO no XSD 1.01 (`manual/.../Schemas/1.01`): raiz `<DPS versao="1.01">`, `infDPS@Id` = `DPS`+cMun(7)+tipoInscFed(1)+InscFed(14)+serie(5)+nDPS(15) = 45 chars. Assinatura `ds:Signature` enveloped + C14N, RSA-SHA1 (padrão SPED — não trocar p/ SHA256 sem o gov aceitar).
- Envelope JSON do transporte ASSUMIDO (campos exatos no Swagger mTLS, fora do texto extraível): `{ "dpsXmlGZipB64": <b64> }`; `POST {UrlBase}/nfse`, `GET {UrlBase}/nfse/{chave}`. **Validar em Produção Restrita**; ajustar nomes de campo aqui se divergir.
- `dhEmi` (DPS) e `dhEvento` (cancelamento) formatados em offset FIXO **-03:00** (R2): `new DateTimeOffset(utc, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(-3))` → `yyyy-MM-ddTHH:mm:sszzz`. Offset fixo (não tz-db) pois Brasil sem DST desde 2019; evita data/competência errada em instante próximo da meia-noite UTC. CAVEAT: formato exato aceito pelo layout SEFIN (separador `T`, `:` no offset, ausência de fração) a CONFIRMAR em Produção Restrita.
- Anti-dupla-emissão (R6): emit-then-commit NÃO é atômico (gov autoriza → processo morre antes do `CommitAsync` → nota fica `Pendente` → retry re-transmite). Protegido pela dedup SEFIN por **série+nDPS**: `NumeroDps` é determinístico (`NumeroDpsEstavel()`) e persistido na criação → retry envia o MESMO DPS → gov devolve a nota existente (idempotente), não emite 2ª. Decisão consciente (3 eixos): sem estado `Transmitindo` extra; a idempotência autoritativa do gov é a salvaguarda. + UNIQUE + chave outbox + estado terminal garantem 1 nota.
