# specification-lgpd — privacidade: portabilidade, exclusão (anonimização), consentimento

DOC PARA AGENTES. Fonte de verdade dos direitos do titular (LGPD) no forzion.tech: exportação de dados (portabilidade, art. 20), exclusão de conta por ANONIMIZAÇÃO (direito ao esquecimento, art. 18), e consentimento de cookies (incl. dado sensível de saúde, art. 11). Formato denso. Consultar antes de alterar export/anonimização, banner de consentimento, ou tratamento de PII.

## MANUTENÇÃO
- Atualizar NA MESMA TAREFA de mudança em: campos PII (export/anonimização), endpoints LGPD, banner/consentimento, gate do Sentry, auditoria, regra de bloqueio.
- Vive em `specs/` (commitado). Coluna/tabela → [specification-db]; endpoints → [specification-backend]; UI → [specification-frontend].

## DECISÕES (2026-05-29)
- **Exclusão = ANONIMIZAÇÃO irreversível e imediata** (não hard-delete). Retém registros transacionais/fiscais (pagamentos, assinaturas) — obrigação fiscal BR + FKs RESTRICT. Sem carência/purga agendada (MVP).
- **Solicitantes**: self-service (titular aluno/treinador via `/perfil`) + admin (suporte).
- **Consentimento**: essenciais (auth httpOnly) sempre; analytics (Sentry) opt-in, default OFF.
- **Auditoria**: via `logs_aprovacao` (enum `ExportacaoDados`, `AnonimizacaoConta`).
- **Regra**: treinador com vínculos ativos → exclusão BLOQUEADA (offboarding primeiro).
- Pendências jurídicas (copy do banner/política, contato DPO) e retenção fiscal (~5 anos): placeholders/documentação; não bloqueiam código.

## MODELO DE ANONIMIZAÇÃO (Domain)
Métodos idempotentes (Result), disparam scrub de PII e mantêm o registro:
- `Conta.Anonimizar(agora)`: `Email` → token irreversível `anon+{guid:N}@anonimizado.local` (via `Email.Criar`, normalizado), `PasswordHash` → vazio (login impossível), `AnonimizadaEm = agora`, email não-verificado. Emite `ContaAnonimizadaEvent(ContaId, TipoConta, OcorridoEm)`. Idempotente via `AnonimizadaEm != null`.
- `Aluno.Anonimizar(agora)`: scrub nome (→"Usuário anonimizado"), email(opt), telefone, e **anamnese SENSÍVEL** (finalidade, foco_treino, nivel_condicionamento, limitacoes_fisicas, doencas, observacoes_adicionais, dias/tempo). Idempotente via sentinela de nome.
- `Treinador.Anonimizar(agora)`: scrub nome (→sentinela), telefone. Idempotente.
- Coluna `contas.anonimizada_em` (tstz null) — migration `AdicionarAnonimizadaEmContas`. Aluno/Treinador usam sentinela de nome (sem coluna nova).

## FLUXO DE EXCLUSÃO (`AnonimizarContaHandler`, transação única)
1. Carrega conta (NotFound se ausente; idempotente se já anonimizada).
2. Resolve perfil. **Treinador com vínculos ativos → `Error.Business("offboarding_necessario")`, aborta.**
3. Captura email/telefone antigos (p/ scrub de logs).
4. `conta.Anonimizar` + `aluno|treinador.Anonimizar` + anonimiza read-model `assinantes` (`IAssinanteRepository.AnonimizarPorAlunoIdAsync`).
5. Scrub recipient dos delivery logs: `IEmailDeliveryLogRepository.AnonimizarPorEmailAsync(email)` + `IWhatsAppDeliveryLogRepository.AnonimizarPorTelefoneAsync(phone)` (UPDATE recipient → placeholder; payload mantido p/ auditoria de entrega).
6. Sessão: `PasswordHash` vazio + `AnonimizadaEm` impedem login futuro (revogação efetiva).
7. `LogAprovacao.Registrar(AnonimizacaoConta, ...)`.
8. `CommitAsync` único (despacha `ContaAnonimizadaEvent`). **RETÉM** pagamentos/assinaturas/logs (sem PII direto).

## FLUXO DE EXPORTAÇÃO (`ExportarDadosPessoaisHandler`)
- DTO `DadosPessoaisExport` versionado (`Versao`), seções: conta, perfil (aluno OU treinador), anamnese (aluno), vínculos, assinaturas, pagamentos, pacotes (treinador), treinos/fichas, execuções, progressão, delivery logs do titular. **Só dados do titular** (zero terceiros). Agrega por `ContaId` via repos existentes. Registra `LogAprovacao(ExportacaoDados)`.

## ENDPOINTS
| Método/Rota | Auth | Notas |
|-------------|------|-------|
| GET `/conta/lgpd/exportar` | autenticado (self) | JSON dos próprios dados |
| DELETE `/conta/lgpd` | autenticado (self) | body `{senha}`; reconfirma senha (BCrypt) antes de anonimizar; rate-limit "write" |
| GET `/admin/contas/{id}/lgpd/exportar` | SystemAdmin | export em nome do titular |
| DELETE `/admin/contas/{id}/lgpd` | SystemAdmin | anonimiza (sem senha) |
- Erros via `ToProblemResult` (`Error.NotFound`/`Business`→404/422). ⚠️ DELETE com body (`senha`) — alguns proxies removem body de DELETE; aceito por decisão.

## CONSENTIMENTO (frontend)
- `ConsentBanner` (`Aceitar todos` / `Só essenciais` / `Preferências` com toggle analytics). Categorias: **essencial** (sempre; auth httpOnly), **analytics** (Sentry, opt-in default OFF).
- Persistência: cookie `consent` versionado (`{v,analytics}`) + hook `useConsent`. Reabrir prefs em `/perfil`.
- **Gate Sentry**: `instrumentation-client.ts` só inicializa `@sentry/nextjs` se `consent.analytics === true` (default OFF até aceite). `ConsentProvider` monta o banner globalmente (root layout).

## FRONTEND — ações
- `/perfil` → seção "Privacidade (LGPD)": "Exportar meus dados" (baixa JSON), "Excluir minha conta" (ConfirmDialog + senha → DELETE → logout/redirect), "Preferências de cookies". API em `lib/api/conta.ts` (`exportarDados`, `excluirConta(senha)`).
- Admin (detalhe treinador/aluno, aba LGPD): exportar + anonimizar (ConfirmDialog destrutivo). `adminApi.exportarDadosConta(contaId)` / `anonimizarConta(contaId)`.

## DB / PII
- PII por tabela e escopo de scrub → ver `.specs/features/lgpd/spec.md` + [specification-db]. Sensível (saúde): anamnese do aluno. Retidos anonimizados: pagamentos, assinaturas_aluno, logs_aprovacao, treinos/execuções (ids).

## AUDITORIA
`logs_aprovacao` com `TipoAcaoAprovacao.ExportacaoDados` / `AnonimizacaoConta` (text enum; sem migration). Registra quem/quando/alvo.

## TESTES
- Backend: Domain (Anonimizar de Conta/Aluno/Treinador — PII some, idempotência, evento), Application (export agrega todas as seções sem terceiros; anonimização scrub + retém financeiro + bloqueio treinador-ativo + senha errada), endpoints (200/401/403). ~40 testes.
- Frontend: `ConsentBanner` (Sentry off sem consentimento, persistência), /perfil export/delete, admin actions; e2e `lgpd/{export-data,delete-account,consent-cookies}` verdes.

## PENDÊNCIAS / GOTCHAS
- **Jurídico**: copy do banner + política de privacidade + contato DPO = placeholders (validar com jurídico).
- **Retenção fiscal** (~5 anos BR) dos registros anonimizados: documentar; sem purga automática.
- **Carência/reversibilidade**: não há (anonimização imediata/irreversível). Avaliar soft-delete + purga agendada no futuro.
- **Export e telefone**: WhatsApp delivery logs casados por telefone do perfil; se o telefone mudou, logs antigos podem não casar (limitação do modelo — logs não têm conta_id).
- **Anonimização ≠ delete**: linha da conta permanece (anônima) — by design (fiscal + FKs RESTRICT).
- Referências: [specification-db] (`anonimizada_em`, enums), [specification-backend] (endpoints/Result), [specification-frontend] (banner/perfil), [specification-email]/[specification-whatsapp] (delivery logs).
