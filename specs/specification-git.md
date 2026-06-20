# specification-git — workflow git (forzion.tech)

DOC PARA AGENTES. Fonte de verdade do workflow git deste repo. Formato denso, agent-oriented. Consultar antes de mudar fluxo de branch, configs de cliente, hooks, ou processo de PR. Cross-ref: [specification-infrastructure] (CI/CD trigger por push/PR).

## MANUTENÇÃO DESTE ARQUIVO
- Atualizar NA MESMA TAREFA de mudança em: scopes de Conventional Commits, layout de branches, hooks `.husky/`, regras de pre-commit, comandos de setup, política de push/rebase.
- NÃO duplicar os scopes de Conventional Commits do AGENTS.md (§CONVENÇÕES-CHAVE) — referenciar.

## CONTEXTO
Solo dev. 1 alteração por branch. Multi-folder: cada frente vive em pasta isolada (clone independente OU worktree). Prioriza baixa fricção sync local↔origin. SEM branch protection no remoto (push direto permitido). CI ainda gate de PR.

## CONFIGS RECOMENDADAS (cliente)
Aplicar via `scripts/setup-git.ps1` (Windows) ou `scripts/setup-git.sh` (POSIX). Idempotente. Default `--global`; flag `-Local` restringe ao clone.

| config | valor | porquê |
|---|---|---|
| `init.defaultBranch` | `main` | alinha branch inicial de `git init` ao convencional |
| `push.autoSetupRemote` | `true` | 1º `git push` em branch nova cria upstream sem `-u` |
| `push.default` | `current` | pusha SÓ branch atual (não todas as matching) |
| `pull.rebase` | `true` | `git pull` rebase em vez de merge — história linear |
| `rebase.autoStash` | `true` | rebase preserva working tree sujo (stash automático) |
| `fetch.prune` | `true` | `git fetch` remove refs locais de branches deletadas no remoto |
| `core.autocrlf` | `true` (Win) / `input` (POSIX) | alinha line endings com pre-commit `dotnet format` (ENDOFLINE check) |

Aliases opcionais (não setados por default):
- `git config --global alias.cap '!f() { git commit -m "$1" && git push; }; f'` — `git cap "msg"` = commit + push.
- `git config --global alias.lg "log --oneline --graph --decorate -20"` — vista compacta.

## WORKFLOW BRANCH (caminho feliz)
- Criar+editar: `git checkout -b feature/x` → commit `feat(scope): ...` → `git push` (autoSetupRemote cria upstream, sem `-u`) → `gh pr create --fill --base homolog`.
- Pós-merge: `git checkout homolog && git pull` (rebase auto) → `git branch -d feature/x` (cleanup; `fetch.prune` remove ref do origin no próximo fetch).

## WORKTREE vs MULTI-CLONE
| Critério | Multi-clone (N pastas, N `.git`) | Worktree (1 `.git`, N pastas) |
|---|---|---|
| Disco | N × tamanho do repo | 1 × repo + working trees leves |
| Configs/hooks | replicar em cada clone | 1 lugar centralizado |
| Branches | independentes (mas duplicadas) | compartilhadas — 1 branch por worktree |
| Isolation | total (incl. submodules) | parcial (refs comuns) |
| Setup nova frente | `git clone <url> ../forzion-foo` | `git worktree add ../forzion-foo branch/x` |
| Cleanup | `rm -rf ../forzion-foo` | `git worktree remove ../forzion-foo` |

**Recomendação:** worktree pra solo dev — uma só `setup-git`, hooks instalados uma vez, sem duplicar repo em disco. Multi-clone fica pra casos de submodules / CI workflows distintos.

Comandos worktree:
```
git worktree add ../forzion-fase3 fix/test-remediation-fase3   # cria checkout em ../forzion-fase3
git worktree add -b feature/nova ../forzion-nova               # cria branch nova + worktree
git worktree list                                              # vê todas
git worktree remove ../forzion-nova                            # quando termina (branch fica)
git worktree prune                                             # limpa refs órfãos
```
- **GOTCHA baseRef stale (CANÔNICO — recorrente com sub-agents)**: worktrees criadas pela automação/harness nascem da `worktree.baseRef` default (`fresh` → `origin/main`). `main` está MUITO atrás de `homolog` (centenas de commits) e de qualquer feature branch → a worktree nasce SEM o código da feature. SEMPRE, no início do trabalho num worktree de sub-agent: `git log --oneline -3` para conferir a base; se não for a branch-alvo, `git reset --hard <branch-alvo>` ANTES de qualquer alteração. Codar sobre base errada = retrabalho garantido + merge conflituoso. (Setar `git config worktree.baseRef <branch>` ajuda, mas o harness pode ignorar — a verificação+reset é a defesa confiável.)
- **Cleanup de worktree+branch na ordem certa**: `git worktree remove <path> --force` ANTES de `git branch -D <branch>` (não dá pra deletar branch ainda checked-out num worktree). Se a worktree sumiu do disco mas a ref persiste: `git worktree prune` primeiro.

## CONVENTIONAL COMMITS
- **Format:** `type(scope): subject` (subject minúsculo após `:`, commitlint enforça).
- **Types comuns:** `feat`, `fix`, `refactor`, `chore`, `test`, `docs`, `style`, `perf`, `ci`, `build`.
- **Scopes válidos** (`commitlint.config.mjs` `scope-enum`, do AGENTS.md `CONVENÇÕES-CHAVE`): `frontend | backend | infra | ci | deps | tests | docs` + `""` (escopo VAZIO permitido — `type: subject` sem `(scope)` passa).
- **GOTCHA scope = ÁREA, não tópico** (recorrente): o scope é a área do repo, NÃO o arquivo/assunto editado. Mexer em `specification-git.md`/`-stripe.md`/`-model.md` é `docs:` (ou escopo vazio), NÃO `docs(git)`/`docs(stripe)`/`docs(model)` — esses falham `scope-enum`. Spec de área coberta usa o scope da área se aplicável (ex.: regra de teste → `docs(tests)`), senão `docs:`.
- **Limites commitlint**: `header-max-length` 100 (erro); `body-max-line-length` 200 (warning).
- **GOTCHA merge commit também passa por commitlint** (recorrente): `git merge --no-ff -m "..."` dispara o `commit-msg` hook. `merge(...)` NÃO é type válido (`type-enum` falha) e header de merge costuma estourar 100 chars. Usar mensagem Conventional: `chore: integra X (#N)` / `feat(backend): ...` com header ≤100. Se o hook abortar o merge (fica em estado merge pendente), completar com `git commit --no-edit -m "<msg conventional>"`.
- **Subject:** ≤72 chars idealmente (header total ≤100, enforçado). Imperativo ("add X", não "added X").
- **Body:** quando o "porquê" não cabe no subject. Linha em branco entre subject e body.
- **Footer:** `Closes #N` pra issue; `BREAKING CHANGE:` pra incompatibilidade.
- **Granularidade:** 1 commit = 1 mudança lógica. Não misture refactor + feature.

Exemplos válidos vistos no repo:
```
fix(backend): derive AssinaturaAluno valor from pacote and enforce tenant
test(frontend): prove JwtMiddleware rejects revoked JWT after logout in E2E
chore(tests): trait-based pre-commit filter + harden Email regex
docs(infra): add git workflow spec + setup script
```

## PRE-COMMIT HOOK
- Vive em `frontend/.husky/pre-commit` (executável). `core.hooksPath` aponta pra `frontend/.husky/`.
- Roda **por área staged** (não global): se só backend → roda gate backend; idem frontend.
- **Gate de comentário (.cs)**: roda ANTES do gate backend, sempre que houver `.cs` staged (exclui EF-generated `*Designer.cs`/`*ModelSnapshot.cs`). Falha o commit se uma linha ADICIONADA contiver o subset objetivamente proibido da AGENTS.md regra 9: andaime/ref de task (`// T2B.3:`, `// TCR1:`, `// T7:` — regex `T[0-9][0-9A-Z.]*:` / `TCR[0-9]+:`) ou divisor decorativo UNICODE (`// ──`/`// ══`/`// ——`, ≥2 chars `[─━═—]` logo após `//`). NÃO bloqueia: divisor ASCII `// --- X ---` (idiom de teste), em-dash `—` no meio de frase, XML doc em interface. Paráfrase/comentário óbvio o gate NÃO pega (exige julgamento) — fica pra revisão.
- **Backend gate**: `dotnet format --verify-no-changes` → `dotnet build -c Release` → **openapi drift** → `dotnet test --filter "Category!=Integration"`.
- **OpenAPI drift gate** (espelha o job `openapi-drift` do CI): após o build, roda `bash scripts/gen-openapi.sh` (regenera `docs/api/openapi.v1.json` offline via OpenAPI nativo — `dotnet build -p:GenerateOpenApi=true --no-incremental` dispara o alvo GetDocument do `Microsoft.Extensions.ApiDescription.Server`; `--no-incremental` obrigatório p/ re-rodar; exporta `ASPNETCORE_ENVIRONMENT=Test`/`Auth__JwtSecret`) e falha o commit se `git diff --quiet -- docs/api/openapi.v1.json` acusar divergência (endpoint/contrato mudou sem regenerar o spec). Fix: `bash scripts/gen-openapi.sh` + `git add docs/api/openapi.v1.json`. Só roda quando `BACKEND_CHANGED=1`.
- **Frontend gate**: `npm run typecheck` → `npx lint-staged` (eslint --fix nos staged) → `npm test` (vitest).
- Filter `Category!=Integration` (trait-based) pula tests que exigem Docker. Sem isso, hook falhava localmente sem Docker rodando.
- **NUNCA `--no-verify`**. Política do repo (regra implícita pelo `husky` instalado). Se hook falha, conserte a causa.
- **Sequência obrigatória antes de `git add` em arquivos `.cs` novos** (CRLF gotcha — ver §EDGE CASES):
  1. `dotnet format forzion.tech.slnx` — normaliza CRLF + aplica style fixes
  2. `git add <arquivos>` — stage pós-format
  3. `git commit` — hook passa no `--verify-no-changes`

## PUSH / PR
- Branch local mesmo nome do remoto (convenção). `git push` sem args = pusha branch atual pro mesmo nome em `origin`.
- **PR é MANUAL — não abrir automaticamente** (decisão 2026-06-10, até segunda ordem; uso consciente de recursos de CI + o usuário decide o que vira PR): concluir em `commit + push` na branch; o usuário pede o PR quando quiser. Ver AGENTS.md `DEFINITION OF DONE` passo 8.
- Quando solicitado: PR vai pra `homolog` (default) OU pra `master` quando a alteração foi feita direto em `homolog`.
- `gh pr create --fill --base homolog` cria PR com title/body do commit topo.
- Após merge no remoto, branch local pode ser deletada (`git branch -d`).
- **CODE REVIEW pós-PR, ao CI ficar VERDE** (regra; AGENTS.md DoD item 9b): aberto o PR, MONITORAR os checks (`gh pr checks <N>`; background até run `completed`). Code review do diff (context7) roda SÓ quando o CI fica VERDE — não revisar código que ainda falha gate (desperdício; o gate pode mudar o diff). **CI VERMELHO ⇒ debugar a causa PRIMEIRO** (`superpowers:systematic-debugging`), corrigir, repush; revisão espera o verde. Pegar log de job que falhou: `gh run view --job <jobId> --log-failed` (exige a RUN inteira `completed` — jobs pending bloqueiam; aguardar). `/code-review` (local) eu rodo; `/code-review ultra` (cloud, billed) é disparado pelo usuário.

## EDGE CASES
- **Branch protection bloqueia push direto** (não é o caso hoje, mas pode passar a ser): use `gh pr create` direto sem push manual `gh` resolve.
- **Rebase agressivo (WIP)** — trabalhar em `feature/x-wip` local sem track; promover via `git push origin feature/x-wip:feature/x` quando estável.
- **CRLF em `.cs` (Windows) — gotcha recorrente de agents** (CANÔNICO) — `dotnet format --verify-no-changes` exige CRLF; arquivos criados via Write tool saem com LF → hook falha `error ENDOFLINE` em cada linha. Fix canônico ANTES de `git add` de `.cs` novos: `dotnet format forzion.tech.slnx` (converte LF→CRLF + aplica style/whitespace). Se rodado depois, re-`git add`. `.gitattributes` (raiz) força `*.cs text eol=crlf` e `*.sh text eol=lf` (husky scripts), `*.verified.txt eol=lf` — cobre checkout Linux no CI. `core.autocrlf=true` (§CONFIGS) cobre a maioria, mas não quando o Write tool bypassa git. NÃO usar workaround Python (frágil; só ENDOFLINE).
- **SonarAnalyzer bloqueia o pre-commit** (CANÔNICO) — `dotnet format --verify-no-changes` pode sair `1` com warning de Sonar que ele reporta mas não aplica. Fix: corrigir o código (aplicar a sugestão); NÃO `#pragma warning disable` (bypassaria o gate). Ex. recorrente `S3267`: `foreach (var x in col) { use x.Prop }` → `foreach (var prop in col.Select(x => x.Prop))` quando só a propriedade é usada.
- **Commit multiline com here-string no shell errado** (CANÔNICO — gotcha recorrente de agents) — ambiente Windows tem 2 shells: PowerShell e Bash tool (bash). Here-string PS `@'...'@` é PowerShell-ONLY; rodada via Bash tool, o `@` vira 1ª linha literal → commit-msg falha commitlint (`subject-empty`/`type-empty`). Regra: passar mensagem multiline por canal compatível com o shell. Bash tool → `git commit -F - <<'EOF' ... EOF` (heredoc). PowerShell tool → `git commit -m @'<newline>...<newline>'@` (here-string; `'@` na coluna 0). NÃO misturar as duas sintaxes. Alternativa neutra: escrever a msg em arquivo e `git commit -F <arquivo>`.
- **Lint-staged corrompendo arquivos** — `playwright/prefer-web-first-assertions` reescreve `await el.getAttribute(...)` em `expect malformado`. Workaround: usar `el.evaluate((e) => e.getAttribute(...))` (rule não match).
- **Reapply stash após rebase falho** — `rebase.autoStash=true` faz pop automático no fim. Se conflito durante pop, resolver manualmente (`git status` mostra UU).
- **Sync após force push em outro clone** (raro) — `git fetch && git reset --hard origin/<branch>` se a branch local NÃO tem trabalho não pushado.
- **Branch reusada após squash-merge** (gotcha recorrente) — PR squash-merged colapsa os N commits da branch num ÚNICO commit novo no alvo; os originais ficam órfãos. Continuar/re-mirar a MESMA branch depois ⇒ GitHub marca CONFLICTING (base divergiu, history não casa). Fix: reconstruir a branch limpa do alvo atualizado (`git reset --hard origin/<alvo>`) + `git cherry-pick` só do trabalho ainda não mergeado; NÃO merge/rebase da branch velha. Alternativa: merge-commit em vez de squash preserva a linhagem e evita o CONFLICTING ao reusar.

## SETUP NOVO CLONE / NOVA MÁQUINA
1. `git clone <url>` (configs `--global` já valem se setup-git rodou antes).
2. 1ª máquina: setup-git (`scripts/setup-git.ps1` Win / `setup-git.sh` POSIX; `-Local`/`--local` restringe ao clone vs default `--global`).
3. `cd frontend && npm install` (instala husky hooks).
4. `dotnet build forzion.tech.slnx`.
5. Smoke (opcional): branch throwaway + commit + `git push` → confirma upstream sem `-u`; deletar depois.

## REFERÊNCIAS
- AGENTS.md `CONVENÇÕES-CHAVE` (scopes).
- AGENTS.md `DEFINITION OF DONE` (passos 7-8: git + PR manual).
- `frontend/.husky/pre-commit` (hook real).
- `frontend/.husky/commit-msg` (commitlint hook).
- `.gitignore` (gates `/docs/*` com whitelist em `docs/api/` — único whitelist atual).
- `.gitattributes` (raiz): `*.cs eol=crlf`, `*.sh eol=lf`, `*.verified.txt eol=lf`.
- `scripts/setup-git.ps1` + `scripts/setup-git.sh` (aplicar configs).
