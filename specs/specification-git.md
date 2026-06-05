# specification-git — workflow git (forzion.tech)

DOC PARA AGENTES. Fonte de verdade do workflow git deste repo. Formato denso, agent-oriented. Consultar antes de mudar fluxo de branch, configs de cliente, hooks, ou processo de PR. Cross-ref: [specification-infrastructure] (CI/CD trigger por push/PR).

## MANUTENÇÃO DESTE ARQUIVO
- Atualizar NA MESMA TAREFA de mudança em: scopes de Conventional Commits, layout de branches, hooks `.husky/`, regras de pre-commit, comandos de setup, política de push/rebase.
- Vive em `specs/` (versionado; commitar). NÃO duplicar os scopes de Conventional Commits do AGENTS.md (§CONVENÇÕES-CHAVE) — referenciar.

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
```
git checkout -b feature/x      # nova branch local
# ... edita ...
git add ...; git commit -m "feat(scope): ..."
git push                       # autoSetupRemote cria upstream — sem flag
# (CI roda no remoto)
# PR via gh OU UI:
gh pr create --fill --base homolog
```
Após merge no remoto:
```
git checkout homolog
git pull                       # rebase auto (pull.rebase=true)
git branch -d feature/x        # cleanup local
git fetch --prune              # remove ref de feature/x do origin/ (também automático com fetch.prune)
```

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

## CONVENTIONAL COMMITS
- **Format:** `type(scope): subject` (subject minúsculo após `:`, commitlint enforça).
- **Types comuns:** `feat`, `fix`, `refactor`, `chore`, `test`, `docs`, `style`, `perf`, `ci`, `build`.
- **Scopes válidos** (vindos do AGENTS.md `CONVENÇÕES-CHAVE`): `frontend | backend | infra | ci | deps | tests | docs`.
- **Subject:** ≤72 chars idealmente. Imperativo ("add X", não "added X").
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
- **Backend gate**: `dotnet format --verify-no-changes` → `dotnet build -c Release` → `dotnet test --filter "Category!=Integration"`.
- **Frontend gate**: `npm run typecheck` → `npx lint-staged` (eslint --fix nos staged) → `npm test` (vitest).
- Filter `Category!=Integration` (trait-based) pula tests que exigem Docker. Sem isso, hook falhava localmente sem Docker rodando.
- **NUNCA `--no-verify`**. Política do repo (regra implícita pelo `husky` instalado). Se hook falha, conserte a causa.
- **Sequência obrigatória antes de `git add` em arquivos `.cs` novos** (CRLF gotcha — ver §EDGE CASES):
  1. `dotnet format forzion.tech.slnx` — normaliza CRLF + aplica style fixes
  2. `git add <arquivos>` — stage pós-format
  3. `git commit` — hook passa no `--verify-no-changes`

## PUSH / PR
- Branch local mesmo nome do remoto (convenção). `git push` sem args = pusha branch atual pro mesmo nome em `origin`.
- PR vai pra `homolog` (default) OU pra `master` quando a alteração foi feita direto em `homolog`. Ver AGENTS.md `FLUXO DE ALTERAÇÃO DE CÓDIGO`.
- `gh pr create --fill --base homolog` cria PR com title/body do commit topo.
- Após merge no remoto, branch local pode ser deletada (`git branch -d`).

## EDGE CASES
- **Branch protection bloqueia push direto** (não é o caso hoje, mas pode passar a ser): use `gh pr create` direto sem push manual `gh` resolve.
- **Rebase agressivo (WIP)** — trabalhar em `feature/x-wip` local sem track; promover via `git push origin feature/x-wip:feature/x` quando estável.
- **CRLF em arquivos `.cs` (Windows) — gotcha recorrente de agents** — `dotnet format --verify-no-changes` exige CRLF. Arquivos criados via Write tool em sessões de agent no Windows saem com LF → hook pre-commit falha com `error ENDOFLINE: Fix end of line marker` em CADA linha. Fix canônico antes de qualquer `git add` de arquivos `.cs` novos:
  ```
  dotnet format forzion.tech.slnx
  ```
  Isso converte LF → CRLF em todos os arquivos do projeto. Rodar ANTES de `git add` pra o stage já pegar CRLF. Se rodado depois, re-stage os arquivos (`git add` de novo). O hook `--verify-no-changes` confirma que o resultado está limpo. **NÃO usar** o workaround Python (frágil; dotnet format também aplica style/whitespace corrections além de ENDOFLINE). Configurar `core.autocrlf=true` no cliente (ver §CONFIGS) previne na maioria dos casos mas não é suficiente quando o Write tool bypassa git.

- **SonarAnalyzer warnings como bloqueantes do pre-commit** — `dotnet format --verify-no-changes` pode falhar com warnings de SonarAnalyzer (ex.: `S3267: Loop should be simplified by calling Select(...)`) quando o analyzer detecta uma violação que *poderia* ter um fix mas o `dotnet format` reporta sem conseguir aplicar. Esses warnings causam exit code 1 no hook. Fix: corrigir o código para remover a violação (aplicar a sugestão do analyzer). NÃO suprimir com `#pragma warning disable` — isso bypassaria o gate. Exemplos recorrentes:
  - `S3267`: `foreach (var x in col) { use x.Prop }` → refatorar para `foreach (var prop in col.Select(x => x.Prop)) { ... }` quando só a propriedade é usada no corpo do loop.
- **Lint-staged corrompendo arquivos** — `playwright/prefer-web-first-assertions` reescreve `await el.getAttribute(...)` em `expect malformado`. Workaround: usar `el.evaluate((e) => e.getAttribute(...))` (rule não match).
- **Reapply stash após rebase falho** — `rebase.autoStash=true` faz pop automático no fim. Se conflito durante pop, resolver manualmente (`git status` mostra UU).
- **Sync após force push em outro clone** (raro) — `git fetch && git reset --hard origin/<branch>` se a branch local NÃO tem trabalho não pushado.

## SETUP NOVO CLONE / NOVA MÁQUINA
1. Clone: `git clone <url>` (configs `--global` já valem se setup-git rodado antes).
2. Se primeira máquina, rodar setup-git:
   ```
   .\scripts\setup-git.ps1            # Windows, --global
   bash scripts/setup-git.sh           # POSIX, --global
   .\scripts\setup-git.ps1 -Local      # OU restringir ao clone
   ```
3. Instalar deps frontend (instala husky hooks): `cd frontend && npm install`.
4. Build backend: `dotnet build forzion.tech.slnx`.
5. Smoke: `git checkout -b chore/setup-smoke && echo x > smoke.txt && git add smoke.txt && git commit -m "chore: smoke" && git push` → upstream criado sem `-u`. Depois deletar branch.

## REFERÊNCIAS
- AGENTS.md `CONVENÇÕES-CHAVE` (scopes).
- AGENTS.md `FLUXO DE ALTERAÇÃO DE CÓDIGO` (PR target).
- `frontend/.husky/pre-commit` (hook real).
- `.gitignore` (gates `/docs/*` com whitelist em `docs/api/` e `docs/test-remediation/`).
- `scripts/setup-git.ps1` + `scripts/setup-git.sh` (aplicar configs).
