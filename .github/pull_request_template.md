<!--
Checklist obrigatorio antes do merge. Mantenha o template — preenchido eh
auditoria do que foi feito; deixar em branco eh red flag em review.
-->

## Resumo

<!-- 1-3 paragrafos: o que muda e por que. -->

## Mudancas

<!--
- Lista por arquivo/area afetada.
- Use scope (frontend/backend/infra/ci/deps/tests/docs).
-->

## Trade-offs / decisoes tecnicas

<!-- Qualquer decisao nao-obvia: por que esse caminho e nao o outro? -->

## Test plan

- [ ] `npm run validate` (typecheck + lint + 355 testes verdes)
- [ ] `npm run test:coverage` (thresholds atingidos)
- [ ] Mudancas em route handlers cobertas por testes em `src/app/api/**`
- [ ] Mudancas em componentes verificadas em DOM real (integration)
- [ ] Sem `vi.stubGlobal("fetch", ...)` — usar MSW
- [ ] Sem `--no-verify` em commits (politica)
- [ ] Conventional Commits no titulo + scope correto

## Checklist de seguranca (se aplicavel)

- [ ] Sem secrets ou env vars sensíveis no diff
- [ ] Sem novos endpoints expostos sem auth
- [ ] Headers/cookies de novas rotas configurados (HttpOnly, Secure, SameSite)
- [ ] Sanitizacao de input em proxy/forwarding

## Notas para o reviewer

<!-- Areas que merecem atencao especial; perguntas em aberto. -->

🤖 Generated with [Claude Code](https://claude.com/claude-code)
