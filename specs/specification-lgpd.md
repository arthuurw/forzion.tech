# specification-lgpd — privacidade: portabilidade, exclusão (anonimização), consentimento

DOC PARA AGENTES. Fonte de verdade dos direitos do titular (LGPD) no forzion.tech: exportação de dados (portabilidade, art. 20), exclusão de conta por ANONIMIZAÇÃO (direito ao esquecimento, art. 18), e consentimento de cookies (incl. dado sensível de saúde, art. 11). Formato denso. Consultar antes de alterar export/anonimização, banner de consentimento, ou tratamento de PII. Cross-ref: [specification-security] (gate de consentimento → Sentry, postura de segurança), [specification-frontend-ui] (ConsentBanner), [specification-observability] (Sentry/RUM gateado por consentimento).

## MANUTENÇÃO
- Atualizar NA MESMA TAREFA de mudança em: campos PII (export/anonimização), endpoints LGPD, banner/consentimento, gate do Sentry, auditoria, regra de bloqueio.
- Coluna/tabela → [specification-db]; endpoints → [specification-backend]; UI → [specification-frontend].

## DECISÕES (2026-05-29)
- **Exclusão = ANONIMIZAÇÃO irreversível e imediata** (não hard-delete). Retém registros transacionais/fiscais (pagamentos, assinaturas) — obrigação fiscal BR + FKs RESTRICT. Sem carência/purga agendada (MVP).
- **Solicitantes**: self-service (titular aluno/treinador via `/perfil`) + admin (suporte).
- **Consentimento**: essenciais (auth httpOnly) sempre; analytics (Sentry) opt-in, default OFF.
- **Auditoria**: via `logs_aprovacao` (enum `ExportacaoDados`, `AnonimizacaoConta`).
- **Regra**: treinador com vínculos ativos → exclusão BLOQUEADA (offboarding primeiro).
- **D-RET (retenção fiscal, 2026-06-06)**: registros transacionais retidos por **5 anos** após `DataCancelamento` da assinatura (referência fiscal BR). Purga = ANONIMIZAÇÃO via **job mensal** (GitHub Actions `lgpd-purge.yml`, cron `0 3 1 * *`): lista elegíveis (`GET /internal/lgpd/contas-elegiveis`) → loop `DELETE /internal/lgpd/contas/{id}` (sleep **15s** — rate-limit interno = 5 req/min/IP). **Tolerância por-conta (CR#2)**: o loop captura o HTTP status por conta (sem `curl -f`), loga ids que falham, processa TODAS e só sai non-zero ao final se houve ≥1 falha — um 429/422 isolado NÃO aborta o lote. Conta elegível = `AnonimizadaEm IS NULL` E teve ≥1 assinatura, TODAS Canceladas com `DataCancelamento < agora-5anos` (aluno+treinador) — `ListarElegivelPurgaLgpdAsync` usa uma query única com subqueries `EXISTS`/`NOT EXISTS` (CR#8 — antes 2 round-trips + `IN(...)`; EF LINQ schema-agnostic, sem `FromSqlRaw`). Ambos endpoints internos: auth `INTERNAL_API_KEY` (header `X-Internal-Key`), NÃO admin-JWT — o `DELETE /admin/contas/{id}/lgpd` (admin-JWT) não é usado pelo job (CI não tem JWT admin). Anonimização reusa `AnonimizarContaHandler` (idempotente).
- Pendências jurídicas (copy do banner/política, contato DPO): placeholders/documentação; não bloqueiam código.

## MODELO DE ANONIMIZAÇÃO (Domain)
Métodos idempotentes (Result), disparam scrub de PII e mantêm o registro:
- `Conta.Anonimizar(agora)`: `Email` → token irreversível `anon+{guid:N}@anonimizado.local` (via `Email.Criar`, normalizado), `PasswordHash` → vazio (login impossível), `AnonimizadaEm = agora`, email não-verificado. Emite `ContaAnonimizadaEvent(ContaId, TipoConta, OcorridoEm)`. Idempotente via `AnonimizadaEm != null`.
- `Aluno.Anonimizar(agora)`: scrub nome (→"Usuário anonimizado"), email(opt), telefone, e **anamnese SENSÍVEL** (finalidade, foco_treino, nivel_condicionamento, limitacoes_fisicas, doencas, observacoes_adicionais, dias/tempo). Idempotente via flag PERSISTIDA `Anonimizado` (NÃO via nome — um usuário real chamado "Usuário anonimizado" ainda tem PII scrub-ada na 1ª chamada).
- `Treinador.Anonimizar(agora)`: scrub nome (→sentinela), telefone. Idempotente via flag PERSISTIDA `Anonimizado`.
- Coluna `contas.anonimizada_em` (tstz null) — migration `AdicionarAnonimizadaEmContas`. Aluno/Treinador: coluna `anonimizado` (bool NN default false) — migration `AdicionarAnonimizadoEmAlunosETreinadores` (DOM-02; antes era campo transiente não mapeado → guard ilusório, voltava `false` no reload). Idempotência cross-sessão garantida tanto pela flag persistida de cada agregado quanto pelo handler via `conta.AnonimizadaEm` (não re-chama `Anonimizar` em conta já anonimizada).

## FLUXO DE EXCLUSÃO (`AnonimizarContaHandler`, transação única)
1. Carrega conta (NotFound se ausente; idempotente se já anonimizada).
2. Resolve perfil. **Treinador com vínculos ativos → `Error.Business("offboarding_necessario")`, aborta.**
3. Captura email/telefone antigos (scrub de logs).
4. `conta.Anonimizar` + `aluno|treinador.Anonimizar` + read-model `assinantes` (`IAssinanteRepository.AnonimizarPorAlunoIdAsync`).
5. Scrub recipient dos delivery logs: `IEmailDeliveryLogRepository.AnonimizarPorEmailAsync` + `IWhatsAppDeliveryLogRepository.AnonimizarPorTelefoneAsync` (recipient já é hash HMAC em repouso — §DB/PII; anonimização hasheia o email/telefone, dá match determinístico e troca o hash por `(anonimizado)`; coluna `payload` NÃO existe — dropada por leak/zero-consumidor em PRIV-02). `IMensagemSuporteRepository.ExcluirPorContaIdAsync` (ExecuteDelete): mensagens de suporte são texto livre (assunto/descrição) = PII potencial e sem valor fiscal → **DELETE** (não scrub), all-or-nothing na mesma transação. `IRefreshTokenFamilyRepository.ExcluirPorContaIdAsync` (ExecuteDelete, cascade nos `refresh_tokens`): purga as sessões de refresh do titular (`Rotulo` = device/user-agent = PII potencial) — também **DELETE**, mesma transação.
6. `PasswordHash` vazio + `AnonimizadaEm` bloqueiam login FUTURO. Refresh purgado ⇒ qualquer renovação cai em 401 (sessão morta). O access JWT já emitido segue válido até expirar (stateless, curto — 15min/10min); self-service revoga o próprio `jti` aqui mesmo (`EnfileirarRevogacaoDoTitularSeSelf`, mesma transação).
7. `LogAprovacao.Registrar(AnonimizacaoConta, ...)`.
8. `CommitAsync` único (despacha `ContaAnonimizadaEvent`). **RETÉM** pagamentos/assinaturas/logs (sem PII direto).

## FLUXO DE EXPORTAÇÃO (`ExportarDadosPessoaisHandler`)
- DTO `DadosPessoaisExport` versionado (`Versao`), seções: conta, perfil (aluno OU treinador), anamnese (aluno), vínculos, assinaturas, pagamentos, pacotes (treinador), treinos/fichas, execuções, progressão, delivery logs do titular (recipient pseudonimizado — hash HMAC, não o e-mail/telefone cru; §DB/PII). **Só dados do titular** (zero terceiros). **Refresh token hashes DELIBERADAMENTE excluídos**: credencial de segurança (SHA-256), não dado pessoal portável (OWASP — não exportar segredos). Agrega por `ContaId` via repos existentes. Registra `LogAprovacao(ExportacaoDados)`.
- **Negociação de formato** via query param `?formato=xlsx|json` (default `json`). Handler reaproveitado; `LogAprovacao(ExportacaoDados)` preservado em ambos os formatos.
  - `json`: retorna `DadosPessoaisExport` serializado (200 application/json).
  - `xlsx`: renderizado por `IDadosPessoaisExcelRenderer` (Infrastructure, ClosedXML); retorna `FileContentResult`, `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `filename: meus-dados.xlsx`.

### ESTRUTURA DO WORKBOOK EXCEL (mapa seção→aba)
10 abas, nesta ordem:

| # | Aba | Conteúdo |
|---|-----|----------|
| 1 | Conta | Campos da entidade `Conta` |
| 2 | Perfil | `Aluno` (todos os campos incl. anamnese) OU `Treinador` — mutuamente exclusivos |
| 3 | Vínculos | Vínculos aluno↔treinador do titular |
| 4 | Assinaturas | Assinaturas do titular |
| 5 | Pagamentos | Pagamentos do titular |
| 6 | Pacotes | Pacotes do treinador (vazia se aluno) |
| 7 | Treinos | Fichas de treino |
| 8 | Execuções | Execuções de exercícios |
| 9 | Logs E-mail | Delivery logs de e-mail |
| 10 | Logs WhatsApp | Delivery logs de WhatsApp |

- **Aba vazia**: criada com linha de cabeçalho (sem dados) — garante completude LGPD art. 18 IV.
- **Formatação**: datas em pt-BR (`dd/MM/yyyy HH:mm`), valores monetários numéricos, GUIDs como strings.

## ENDPOINTS
| Método/Rota | Auth | Notas |
|-------------|------|-------|
| GET `/conta/lgpd/exportar?formato=xlsx\|json` | autenticado (self) | Exporta próprios dados; `formato` default `json`; xlsx → `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` + `meus-dados.xlsx` |
| DELETE `/conta/lgpd` | autenticado (self) | body `{senha}`; reconfirma senha (BCrypt) antes de anonimizar; rate-limit "write" |
| GET `/admin/contas/{id}/lgpd/exportar?formato=xlsx\|json` | SystemAdmin | Exporta dados do titular; mesmo comportamento de formato |
| DELETE `/admin/contas/{id}/lgpd` | SystemAdmin | anonimiza (sem senha) |
| GET `/internal/lgpd/contas-elegiveis` | `INTERNAL_API_KEY` (`X-Internal-Key`) | IDs elegíveis à purga (D-RET); job mensal |
| DELETE `/internal/lgpd/contas/{id}` | `INTERNAL_API_KEY` (`X-Internal-Key`) | anonimiza elegível (job); reusa `AnonimizarContaHandler` |
- Erros via `ToProblemResult` (`Error.NotFound`/`Business`→404/422). ⚠️ DELETE com body (`senha`) — alguns proxies removem body de DELETE; aceito por decisão.

## CONSENTIMENTO (frontend)
- `ConsentBanner` (`Aceitar todos` / `Só essenciais` / `Preferências` com toggle analytics). Categorias: **essencial** (sempre; auth httpOnly), **analytics** (Sentry, opt-in default OFF).
- Persistência: cookie `consent` versionado (`{v,analytics}`) + hook `useConsent`. Reabrir prefs em `/perfil`.
- **Gate Sentry**: `instrumentation-client.ts` só inicializa `@sentry/nextjs` se `consent.analytics === true` (default OFF até aceite). `ConsentProvider` monta o banner globalmente (root layout).

## FRONTEND — ações
- `/perfil` → seção "Privacidade (LGPD)": botões "Exportar Excel" e "Exportar JSON" (`baixarMeusDados(formato)`, default `xlsx`), "Excluir minha conta" (ConfirmDialog + senha → DELETE → logout/redirect), "Preferências de cookies". API em `lib/api/conta.ts` (`baixarMeusDados(formato='xlsx')`, `excluirConta(senha)`). Página de assinatura do aluno também expõe os dois botões de download.
- Admin (detalhe treinador/aluno, aba LGPD): exportar + anonimizar (ConfirmDialog destrutivo). `adminApi.exportarDadosConta(contaId)` / `anonimizarConta(contaId)`.

## DB / PII
- PII por tabela e escopo de scrub → ver `.specs/features/lgpd/spec.md` + [specification-db]. Sensível (saúde): anamnese do aluno. Retidos anonimizados: pagamentos, assinaturas_aluno, logs_aprovacao, treinos/execuções (ids). `mensagens_suporte` E `refresh_token_families`/`refresh_tokens` são DELETADAS (não retidas) — texto livre / credencial de sessão sem valor fiscal.
- **Delivery logs pseudonimizados em REPOUSO** (PRIV-02): `email_delivery_logs`/`whatsapp_delivery_logs` NÃO guardam recipient cru — só `recipient_*_hash` = **HMAC-SHA256 keyed** (`RecipientHasher`, hex-lower) do e-mail/telefone; coluna `payload` dropada (leak, zero consumidor). Chave `DeliveryLog:RecipientHashKey` (`DeliveryLogSettings`): **fail-closed em Production** (`InfrastructureExtensions` `ValidateOnStart` — prod SEM chave NÃO sobe); fora de prod cai no `DevDefaultKey`. **Prod exige chave DISTINTA da homolog** (reusar = correlação cross-ambiente). Lookup/scrub hasheiam o valor e dão match (determinístico); anonimização seta o hash → `(anonimizado)`. **Canonicalização write=read** (CR1, simetria obrigatória): o hash é SEMPRE sobre a forma canônica — `HashEmail` = `Trim().ToLowerInvariant()`; `HashTelefone` = E.164 via `PhoneNumberNormalizer` (mesma forma usada no envio Meta). Centralizada em `RecipientHasher` (2 métodos tipados, não um `Hash` cru) p/ ser impossível um call-site esquecer de normalizar — divergência write/read = lookup/scrub silenciosamente sem match. **Forward-only**: linhas hasheadas ANTES da canonicalização guardam só o hash antigo (não-canônico) e NÃO há recipient cru p/ re-hashear ⇒ permanecem não-correlacionáveis; sem migração de dados (aceito — o dado em repouso já é pseudônimo). Migration `DeliveryLogPseudonimizarRecipient` (dropa payload + converte recipient; linhas antigas scrub-purgadas). Cross-ref [specification-observability] §1 (PII em repouso) / [specification-security].

## CONSENTIMENTO DE SAÚDE (art. 11 — anamnese)
Anamnese do aluno (finalidade, foco_treino, nivel_condicionamento, limitacoes_fisicas, doencas) = dado sensível de saúde → exige consentimento específico e destacado.
- **Backend (`RegistrarAlunoHandler`)**: quando o cadastro coleta qualquer dado de saúde (`command.ColetaDadosSaude`), o validator exige `ConsentimentoDadosSaude == true` (FluentValidation, erro `consentimento_saude_obrigatorio`). Defense-in-depth — não confia só no checkbox do frontend.
- **Registro**: dentro da transação de cadastro grava `LogAprovacao.Registrar(TipoAcaoAprovacao.ConsentimentoAnamnese, ator=conta, alvo=conta, "Conta", timestamp = ConsentimentoDadosSaudeEm do cliente ?? agora, observacao="v1")`. Sem dado sensível no cadastro → sem consentimento exigido e sem log.
- Sem nova coluna/migration: reusa `logs_aprovacao` (enum text). Versão do termo no campo `observacao`.

## AUDITORIA
`logs_aprovacao` com `TipoAcaoAprovacao.ExportacaoDados` / `AnonimizacaoConta` / `ConsentimentoAnamnese` (text enum; sem migration). Registra quem (`realizado_por`) / quando / alvo (`entidade_id` + tipo `Conta`). **Atribuição da exportação** (`ExportarDadosPessoaisCommand(ContaId, RealizadoPorId)`): self (`/conta/lgpd/exportar`) ⇒ `realizado_por = alvo = titular`; admin (`/admin/contas/{id}/lgpd/exportar`) ⇒ `realizado_por = ContaId do admin`, `alvo = titular` (exportação por admin fica atribuída a ele). Auditoria OBRIGATÓRIA/fail-closed: `LogAprovacao` é commitado ANTES de devolver o export; falha ao registrar ⇒ export NÃO retorna. `ConsentimentoAnamnese` = consentimento art. 11 no cadastro do aluno (observacao = versão do termo).

## TESTES
- Backend: Domain (Anonimizar de Conta/Aluno/Treinador — PII some, idempotência, evento), Application (export agrega todas as seções sem terceiros; anonimização scrub + retém financeiro + bloqueio treinador-ativo + senha errada), endpoints (200/401/403). ~40 testes.
- Frontend: `ConsentBanner` (Sentry off sem consentimento, persistência), /perfil export/delete, admin actions; e2e `lgpd/{export-data,delete-account,consent-cookies}` verdes.

## PENDÊNCIAS / GOTCHAS
- **Jurídico**: copy do banner + política de privacidade + contato DPO = placeholders (validar com jurídico).
- **Retenção fiscal** (5 anos BR): RESOLVIDO em D-RET (job mensal `lgpd-purge.yml`). Ver §DECISÕES.
- **Carência/reversibilidade**: não há (anonimização imediata/irreversível). Avaliar soft-delete + purga agendada no futuro.
- **Export e telefone**: WhatsApp delivery logs casados por telefone do perfil; se o telefone mudou, logs antigos podem não casar (limitação do modelo — logs não têm conta_id).
- **Anonimização ≠ delete**: linha da conta permanece (anônima) — by design (fiscal + FKs RESTRICT).
- Referências: [specification-db] (`anonimizada_em`, enums), [specification-backend] (endpoints/Result), [specification-frontend] (banner/perfil), [specification-email]/[specification-whatsapp] (delivery logs).
