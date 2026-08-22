# specification-agents — gateway de agentes / API interna `/internal/agents/v1` (forzion.tech)
DOC PARA AGENTES. Fonte de verdade da API interna consumida pelo **Forzion Agent Gateway** (repo externo `forzion.tech-agents`): payload canônico da assinatura HMAC, ordem de verificação, rotação de chave, vocabulário de erro, gotcha de borda e roteiro das fatias. Formato denso, agent-oriented. Cross-ref: [specification-security] (§5 segredos, §7 assinatura, §9.1 rotação), [specification-backend] (rate-limit/endpoints internos), [specification-infrastructure] (nginx edge, `.env` da VM), [specification-observability] (log de rejeição), [specification-db]/[specification-lgpd] (fatias 1-4).

## MANUTENÇÃO DESTE ARQUIVO
Atualizar quando mudar: payload canônico ou qualquer campo que entre nele, ordem de verificação, janela de timestamp, formato do header de assinatura, nomes/semântica dos segredos `Agents:Hmac:*`, tabela domínio→`code`, composição da tag `agents-ready`, policy de rate limit do grupo, ou quando uma fatia (1-4) for entregue. Contrato canônico do wire = `.specs/contracts/forzion-internal-api.v1.yaml` (repo `forzion.tech-agents`, decisão D31) — este arquivo descreve a IMPLEMENTAÇÃO deste lado, não substitui o YAML.

## 1. O QUE EXISTE HOJE
Fatia 0 (`agents-f0-hmac`), fatia 1 (`agents-f1-perfil-publico`) e a borda de agente da fatia 2 (`agents-f2-leads`) entregues: grupo de rota + verificação de assinatura + envelope de erro + rate limit + `GET /health` + `GET business-info` + `GET services` (§6-A) + `POST leads` (§6-B). Os outros dois caminhos do contrato (`GET availability`, `POST booking-requests`) **não existem** (404 de rota, sem stub, sem 501) — são as fatias 3-4 (§8).

| Componente | Local | Papel |
|---|---|---|
| `AgentEndpoints` | `Api/Endpoints/Agents/AgentEndpoints.cs` | grupo `/internal/agents/v1` + `GET /health` + `GET business-info` + `GET services` |
| `AgentErrorCode` / `AgentProblem` | `Api/Endpoints/Agents/` | os 9 `code` do contrato + envelope RFC 9457 |
| `AgentExceptionFilter` | `Api/Endpoints/Agents/AgentExceptionFilter.cs` | captura exceção não tratada em QUALQUER endpoint do grupo, loga real (Error) server-side, responde `503 dependency_unavailable` — nunca o formato PT-BR do `GlobalExceptionHandler` |
| `CanonicalPayload` | `Api/Endpoints/Agents/Hmac/` | montagem do payload canônico (puro) |
| `HmacSignatureVerifier` | `Api/Endpoints/Agents/Hmac/` | parse `v1=`, HMAC, constant-time, 2 chaves, janela |
| `HmacSignatureFilter` | `Api/Endpoints/Agents/Hmac/` | I/O: cap de corpo, buffer verificado, headers, `Problem`, log |
| `AgentsHmacOptions` / `AddAgentsHmac` | `Api/Configuration/AgentsHmacExtensions.cs` | ligação de `Agents:Hmac` + fail-closed no boot |
| policy `agents` / tag `agents-ready` | `Api/Extensions/DependencyInjectionExtensions.cs` | 120/min por IP; `db`+`schema` |
| `AgentTenantGuard` | `Application/UseCases/Agents/AgentTenantGuard.cs` | guard de colapso 404 compartilhado (not-found ∪ inativo ∪ não-publicado) — 1 única implementação para todo handler de leitura não divergir por engano |
| `ObterBusinessInfoHandler` | `Application/UseCases/Agents/BusinessInfo/` | projeta `Treinador`+`PerfilPublico` publicados pro DTO de wire `BusinessInfo` |
| `ListarServicosHandler` | `Application/UseCases/Agents/Services/` | projeta `Pacote`s públicos (+ filtro opcional `category`) pro DTO de wire `Service` |

**Proteção vive no GRUPO, não no endpoint** (`MapGroup(...).AddEndpointFilter<AgentExceptionFilter>().AddEndpointFilter<HmacSignatureFilter>().RequireRateLimiting("agents").ExcludeFromDescription()`). `AgentExceptionFilter` é registrado ANTES do `HmacSignatureFilter` na cadeia de `.AddEndpointFilter<>()` — outermost wrapper, primeiro a executar, cobre até uma exceção hipotética dentro do próprio filtro HMAC. Endpoint acrescentado ao grupo nasce assinado, com rate limit próprio, coberto contra exceção não tratada e fora do OpenAPI, sem declarar nada. Esquecer anotação deixa de ser uma forma de abrir a superfície.

## 2. PAYLOAD CANÔNICO
```
{MÉTODO_MAIÚSCULO}\n{caminho + query}\n{sha256_hex_minúsculo(corpo)}\n{timestamp_unix_segundos}
```
- Separador `\n` (LF) puro, **sem `\r`**.
- Método em MAIÚSCULO (`GET`, `POST`).
- Caminho **inclui o prefixo `/internal/agents/v1`** e a query **exatamente como recebida** — sem reordenar, sem deduplicar, sem decodificar. `?b=2&a=1` entra nessa ordem; parâmetro repetido entra repetido.
- Corpo ausente ⇒ SHA-256 da string vazia (`e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`), **nunca** string vazia.
- Timestamp unix em SEGUNDOS, o mesmo valor do header `X-Forzion-Timestamp`. **O payload usa a forma decimal MÍNIMA do número, não o texto literal do header**: o backend faz `long.TryParse` do header e re-serializa (`ToString(InvariantCulture)`) ao montar o payload — sem zero à esquerda, sem sinal, sem espaço. Contrato implícito de formato: **o gateway deve escrever o header JÁ nessa forma** e assinar o MESMO valor que escreveu; normalizar não é responsabilidade do backend. Sinal e espaço já são rejeitados no parse (`NumberStyles.None` ⇒ `401 signature_invalid`), então o único caso de divergência silenciosa é zero à esquerda: header `01787000000` faz o backend assinar `1787000000` e, se o gateway tiver assinado o literal, o 401 é mudo.
- Assinatura = HMAC-SHA256 do payload, hex minúsculo, no header `X-Forzion-Signature` no formato `v1=<hex>`. `v1` é versão de **esquema**, não identificador de chave.

Exemplo literal do contrato (`GET /internal/agents/v1/tenants/7f3a.../services?category=aulas`, ts `1787000000`):
```
GET
/internal/agents/v1/tenants/7f3a.../services?category=aulas
e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
1787000000
```

Vetores de regressão: `forzion.tech.Tests/Api/Agents/CanonicalPayloadTests.cs` (exemplo literal, hash da string vazia asserido contra a constante escrita à mão, query fora de ordem, parâmetro repetido, ausência de `\r`).

## 3. ORDEM DE VERIFICAÇÃO (D4 — assinatura ANTES da janela)
1. Corpo lido uma vez sob `LimitedStream` de **64 KB** ⇒ atingiu o cap ⇒ `400 validation_failed` (sem hashear o excedente). Cap **exclusivo**: 65.535 bytes passa, **65.536 bytes exatos já rejeitam** — o `LimitedStream` lança quando o saldo zera e `CopyToAsync` sempre emite mais um `Read`. Herdado do cap de webhook, mantido por consistência.
2. `X-Forzion-Timestamp` ausente, vazio ou não-parseável ⇒ `401 signature_invalid`. **Fail-closed**: o enforcement NÃO é guardado por `if (parseou) { valida }`.
3. `X-Forzion-Signature` sem `v1=`, com prefixo diferente ou hex inválido ⇒ `401 signature_invalid`.
4. HMAC recomputado sobre o payload; comparação `CryptographicOperations.FixedTimeEquals`, precedida de checagem de comprimento. **`FixedTimeEquals` RETORNA `false` em spans de tamanhos diferentes — não lança**; a checagem é defesa redundante, não pré-condição (o comentário oposto em `Api/Extensions/InternalApiKeyValidator.cs` está incorreto). Nenhuma chave confere ⇒ `401 signature_invalid`.
5. **Só então** `|agora − ts| > 300s` ⇒ `401 timestamp_out_of_window`.
6. O buffer verificado é reinstalado como `Request.Body` — o handler consome **os mesmos bytes** que foram hasheados, nunca uma releitura do socket.

**Porquê desta ordem:** o timestamp é campo ASSINADO. Antes de a assinatura conferir ele é entrada hostil, e responder `timestamp_out_of_window` a quem não tem a chave entrega o relógio do servidor a chamador não autenticado. Custo aceito: com segredo errado E relógio torto, o operador vê `signature_invalid` primeiro e só descobre o desvio depois de corrigir o segredo. O contrato exige que os dois `code` sejam **distintos** (são), não uma ordem específica.

Rejeição nunca é exceção — resultado tipado (`Valida` / `AssinaturaInvalida` / `TimestampForaDaJanela`). Qualquer exceção inesperada na verificação também rejeita; nunca vaza 500 nem passa adiante.

**Replay dentro da janela de 300s é possível por desenho** — o contrato não define nonce. Hoje o único endpoint é `GET /health` idempotente. Nos writes das fatias 2/4 a mitigação é a idempotência (`idempotencyKey` única por `(TreinadorId, chave)`), não a auth. Store de nonce é aditivo e não muda o contrato, se virar necessário.

**Hex da assinatura aceita maiúsculas** (`Convert.FromHexString` aceita as duas caixas). O contrato diz minúsculo; ser estrito criaria mais um 401 indiagnosticável e não é propriedade de segurança — o HMAC é o mesmo. Residual consciente.

## 4. SEGREDO E ROTAÇÃO (D6)
| Chave | Origem | Obrigatória |
|---|---|---|
| `Agents:Hmac:SecretAtual` | env/`.env` root-only da VM (`AGENTS_HMAC_SECRET_ATUAL`) | **sim** em `Production`/`Homolog` (boot falha) |
| `Agents:Hmac:SecretAnterior` | idem (`AGENTS_HMAC_SECRET_ANTERIOR`) | não — só durante a janela de rotação |

- Mín. **32 bytes UTF-8**. Gerar: `openssl rand -base64 64`. **DISTINTO por ambiente** ([specification-security] §10.2) — segredo de homolog não pode assinar requisição de produção; é essa separação que impede o gateway de homolog escrever em prod.
- `Production`/`Homolog` sem `SecretAtual` ⇒ `InvalidOperationException` no boot (paridade com `Auth:JwtSecret`/`Mfa:EncryptionKey`). Fora desses ambientes o boot segue e **o grupo continua fechado**: segredo ausente ⇒ `401`, nunca bypass. Nenhum ambiente tem exceção à verificação.
- `SecretAnterior` NÃO tem checagem de tamanho no boot: durante a rotação ela é a chave que está SAINDO, e reprovar o boot por ela seria hostil ao próprio procedimento. Chave curta simplesmente não confere assinatura legítima nenhuma.
- O verificador tenta a atual e, falhando, a anterior. Sem `SecretAnterior` configurada, só a atual é aceita.

**Procedimento de rotação (sem janela de indisponibilidade)** — cadência 90d, alinhada a `Internal:ApiKey` ([specification-security] §9.1):
1. Gerar valor novo. `AGENTS_HMAC_SECRET_ANTERIOR` = valor atual; `AGENTS_HMAC_SECRET_ATUAL` = valor novo. Redeploy do backend.
2. Gateway (repo `forzion.tech-agents`) passa a assinar com o valor novo. Durante a janela as duas chaves conferem.
3. Confirmado o corte, remover `AGENTS_HMAC_SECRET_ANTERIOR` do `.env` + redeploy. A partir daí a chave velha não confere mais.

Ordem invertida (trocar o gateway antes do backend aceitar a chave nova) derruba a integração inteira — é a única forma de errar este procedimento. Vazamento confirmado ⇒ rotação IMEDIATA, pulando o passo de convivência (aceitar a chave vazada durante a janela é o que se quer evitar).

## 5. ERROS — TABELA DOMÍNIO→`code` (D7)
Envelope `application/problem+json` (RFC 9457) com `type`/`title`/`status`/`code` sempre presentes. `type` = `https://forzion.tech/problems/{code}`. `title` em **inglês e estável**, fora da customização PT-BR global de `ProblemDetails` — o consumidor é máquina com contrato versionado. `detail` é **literal fixo por `code`**: sem segredo, assinatura, corpo, PII nem relógio do servidor. O motivo real vai só para log server-side.

| `code` | Status | Origem no domínio |
|---|---|---|
| `signature_invalid` | 401 | header ausente/vazio/não-parseável, prefixo ≠ `v1=`, hex inválido, HMAC não confere |
| `timestamp_out_of_window` | 401 | assinatura válida e `\|agora − ts\| > 300s` |
| `validation_failed` | 400 | corpo ≥ 64 KB; validação de request das fatias 1-4 |
| `tenant_not_found` | 404 | `tenantId` inexistente **ou** fora do escopo (colapso deliberado — não confirma existência) |
| `service_not_found` | 404 | reservado para consulta por serviço único (fatias 3/4 — `serviceId` referenciado em `GET availability`/`POST booking-requests`); **não emitido pela fatia 1** — `GET services` lista, nunca resolve um item, então não há 404 por serviço |
| `slot_not_found` | 404 | slot derivado que nunca existiu (fatia 3/4) |
| `slot_unavailable` | 409 | slot existia e lotou/sumiu entre a consulta e o write (fatia 4) |
| `idempotency_conflict` | 409 | mesma `idempotencyKey` com argumentos diferentes (fatias 2/4) |
| `dependency_unavailable` | 503 | `agents-ready` `Unhealthy`; agenda inconsultável (fatia 3) |

Mensagem interna PT-BR **nunca** vira `detail`. `code` NUNCA sai de `ToString()` de enum — o domínio é PT-BR e um rename interno quebraria o contrato em silêncio; o valor de wire é tabela explícita (`AgentErrorCodeExtensions`).

`503 dependency_unavailable` é resposta de primeira classe, não exceção não tratada: `GET /availability` que não consegue consultar a agenda responde `503` e **nunca lista vazia** — o gateway lê lista vazia como "não há vaga" e o agente diria ao consumidor que não existe horário quando a consulta falhou.

## 6. HEALTH — `GET /internal/agents/v1/health`
- Exige assinatura como qualquer outro caminho do grupo (o contrato lista `401` entre as respostas de `/health`); não é exceção.
- Agrega **só** a tag `agents-ready` = `db` + `schema`. Tag ADITIVA: os dois checks continuam em `ready`. Falha de `stripe`/`resend`/`whatsapp` não altera o resultado — o gateway só abre circuito por algo que ele realmente usa. Cada fatia acrescenta sua dependência à tag quando passar a precisar dela.
- `Healthy` ⇒ `200 {"status":"healthy","checkedAt":<ISO 8601>}`; `Degraded` ⇒ `200 "degraded"`; `Unhealthy` ⇒ **`503` + `Problem{code=dependency_unavailable}`**. Consequência registrada: o valor `unhealthy` do enum `HealthStatus` do contrato é **inalcançável por desenho** — o 503 é o sinal mais forte e é o que o contrato lista.
- Resposta não expõe nome de check, descrição, mensagem de exceção nem dado de infra: só o status agregado e o instante.
- `checkedAt` vem do `TimeProvider` do servidor, **nunca ecoado do cliente**.

## 6-A. ENDPOINTS DE LEITURA — FATIA 1 (`GET business-info`, `GET services`)

Ambos vivem no MESMO grupo `/internal/agents/v1`, herdam HMAC + `AgentExceptionFilter` + rate limit sem declarar nada. `tenantId` = `Treinador.Id` (D1).

**Bind de `tenantId`**: manual como `string` + `Guid.TryParse` no próprio endpoint — NUNCA o model-binding automático do ASP.NET Core para `Guid` (esse erraria fora do envelope `AgentProblem`, quebrando o contrato). Malformado ⇒ `400 validation_failed`.

**Guard de colapso 404 (`AgentTenantGuard.EstaPublicado`)**: `treinador is null || treinador.Status != Ativo || !treinador.PerfilPublico.IsPublicado` ⇒ **1 única branch**, `tenant_not_found` — tenant inexistente, treinador inativo e perfil não-publicado respondem byte-a-byte idêntico (mesmo `detail` fixo). Divergir confirmaria a um chamador não autorizado que o tenant existe (mesma defesa IDOR do vínculo aluno-treinador). Compartilhado pelos dois handlers — nunca reimplementado por handler.

### `GET /tenants/{tenantId}/business-info`
- `200` com `BusinessInfoResponse`: `name` (=`PerfilPublico.NomeFantasia`), `modalities` (ver abaixo), `address`/`openingHours`/`policies` **omitidos do JSON** (`JsonIgnore(WhenWritingNull)`, não serializados como `null` nem `{}`) quando o dado correspondente é nulo/vazio. `plans` **nunca populado — nem existe como propriedade do DTO** (fora de escopo, ver `spec.md` da fatia).
- `modalities` = `Categoria` distintos (case-insensitive, preserva a 1ª grafia encontrada) dos `Pacote` do treinador com `IsPublico=true`. Lista vazia (`[]`) quando não há nenhum pacote público — **`200`, nunca `404`**: treinador publicado sem catálogo é estado válido (AGF1-05).
- 404 pelo guard de colapso acima.

### `GET /tenants/{tenantId}/services`
- `200` com array de `ServiceResponse` (um por `Pacote` público): `id`=`Pacote.Id.ToString()`, `name`, `category`, `description`, `durationMinutes`, `price`={`amount`=`Pacote.Preco`, `currency`="BRL"} **sempre presente mesmo quando `Preco=0`**, `trialAvailable`.
- Query opcional `category`: filtro case-insensitive (`OrdinalIgnoreCase`) sobre `Pacote.Categoria`, aplicado em memória após `IPacoteRepository.ListarPublicosPorTreinadorAsync` já ter restringido a `IsPublico=true` — filtro de categoria NUNCA contorna o filtro de público. `> 100` caracteres ⇒ `400 validation_failed` (validado no endpoint, antes de chamar o handler).
- Catálogo vazio, com ou sem filtro `category` — `200 []`, **nunca `404`/`503`** só por catálogo vazio.
- 404 pelo mesmo guard de colapso do `business-info` (`AgentTenantGuard`, não duplicado).

**Lado de escrita (fora do grupo de agentes, autenticado por JWT do treinador — quem alimenta os dois `GET` acima):**
- `GET/PUT /treinador/perfil-publico`: lê/grava `PerfilPublico` inteiro (`NomeFantasia`, `Endereco`, `Politicas`, `HorariosFuncionamento`, `IsPublicado`). O `PUT` sempre reenvia a lista completa de horários — `PerfilPublico.SubstituirHorarios` faz replace atômico (valida tudo antes de mutar; um item inválido não deixa a coleção pela metade).
- `POST/PATCH /treinador/pacotes`: ganharam `categoria`/`duracaoMinutos`/`trialDisponivel`/`isPublico` no payload — `isPublico=true` sem `categoria` falha (`pacote.categoria_obrigatoria_para_publico`), mesma regra que `GET business-info` depende para derivar `modalities`.
- Sem esses dois pontos de escrita o `PerfilPublico`/catálogo público do domínio existe mas é inacessível ao treinador — gap real encontrado pelo Verifier da fatia 1 (UI já existia sem endpoint por trás) e fechado na mesma fatia.

## 6-B. ENDPOINT DE ESCRITA COM CORPO — FATIA 2 (`POST leads`)

`POST /internal/agents/v1/tenants/{tenantId}/leads` é o primeiro endpoint do grupo com parâmetro de corpo. `RegistrarLeadAgenteHandler` (`Application/UseCases/Agents/Leads/`) implementa a ordem: consent → limites → contato → `AgentTenantGuard.EstaPublicado` → idempotência → persistir. Resposta projeta `StagedLead {leadId, source: "agent", status: "pending"}` por tabela explícita (nunca `ToString()` de enum) — literal constante mesmo no caminho idempotente (o lead pode já ter mudado de status internamente; o wire só conhece `pending`).

- **GOTCHA `[FromBody]` em tipo complexo quebra a assinatura — CANÔNICO, vale para qualquer endpoint futuro do grupo com corpo.** Minimal API resolve parâmetros ANTES da cadeia de `IEndpointFilter` rodar (`EndpointFilterInvocationContext.Arguments` já vem populado quando `InvokeAsync` do filtro mais externo começa — confirmado no doc oficial: filtros leem `context.GetArguments()[0]` já materializado, síncrono, sem await). Um parâmetro `[FromBody] TipoComplexo` faz o model binder LER E DRENAR `HttpContext.Request.Body` nesse momento — antes do `HmacSignatureFilter` (mais interno na cadeia) sequer rodar. O filtro então hasheia um corpo já vazio, e toda requisição assinada corretamente cai em `401 signature_invalid` mudo (o desalinhamento é inteiramente do lado do servidor; o gateway não tem como saber). Reproduzido e corrigido nesta fatia: nenhum teste do grupo cobria essa combinação porque as fatias 0-1 só têm `GET`s (sem corpo) e o único endpoint de teste com corpo do grupo (`HmacSignatureFilterTests`) usa `HttpContext` cru, não `[FromBody]`.
  **Fix obrigatório para todo endpoint futuro do grupo com corpo**: assinatura do handler recebe `HttpContext httpContext` (não o DTO tipado), e o corpo é lido MANUALMENTE dentro do handler via `httpContext.Request.ReadFromJsonAsync<T>(cancellationToken)` — nesse ponto o `HmacSignatureFilter` já rodou e já substituiu `Request.Body` pelo buffer que ele mesmo hasheou (`requisicao.Body = corpo;`), então a leitura do handler vê exatamente os bytes verificados. `JsonException` na desserialização ⇒ `400 validation_failed`.

## 7. BORDA / RATE LIMIT / SUPERFÍCIE
- **GOTCHA nginx (D5) — NÃO É O CAMINHO ATIVO (decisão 2026-08-19, AD-016).** O design original (D5) presumia o gateway atravessando o nginx público; **decisão do usuário inverteu essa premissa**: `location /internal/ { return 404; }` (F3) fica INTOCADO — o gateway alcança por rede privada, mesma postura dos crons ([specification-infrastructure] §Workflows de billing/cron), nunca pela borda pública. Isolamento de rede + HMAC, não HMAC como única barreira de um endpoint internet-facing. Mecanismo exato de acesso privado (peer Tailscale dedicado, reuso do padrão SSH+`docker compose exec`, outro) é provisionamento de infra ainda não decidido — fora do escopo desta fatia. O gotcha de reescrita/normalização do payload (abaixo) permanece TECNICAMENTE válido caso a decisão mude no futuro, mas não é risco ativo hoje. **AGF0-37** ("verificação através do caminho de rede real") continua ⏳ pendente — bloqueado por essa decisão de infra, não só por deploy ainda não ter ocorrido (lição L-007 é o caso normal; aqui há também o provisionamento em aberto).
- **Request-target cru (`RawTarget`) é a fonte do caminho assinado, com fallback.** `HmacSignatureFilter` lê `IHttpRequestFeature.RawTarget` (`contexto.Features.Get<IHttpRequestFeature>()`) e o usa como caminho+query do payload quando presente. Documentado pelo ASP.NET Core como "the raw path and full query… has not been UrlDecoded" — é o único acesso aos bytes da wire. **NÃO usar `requisicao.Path + requisicao.QueryString`**: o operador entre `PathString`/`QueryString` chama `ToUriComponent()` em `Path`, que RE-NORMALIZA o percent-encoding do CAMINHO (`%7E` vira `~`) — `PathString.FromUriComponent` já decodifica no parse do Kestrel (documentado: "fully decoded… except `%2F`"), e `ToUriComponent()` reconstrói a partir do valor já decodificado. `Request.Path` sozinho tem o mesmo problema, pela mesma razão. **A query não é afetada por essa mutação especificamente** — `QueryString.ToUriComponent()` não decodifica/recodifica percent-encoding igual a `PathString` (mutação confirmada por teste: reverter para o operador não derruba nenhum teste de query); o `%7E`→`~` observado num probe inicial desta investigação veio do `System.Uri` do CLIENTE de teste normalizando antes de sair (gotcha do harness, não do servidor — `%7E` não serve de sonda de fidelidade de wire via `HttpClient`, usar `%2F`, que sobrevive). Ainda assim, `RawTarget` é a fonte certa: é o único caminho que também preserva o CAMINHO cru, e é isso que protege as fatias 1-4 se algum dia um path param deixar de ser UUID.
  **Fallback**: host que não popula `RawTarget` (`TestServer` in-memory deixa vazio — confirmado empiricamente) cai em `Path.Value + QueryString.Value`, concatenação direta das strings. Preserva a query (que já não corria risco real via o operador) e mantém o caminho decodificado — aceitável só porque nenhum host de produção fica nesse ramo E os 6 endpoints do contrato só têm `tenantId` (UUID) como path param.
  Sensor: `HmacSignatureFilterTests` injeta `RawTarget` sintético via middleware (simula o Kestrel real) e prova que ele vence sobre `Path`/`QueryString`; testes com `RawTarget` vazio travam o fallback (ordem de query, ausência de query, percent-encoding de query).
  **Borda**: dado o AD-016, o `location /internal/agents/v1` do nginx NÃO será aberto — nota mantida por segurança futura, caso a decisão seja revisitada: um `location` desse tipo PRECISARIA usar `proxy_pass http://<upstream>:8080$request_uri;`, o mesmo padrão já usado em `location /webhooks/` (`nginx/nginx.conf`). `$request_uri` preserva a forma crua/não-decodificada do request-target; qualquer outra forma de `proxy_pass` (com URI de destino) faz o nginx normalizar o caminho e quebra `RawTarget` ponta-a-ponta em silêncio.
- **`/internal/` é inalcançável de fora**: nginx `location /internal/ { return 404; }` nos dois vhosts + bloqueio por primeiro segmento normalizado no proxy BFF do Next (`/api/backend/internal/*` ⇒ 404 sem encaminhar, cobre `Internal`, `%69nternal`, `%2569nternal`). O prefixo de agentes tem teste de regressão próprio ([specification-security] §1).
- **Rate limit: policy `agents`, fixed window 120/min por IP** (`RateLimitPartitionKeys.KeyFromIp`), registrada nos **dois** ramos do `AddRateLimiter` — policy referenciada e não registrada quebra em runtime, não no build. Isolada da `internal` (5/min, que serve os crons): uma conversa do agente dispara 3-4 chamadas e leria o 429 como indisponibilidade. **Particionar por identidade é impossível**: `UseRateLimiter()` é middleware e roda ANTES de qualquer endpoint filter, logo antes da assinatura conferir.
  **LIMITAÇÃO CONHECIDA (consequência do AD-016), registrada e NÃO corrigida nesta fatia.** A partição por IP pressupunha chamadores diversos vindos da borda pública; com o gateway alcançando por rede privada como peer único, todo o tráfego de TODOS os tenants chega da MESMA origem — o balde deixa de ser "por chamador" e vira, na prática, um teto ÚNICO compartilhado por toda a plataforma, com conversas de tenants distintos competindo na mesma janela. Não há fix limpo dentro desta fatia (particionar por tenant exigiria identidade, indisponível antes do HMAC, pela razão do parágrafo acima). Candidata a recalibração quando houver dado real de volume do gateway — mesmo critério aplicado às demais policies no `spec.md` (Out of Scope): sem medição, número novo seria chute.
- **Fora do OpenAPI**: `ExcludeFromDescription()` no grupo. O documento versionado (`docs/api/openapi.v1.json`, gate de drift no pre-commit e no CI) não ganha nenhum caminho `/internal/agents/`.
- **Log de rejeição**: `LogWarning` com motivo (`code`), método e caminho — nunca assinatura, segredo ou corpo, em nenhum nível. Sem esse log a divergência de payload canônico (o maior risco do handover) fica indiagnosticável. Cross-ref [specification-observability].

## 8. FATIAS — ROTEIRO
`tenantId` = **`Treinador.Id`** (D1). O treinador É o negócio que o agente representa; o escopo do contrato vira filtro por `TreinadorId`, a mesma fronteira de isolamento que o repo já usa. Wire é uuid opaco ⇒ se multi-unidade aparecer, uma entidade `Tenant` entra com `Treinador` apontando pra ela sem mudar o contrato.

| # | Fatia | Entrega | Endpoints | Design-review |
|---|---|---|---|---|
| 0 | `agents-f0-hmac` | HMAC + `Problem`/`code` + rate limit + grupo | `GET /health` | sim (auth) — **FEITA** |
| 1 | catálogo público | `PerfilPublico` novo + `Pacote` com `Categoria`/`DuracaoMinutos`/`TrialDisponivel`/`IsPublico` (nasce `false`) + UI do treinador | `GET business-info`, `GET services` | não — **FEITA** |
| 2 | leads | `Lead` + esteira completa (lista, filtros, histórico, conversão→aluno, métricas) + idempotência | `POST leads` | **sim** (PII/LGPD) |
| 3 | agenda | `JanelaAtendimento` + `BloqueioAgenda` + UI; slots DERIVADOS no read, `slotId` = hash determinístico de `(TreinadorId, PacoteId, inícioUTC)` | `GET availability` | não |
| 4 | agendamento | `SolicitacaoAgendamento` + esteira (confirmar→compromisso, recusar) | `POST booking-requests` | **sim** (concorrência) |

Invariantes do contrato que o schema não expressa (valem em TODAS as fatias): (1) nenhum write confirma nada — `POST` cria registro PENDENTE, confirmação é humana; (2) escopo de tenant é imposto pelo servidor, nunca pelo chamador; (3) **nenhum `GET` devolve PII** — só dado já público no site do tenant; (4) idempotência é honrada aqui (constraint no banco), não só aceita.

DTOs do contrato são projeções de borda (`Api/Endpoints/Agents/` + `Application/UseCases/Agents/`); **nenhum tipo do contrato entra em Domain**. Projeção explícita é o que garante a invariante 3: campo novo no agregado não vaza por default.

## 9. GATE EXTERNO
A definição de pronto declarada pelo handover é a **suíte de contrato T17** do repo `forzion.tech-agents`, apontada para o deploy de homolog. Não é entrega deste repo. O primeiro handshake real fecha também AGF0-37 (verificação através do nginx).
