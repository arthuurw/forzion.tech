# forzion.tech — Frontend

Interface web da plataforma forzion.tech para personal trainers e alunos.

---

## Índice

- [Stack](#stack)
- [Pré-requisitos](#pré-requisitos)
- [Comandos](#comandos)
- [Variáveis de Ambiente](#variáveis-de-ambiente)
- [Estrutura de Arquivos](#estrutura-de-arquivos)
- [Rotas por Perfil](#rotas-por-perfil)
- [Autenticação e Sessão](#autenticação-e-sessão)
- [Proxy Server-Side](#proxy-server-side)
- [Proteção de Rotas](#proteção-de-rotas)
- [Inatividade e Auto-logout](#inatividade-e-auto-logout)
- [Tratamento de Erros](#tratamento-de-erros)
- [Hooks Reutilizáveis](#hooks-reutilizáveis)
- [Componentes Reutilizáveis](#componentes-reutilizáveis)
- [Utilitários e Constantes](#utilitários-e-constantes)
- [Padrões de Desenvolvimento](#padrões-de-desenvolvimento)
- [API Client](#api-client)
- [Tema e Responsividade](#tema-e-responsividade)
- [Segurança](#segurança)
- [Testes](#testes)

---

## Stack

| | Tecnologia | Versão |
|--|------------|--------|
| Framework | Next.js (App Router) | 16 |
| UI | MUI (Material UI) + Emotion | v9 |
| Linguagem | TypeScript | 5 |
| Forms | React Hook Form + Zod | 7 + 4 |
| HTTP | Axios | 1.x |
| Estado global | Zustand | 5 |
| Datas | Day.js | 1.x |
| Runtime | React | 19 |
| Gráficos | Recharts | 3.x |
| Exportação | ExcelJS | 4.x |
| Pagamentos | @stripe/stripe-js + @stripe/react-stripe-js | 9.x / 6.x |
| JWT (servidor) | jose | 6.x |
| Observabilidade | Sentry + Web Vitals (RUM) | 10 |

### Test harness

| Camada | Ferramenta | Versão |
|--------|------------|--------|
| Unit / integration / api | Vitest 4 (projects) + jsdom + Testing Library | 4 |
| Mock de API HTTP | MSW + @mswjs/data + OpenAPI codegen | 2 |
| Property-based | fast-check + @fast-check/vitest | 4 |
| A11y (componente) | vitest-axe | 1 |
| E2E + a11y de página | Playwright + @axe-core/playwright | 1.x |
| Component workshop | Storybook 10 + test-runner | 10 |
| Mutation testing | Stryker (lib + hooks) | 9 |
| Contract testing | Pact (consumer-driven, broker self-hosted) | 13 |
| Performance | Lighthouse CI + bundle-analyzer + linkinator | — |
| Dead code | knip + madge | 5 / 8 |

> Os planos de harness (`docs/frontend-harness-plan.md`, `docs/frontend-harness-rationale.md`) ficam fora do controle de versão — `docs/` é gitignored (ver `.gitignore` na raiz). Consulte-os localmente se presentes.

---

## Pré-requisitos

- Node.js 20+
- API backend rodando em `http://localhost:5230` ou `https://localhost:7220` (ver `../README.md`)

---

## Comandos

```bash
# ── App ─────────────────────────────────────────────
npm install            # dependências
npm run dev            # dev server (http://localhost:3000)
npm run build          # build de produção (standalone)
npm start              # serve o build

# ── Qualidade (gate local) ──────────────────────────
npm run validate       # typecheck + lint + test (roda no pre-commit)
npm run typecheck      # tsc --noEmit
npm run lint           # eslint .

# ── Testes Vitest (projects) ────────────────────────
npm test               # todos os projects, single run
npm run test:unit      # project unit (node) — lib, hooks, middleware
npm run test:integration # project integration (jsdom) — componentes/páginas
npm run test:api       # project api (node) — route handlers
npm run test:watch     # watch (project unit)
npm run test:coverage  # cobertura (thresholds por camada)
npm run test:property  # só property-based (fast-check)

# ── E2E (Playwright) ────────────────────────────────
npm run e2e            # suite completa (precisa de E2E_BASE_URL + creds)
npm run e2e:smoke      # só @smoke
npm run e2e:ui         # modo UI
npm run e2e:install    # instala browsers

# ── Storybook ───────────────────────────────────────
npm run storybook      # dev (http://localhost:6006)
npm run storybook:build
npm run storybook:test # test-runner contra o storybook

# ── Qualidade avançada ──────────────────────────────
npm run test:mutation  # Stryker (mutation score em lib + hooks)
npm run test:contract  # Pact (gera/valida contratos consumer)
npm run hygiene        # madge (circular) + knip (dead code)
npm run analyze        # bundle analyzer (ANALYZE=true build)
npm run lhci           # Lighthouse CI
npm run links          # linkinator (links quebrados)

# ── Segurança / supply chain ────────────────────────
npm run security:all   # audit (prod) + license-checker + SBOM

# ── OpenAPI ─────────────────────────────────────────
npm run openapi:sync   # baixa spec do backend + gera tipos MSW
npm run openapi:check   # falha se os tipos divergirem do spec
```

> CI roda esses lanes no GitHub Actions (`.github/workflows/`): `ci.yml` (gate de PR: lint, build, testes, security, cobertura), `semgrep.yml` (SAST), `hygiene.yml` (dead code), `contract.yml` (Pact), `mutation.yml` (semanal), `smoke.yml`/`lighthouse.yml`/`zap.yml` (pós-deploy/manual contra homolog).

---

## Variáveis de Ambiente

Crie `.env.local` na raiz de `frontend/`:

```env
# URL base usada pelo proxy server-side para chamar o backend
# (nunca exposta ao browser)
API_BASE_URL=https://localhost:7220

# Segredo JWT — deve ser idêntico ao JWT_SECRET do backend
# Usado por /api/auth/me para verificar assinatura HMAC do token
JWT_SECRET=sua_chave_secreta_minimo_32_chars
JWT_ISSUER=forzion.tech
JWT_AUDIENCE=forzion.tech

# Exposta ao browser — aponta para o proxy Next.js, NÃO para o backend diretamente
# O proxy injeta o token Bearer server-side; o browser nunca vê o token
NEXT_PUBLIC_API_BASE_URL=/api/backend

# Chave pública do Stripe (pk_test_... ou pk_live_...)
# Usada pelo Stripe.js no browser para tokenizar cartões — nunca é secreta
NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=pk_test_...
```

> **Importante**: `NEXT_PUBLIC_API_BASE_URL` deve apontar para `/api/backend` (proxy local) e não para o backend diretamente. O token JWT nunca é exposto ao browser — o proxy server-side em `src/app/api/backend/[...path]/route.ts` lê o cookie `token` (httpOnly) e injeta `Authorization: Bearer` antes de encaminhar.

---

## Estrutura de Arquivos

```
frontend/
├── src/
│   ├── app/
│   │   ├── (admin)/                    # Route group — SystemAdmin
│   │   │   ├── layout.tsx              # AppLayout com nav admin
│   │   │   └── admin/
│   │   │       ├── page.tsx            # Dashboard (stat cards, donut, pendentes)
│   │   │       ├── treinadores/
│   │   │       │   ├── page.tsx        # Lista + aprovação + inativação + plano; ícone Info → detalhe
│   │   │       │   └── [treinadorId]/  # Detalhe — tabs: Alunos, Vínculos, Treinos, Pacotes
│   │   │       ├── alunos/
│   │   │       │   ├── page.tsx        # Lista todos os alunos (filtros: nome, status)
│   │   │       │   └── [alunoId]/      # Detalhe — tabs: Dados+Vínculo, Fichas, Execuções, Progressão
│   │   │       ├── treinos/
│   │   │       │   └── [treinoId]/     # Detalhe de treino (read-only)
│   │   │       ├── planos/             # CRUD planos globais (nome, tier, maxAlunos, preço, descricao, ativo)
│   │   │       ├── grupos-musculares/  # CRUD grupos musculares
│   │   │       └── exercicios/         # Biblioteca global de exercícios
│   │   │
│   │   ├── (treinador)/                # Route group — Treinador
│   │   │   ├── layout.tsx
│   │   │   └── treinador/
│   │   │       ├── page.tsx            # Dashboard (stat cards, donut alunos, vínculos pendentes)
│   │   │       ├── alunos/
│   │   │       │   ├── page.tsx        # Lista de alunos vinculados
│   │   │       │   └── [alunoId]/      # Detalhe do aluno + fichas vinculadas + progressão
│   │   │       ├── treinos/
│   │   │       │   ├── page.tsx        # Lista fichas + criar (filtro por objetivo/dificuldade)
│   │   │       │   └── [treinoId]/     # Editor de ficha (exercícios, séries, ordem, cargas)
│   │   │       ├── exercicios/         # Biblioteca pessoal + copiar global
│   │   │       ├── pacotes/            # CRUD pacotes (nome + descrição + preço)
│   │   │       ├── pagamentos/         # Stripe Connect onboarding + status da conta
│   │   │       └── onboarding/
│   │   │           └── retorno/        # Retorno pós-onboarding Stripe (verifica status)
│   │   │
│   │   ├── (aluno)/                    # Route group — Aluno
│   │   │   ├── layout.tsx
│   │   │   └── aluno/
│   │   │       ├── page.tsx            # Dashboard aluno
│   │   │       ├── fichas/
│   │   │       │   ├── page.tsx        # Lista fichas ativas
│   │   │       │   └── [fichaId]/
│   │   │       │       ├── page.tsx    # Detalhe da ficha com exercícios + exportar Excel
│   │   │       │       └── executar/   # Execução passo a passo com registro de cargas
│   │   │       ├── historico/          # Histórico de execuções com gráficos
│   │   │       ├── assinatura/         # Status da assinatura + pagamento pendente
│   │   │       └── pagamentos/         # Histórico de cobranças (Pix ou cartão)
│   │   │
│   │   ├── (public)/                   # Route group — sem auth
│   │   │   ├── layout.tsx              # PublicLayout
│   │   │   ├── login/
│   │   │   ├── verify-email/           # Verifica e-mail via token (?token=); erro → botão reenviar
│   │   │   ├── resend-verification/    # Reenvia link de verificação (e-mail; sucesso silencioso)
│   │   │   ├── forgot-password/        # Solicita link de reset (e-mail; sucesso silencioso)
│   │   │   ├── reset-password/         # Define nova senha via token (?token=)
│   │   │   └── cadastro/
│   │   │       ├── treinador/
│   │   │       └── aluno/              # Seleciona treinador + pacote
│   │   │
│   │   ├── api/                        # Route Handlers (BFF — server-side only)
│   │   │   ├── auth/
│   │   │   │   ├── route.ts            # POST /api/auth — login; seta cookies httpOnly; token não retornado no body
│   │   │   │   ├── me/route.ts         # GET /api/auth/me — verifica JWT via jose jwtVerify (HMAC)
│   │   │   │   ├── logout/route.ts     # POST /api/auth/logout — revoga JTI, limpa cookies
│   │   │   │   ├── register/
│   │   │   │   │   ├── treinador/route.ts
│   │   │   │   │   └── aluno/route.ts
│   │   │   │   ├── verify-email/route.ts       # POST — verifica e-mail (repassa ProblemDetails)
│   │   │   │   ├── resend-verification/route.ts # POST — reenvia verificação (sempre 200)
│   │   │   │   ├── forgot-password/route.ts     # POST — solicita reset (sempre 200)
│   │   │   │   ├── reset-password/route.ts      # POST — redefine senha via token
│   │   │   │   └── treinadores/
│   │   │   │       ├── route.ts        # GET — lista treinadores ativos (público)
│   │   │   │       └── [treinadorId]/pacotes/route.ts
│   │   │   └── backend/
│   │   │       └── [...path]/route.ts  # Proxy genérico — injeta Bearer, repassa ao backend
│   │   │
│   │   ├── _landing/
│   │   │   └── HowItWorks.tsx          # Seção "Como funciona" da landing page
│   │   ├── perfil/
│   │   │   ├── layout.tsx
│   │   │   └── page.tsx                # Perfil + alterar senha (todos os perfis)
│   │   ├── layout.tsx                  # Root layout — AuthProvider + SnackbarProvider + viewport
│   │   ├── page.tsx                    # Landing page (hero + planos + CTA)
│   │   ├── error.tsx                   # Error boundary global
│   │   └── not-found.tsx
│   │
│   ├── components/
│   │   ├── forms/
│   │   │   ├── FormTextField.tsx       # TextField integrado com react-hook-form
│   │   │   ├── FormSelect.tsx          # Select integrado com react-hook-form
│   │   │   └── PasswordField.tsx       # TextField com toggle de visibilidade
│   │   ├── layout/
│   │   │   ├── AppLayout.tsx           # Layout autenticado + proteção client-side + safe-area
│   │   │   ├── AppHeader.tsx           # Header com nav + avatar + logout
│   │   │   ├── PublicLayout.tsx        # Layout para páginas públicas (100dvh)
│   │   │   └── NavConfig.tsx           # Itens de nav por TipoConta
│   │   ├── pagamento/
│   │   │   ├── PagamentoPix.tsx        # QR Code + copia e cola + polling 30s de status
│   │   │   └── PagamentoCartao.tsx     # Stripe Elements (PaymentElement) + confirmPayment
│   │   ├── treinador/
│   │   │   └── ProgressaoAluno.tsx     # Gráfico de progressão do aluno
│   │   └── ui/
│   │       ├── AlertBanner.tsx         # Banner de erro/sucesso inline
│   │       ├── ConfirmDialog.tsx       # Dialog de confirmação genérico
│   │       ├── DataList.tsx            # Card com loading + empty + tabela paginada
│   │       ├── EmptyState.tsx          # Estado vazio com ícone e mensagem
│   │       ├── InfoLine.tsx            # Label + valor em linha (detalhe)
│   │       ├── LoadingSpinner.tsx      # Spinner centralizado
│   │       ├── Logo.tsx                # Logo forzion.tech
│   │       ├── ResponsiveTable.tsx     # Tabela desktop / cards mobile com paginação MUI
│   │       ├── SnackbarProvider.tsx    # Contexto global de snackbar
│   │       └── StatusChip.tsx          # Chip colorido para status (AlunoStatus, TreinadorStatus, VinculoStatus)
│   │
│   ├── hooks/
│   │   ├── usePaginatedList.ts         # Lista paginada genérica
│   │   ├── useCRUDDialog.ts            # Estado de 3 dialogs CRUD
│   │   └── useInactivity.ts            # Auto-logout por inatividade
│   │
│   ├── lib/
│   │   ├── api/
│   │   │   ├── client.ts               # Instância Axios configurada
│   │   │   ├── admin.ts                # Treinadores, planos, grupos, exercícios globais, visibilidade admin
│   │   │   ├── treinador.ts            # Alunos, vínculos, fichas, exercícios, pacotes
│   │   │   ├── aluno.ts                # Fichas, execuções, vínculo
│   │   │   ├── pagamento.ts            # Onboarding Stripe, cobranças, pagamentos
│   │   │   └── conta.ts                # Perfil, senha
│   │   ├── auth/
│   │   │   ├── AuthContext.tsx         # Contexto de sessão React
│   │   │   ├── context.ts             # homeRouteFor
│   │   │   └── jwt.ts                 # parseJwtPayload + extractTipoConta (base64-decode, sem HMAC — uso só em middleware)
│   │   ├── constants/
│   │   │   ├── labels.ts               # Maps enum → label PT-BR
│   │   │   └── enrollmentOptions.ts    # Opções de select do cadastro aluno
│   │   ├── theme/
│   │   │   └── index.ts                # Tema MUI (paleta + tipografia + overrides responsivos)
│   │   ├── utils/
│   │   │   ├── formatting.ts           # Funções de formatação
│   │   │   └── excel.ts                # Exportação de fichas para .xlsx (ExcelJS)
│   │   ├── validations/
│   │   │   └── common.ts               # Schemas Zod reutilizáveis
│   │   └── rateLimit.ts                # Rate limiter in-memory para /api/auth (10 req/min por IP)
│   │
│   ├── middleware.ts                   # Proteção de rotas server-side
│   ├── styles/
│   │   └── globals.css                 # Reset + 100dvh + safe-area-inset
│   └── types/
│       └── index.ts                    # Types e interfaces compartilhados
│                                       # TierPlano: "Free" | "Basic" | "Pro" | "ProPlus" | "Elite"
│                                       # PlanoPlataformaResponse: inclui tier e descricao (nullable)
│
├── vitest.config.mts                   # Config Vitest
├── next.config.ts                      # Headers de segurança, output standalone
└── .env.local                          # Variáveis locais (não versionado)
```

---

## Rotas por Perfil

| Path | Perfil | Descrição |
|------|--------|-----------|
| `/` | público | Landing page — hero, planos (com tier, preço e `descricao` vindos da API), como funciona, CTA de cadastro |
| `/login` | público | Formulário de login (403 `EMAIL_NAO_VERIFICADO` → orienta verificar e-mail) |
| `/cadastro/treinador` | público | Cadastro de treinador (seleciona plano) |
| `/cadastro/aluno` | público | Cadastro de aluno (seleciona treinador + pacote) |
| `/verify-email` | público | Verifica e-mail via `?token=`; em erro/expirado, botão "Reenviar verificação" |
| `/resend-verification` | público | Reenvia link de verificação (informa e-mail; sucesso silencioso) |
| `/forgot-password` | público | Solicita link de reset de senha (sucesso silencioso) |
| `/reset-password` | público | Define nova senha via `?token=` |
| `/admin` | SystemAdmin | Dashboard — stat cards, gráfico donut por status, vínculos pendentes inline |
| `/admin/treinadores` | SystemAdmin | Lista com filtro por status, aprovação, reprovação, inativação, atribuição de plano; ícone Info → detalhe |
| `/admin/treinadores/[treinadorId]` | SystemAdmin | Detalhe do treinador — tabs: Alunos, Vínculos, Treinos, Pacotes |
| `/admin/alunos` | SystemAdmin | Lista todos os alunos com filtros por nome e status |
| `/admin/alunos/[alunoId]` | SystemAdmin | Detalhe do aluno — tabs: Dados + Vínculo, Fichas, Execuções, Progressão |
| `/admin/treinos/[treinoId]` | SystemAdmin | Detalhe de treino (read-only) |
| `/admin/planos` | SystemAdmin | CRUD planos globais (nome, tier, maxAlunos, preço, descricao, ativo) |
| `/admin/grupos-musculares` | SystemAdmin | CRUD grupos musculares |
| `/admin/exercicios` | SystemAdmin | Biblioteca global de exercícios (CRUD + grupo muscular) |
| `/treinador` | Treinador | Dashboard — stat cards, donut alunos por status, vínculos pendentes inline |
| `/treinador/alunos` | Treinador | Lista de alunos vinculados com filtro por status |
| `/treinador/alunos/[alunoId]` | Treinador | Detalhe do aluno + fichas vinculadas + progressão |
| `/treinador/treinos` | Treinador | Lista fichas (filtro por objetivo/dificuldade); criar; duplicar |
| `/treinador/treinos/[treinoId]` | Treinador | Editor de ficha — exercícios, séries, cargas, ordem + exportar Excel |
| `/treinador/exercicios` | Treinador | Biblioteca pessoal + copiar exercícios globais |
| `/treinador/pacotes` | Treinador | CRUD pacotes (nome, descrição, preço) |
| `/treinador/pagamentos` | Treinador | Stripe Connect onboarding — configura conta de recebimentos |
| `/treinador/onboarding/retorno` | Treinador | Retorno pós-onboarding Stripe — verifica e exibe status |
| `/aluno` | Aluno | Dashboard |
| `/aluno/fichas` | Aluno | Lista fichas ativas vinculadas ao aluno |
| `/aluno/fichas/[fichaId]` | Aluno | Detalhe da ficha com exercícios, instruções e botão exportar |
| `/aluno/fichas/[fichaId]/executar` | Aluno | Execução passo a passo — registra séries e cargas |
| `/aluno/historico` | Aluno | Histórico de execuções com gráficos de progressão |
| `/aluno/assinatura` | Aluno | Status da assinatura + pagamento pendente (Pix ou cartão) |
| `/aluno/pagamentos` | Aluno | Histórico de cobranças com tabela e dialog de pagamento inline |
| `/perfil` | todos | Dados da conta + alterar senha |

---

## Autenticação e Sessão

O fluxo usa dois cookies complementares, ambos `httpOnly`:

| Cookie | `httpOnly` | `SameSite` | Uso |
|--------|-----------|-----------|-----|
| `token` | ✅ | `strict` | JWT completo — lido pelo middleware e por `/api/auth/me`; não acessível via JS |
| `session_guard` | ✅ | `strict` | Flag de sessão; ausência invalida sessão mesmo com `token` presente |

`maxAge` de ambos é derivado do `exp` do JWT — expiram juntos com o token.

> O cookie `token_access` (JS-readable, usado anteriormente pelo interceptor Axios) foi **removido** por risco de XSS. O token nunca é exposto ao browser — o proxy server-side injeta o Bearer.

### Login

1. Página `/login` chama `POST /api/auth` (Route Handler — server-side)
2. Route Handler repassa para `POST /auth/login` na API backend
3. Em sucesso: seta cookies `token` + `session_guard` (httpOnly, maxAge = exp do JWT)
4. **Token JWT não é retornado no body da resposta** — permanece exclusivamente nos cookies
5. `AuthContext.login()` atualiza o estado React com os dados do usuário
6. `router.push(homeRouteFor(tipoConta))` redireciona para a área correta

### Validação de Sessão no Mount

`AuthContext` dispara `GET /api/auth/me` no mount:

```
Browser → GET /api/auth/me (server-side Route Handler)
            └── lê cookie 'token' (httpOnly) + 'session_guard'
            └── jose.jwtVerify(token, JWT_SECRET, { issuer, audience })
                  ├── verifica assinatura HMAC-SHA256
                  ├── valida exp, iss, aud
                  └── extrai conta_id, tipo_conta, perfil_id
            └── retorna SessionUser | null
```

Se `null`: `AppLayout` chama `POST /api/auth/logout` (limpa cookies) e redireciona para `/login`.

### Logout

1. `POST /api/auth/logout` (Route Handler)
2. Route Handler lê cookie `token` httpOnly → chama `POST /conta/logout` no backend (revoga JTI)
3. Apaga todos os cookies
4. `AuthContext.logout()` → `setUser(null)` → `router.push("/login")`

---

## Proxy Server-Side

`src/app/api/backend/[...path]/route.ts` — proxy genérico que intercepta todas as chamadas Axios do frontend:

```
Browser
  └── Axios → GET/POST/PATCH/DELETE /api/backend/<qualquer-rota>
                └── Route Handler (server-side)
                      ├── lê cookie 'token' (httpOnly, invisível ao browser)
                      ├── injeta Authorization: Bearer <token>
                      └── encaminha para API_BASE_URL/<rota>
                            └── Backend ASP.NET Core
```

Suporta `GET`, `POST`, `PUT`, `PATCH`, `DELETE`. Repassa body, query string e headers relevantes. Em caso de 401 do backend, o Axios response interceptor redireciona para `/login`.

---

## Proteção de Rotas

### Server-side — `middleware.ts`

Executa em cada request (exceto assets estáticos e `/api/`). Lê `token` + `session_guard` (httpOnly) e extrai `tipo_conta` do JWT payload via **base64-decode** (sem verificação HMAC — performance; a verificação HMAC acontece em `/api/auth/me` e o backend valida em cada chamada autenticada):

| Situação | Comportamento |
|----------|--------------|
| Sem autenticação em área protegida | Redireciona `/login` |
| Autenticado em `/login` | Redireciona para área do `TipoConta` |
| Autenticado em área errada (ex: Aluno em `/admin`) | Redireciona para área correta |
| `/` e `/cadastro/*` | Sempre acessíveis |

### Client-side — `AppLayout.tsx`

Camada extra de proteção após o middleware:

```
mount → isLoading: spinner
     → !isLoading && !user → POST /api/auth/logout (limpa cookies) → router.replace("/login")
     → !isLoading && user → renderiza children
```

---

## Inatividade e Auto-logout

`useInactivity` monitora eventos de interação (`mousemove`, `keydown`, `click`, etc.) e:

| Evento | Quando |
|--------|--------|
| `onWarn(minutesInactive)` | A cada múltiplo de 5 min de inatividade (5, 10, 15 min) |
| `onTimeout()` | Após 20 min de inatividade → logout automático |

Check a cada 20 segundos. Reinicia ao detectar qualquer interação. Ativo apenas quando `user != null`.

---

## Tratamento de Erros

### HTTP → UI

| Status | Comportamento |
|--------|--------------|
| `401` | Axios response interceptor → `window.location.href = "/login"` |
| `403` | Acesso negado ou, no login, e-mail não verificado (`code = EMAIL_NAO_VERIFICADO`) → orienta verificar/reenviar |
| `400` | FluentValidation → campos específicos via `error.response.data.errors` |
| `422` | Regra de negócio → `error.response.data.detail` exibido via `AlertBanner` |
| `404` | Mensagem genérica ou `error.response.data.title` |
| `429` | Rate limit atingido — mensagem específica |
| `5xx` | Mensagem genérica (detalhes internos nunca expostos) |

### Erro de autenticação nas páginas públicas

Login e cadastros **não** expõem mensagens internas:

- `401` → mensagem hardcoded ("E-mail ou senha inválidos")
- `5xx` → mensagem genérica
- outros → `problem.title` ou `problem.detail` do RFC 7807

---

## Hooks Reutilizáveis

### `usePaginatedList<T>`

Gerencia estado completo de lista paginada com carregamento automático.

```tsx
interface Options<T> {
  fetcher: (page: number, pageSize: number) => Promise<PaginatedResponse<T>>;
  errorMessage?: string;    // default: "Erro ao carregar."
  initialPageSize?: number; // default: 10
}

// Retorno:
{
  items, total,
  page, pageSize,
  loading, error, success,
  setPage, setPageSize,
  setError, setSuccess,
  reload,
}
```

Uso com filtros — a `fetcher` deve ser um `useCallback` com filtros como deps. Mudança de dep → nova ref → hook recarrega automaticamente:

```tsx
const fetcher = useCallback(
  (p, ps) => api.listarAlunos({ pagina: p + 1, tamanhoPagina: ps, status: filtroStatus })
               .then(r => r.data),
  [filtroStatus]
);

const { items, total, page, pageSize, loading, error, setPage, setPageSize, reload } =
  usePaginatedList<AlunoResponse>({ fetcher });

// onChange de filtro:
const handleFiltro = (novoStatus: string) => {
  setFiltroStatus(novoStatus);
  setPage(0); // reset para página 0
};
```

### `useCRUDDialog<T>`

Gerencia estado de 3 dialogs (criar / editar / excluir) de forma unificada.

```tsx
const {
  // Criar
  createOpen, openCreate, closeCreate,
  creating, setCreating,

  // Editar
  editTarget,  // T | null — item sendo editado
  openEdit,    // (item: T) => void
  closeEdit,
  editing, setEditing,

  // Excluir
  deleteTarget, // T | null
  openDelete,   // (item: T) => void
  closeDelete,
  deleting, setDeleting,
} = useCRUDDialog<PlanoPlataformaResponse>();
```

### `useInactivity`

```tsx
useInactivity({
  enabled: !!user,
  onWarn: (minutes) => showSnackbar(`Você está inativo há ${minutes} min`),
  onTimeout: () => logout(),
});
```

---

## Componentes Reutilizáveis

### UI

| Componente | Props principais | Descrição |
|-----------|-----------------|-----------|
| `AlertBanner` | `message, severity` | Banner de erro/sucesso inline dentro do card |
| `ConfirmDialog` | `open, title, message, onConfirm, onClose, loading` | Dialog genérico de confirmação com botão de loading |
| `DataList` | `loading, error, empty, children` | Card wrapper com estados de loading / empty / erro |
| `EmptyState` | `message, icon?` | Estado vazio centralizado com ícone MUI |
| `InfoLine` | `label, value` | Label + valor em linha (para telas de detalhe) |
| `LoadingSpinner` | — | `CircularProgress` centralizado na tela |
| `Logo` | `size?` | Logo forzion.tech em texto estilizado |
| `ResponsiveTable` | `columns, rows, total, page, pageSize, onPageChange, onPageSizeChange` | Tabela desktop; cards mobile com Divider; paginação MUI |
| `SnackbarProvider` | — | Contexto global de notificação via `useSnackbar()` |
| `StatusChip` | `status, type` | Chip colorido para `AlunoStatus`, `TreinadorStatus` ou `VinculoStatus` |

### Pagamento

| Componente | Props principais | Descrição |
|-----------|-----------------|-----------|
| `PagamentoPix` | `pagamentoId, onPago?` | Exibe QR Code + copia-e-cola; polling de 30 s para detectar confirmação; estados: Pendente / Pago / Expirado / Falhou |
| `PagamentoCartao` | `pagamentoId, onPago?` | Stripe `<Elements>` com `clientSecret`; `PaymentElement` + `stripe.confirmPayment`; suporta 3DS com redirect |

Ambos usam `pagamentoApi.obterPagamento` para buscar `PagamentoResponse` e renderizam conforme `status` + `metodoPagamento`.

### Forms

| Componente | Descrição |
|-----------|-----------|
| `FormTextField` | `TextField` MUI integrado com `react-hook-form` (`register`, `errors`) |
| `FormSelect` | `Select` MUI integrado com `react-hook-form` |
| `PasswordField` | `FormTextField` com ícone de toggle de visibilidade |

---

## Utilitários e Constantes

### `src/lib/utils/formatting.ts`

| Função | Parâmetros | Retorno | Descrição |
|--------|-----------|---------|-----------|
| `formatarSeries(series)` | `SerieConfigResponse[]` | `string` | Ex: `"3×8–12 / 2×15"` |
| `formatarData(iso)` | `string` (ISO 8601) | `string` | Ex: `"09/05"` |
| `getWeekLabel(dateStr)` | `string` | `string` | Início da semana `"DD/MM"` |
| `periodoParaDatas(periodo)` | `"7d" \| "30d" \| "90d"` | `{ de, ate }` | Datas para filtro de histórico |

### `src/lib/utils/excel.ts`

Exportação de ficha de treino para Excel (`.xlsx`) via **ExcelJS** (`exceljs@^4.4.0`).

| Exportação | Assinatura | Descrição |
|------------|-----------|-----------|
| `exportarFichaParaExcel(params)` | `FichaExportParams → Promise<void>` | Gera e faz download do arquivo `.xlsx` via Blob + `URL.createObjectURL` |
| `buildFichaRows(params)` | `FichaExportParams → (string\|number\|null)[][]` | Monta matriz de linhas; pura (sem I/O); retorna valores brutos |
| `sanitizeFilename(nome)` | `string → string` | Remove chars inválidos/perigosos do nome do arquivo |
| `safeCell(v)` | `string\|number\|null → string\|number\|null` | Prefixia strings que começam com `=`, `+`, `-`, `@`, `\|`, `%` com `'` para prevenir formula injection no ExcelJS |

**Estrutura do Excel gerado** (9 colunas): `#`, Exercício, Qtd Séries, Reps Mín, Reps Máx, Descrição, Carga (kg), Descanso (s), Observação — uma linha por grupo de séries; exercício e observação repetidos apenas na primeira linha.

**Segurança**: `sanitizeFilename` remove path traversal (`.`, `/`, `\`), null byte, angle brackets e formula triggers no nome do arquivo. `safeCell` previne formula injection nas células — necessário porque ExcelJS avalia strings iniciadas com `=` como fórmulas (ao contrário do SheetJS que usava type `'s'`). O `'` prefixado é o marcador de string forçada do Excel e não é exibido ao usuário.

Disponível em: `/treinador/treinos/[treinoId]` (botão "Exportar") e `/aluno/fichas/[fichaId]` (botão "Exportar").

### `src/lib/rateLimit.ts`

Rate limiter in-memory para `POST /api/auth` (rota de login).

| Parâmetro | Valor |
|-----------|-------|
| Limite | 10 requests por janela |
| Janela | 60 segundos |
| IP preferencial | `x-real-ip` (set pelo nginx, não spoofável) |
| Fallback | Último IP de `x-forwarded-for` |

`getClientIp` prioriza `x-real-ip` (definido pelo nginx via `proxy_set_header X-Real-IP $remote_addr`) em vez de `x-forwarded-for` (que pode ser forjado pelo cliente antes de chegar ao nginx).

### `src/lib/constants/labels.ts`

| Constante | Tipo | Uso |
|-----------|------|-----|
| `OBJETIVO_LABEL` | `Record<string, string>` | `ObjetivoTreino` → label PT-BR |
| `OBJETIVOS` | `ObjetivoTreino[]` | Lista de valores de enum |
| `OBJETIVOS_FILTRO` | `{ value, label }[]` | Opções para `<Select>` de filtro |
| `DIFICULDADE_LABEL` | `Record<DificuldadeTreino, string>` | `DificuldadeTreino` → label PT-BR |
| `DIFICULDADES` | `{ value, label, color }[]` | Com cor para chip (verde/laranja/vermelho) |
| `GRUPO_MUSCULAR_LABEL` | `Record<string, string>` | Grupos musculares → label PT-BR |
| `ALUNO_STATUS_COLORS` | `Record<AlunoStatus, string>` | Status → cor hex |

### `src/lib/constants/enrollmentOptions.ts`

Opções de `<Select>` para o formulário de cadastro de aluno:

| Constante | Valores |
|-----------|---------|
| `DIAS_OPTIONS` | Dias de treino por semana |
| `TEMPO_OPTIONS` | Tempo de treino por sessão |
| `FINALIDADE_OPTIONS` | Finalidade do treino (Hipertrofia, Emagrecimento, etc.) |
| `NIVEL_OPTIONS` | Nível de experiência (Iniciante, Intermediário, Avançado) |

---

## Padrões de Desenvolvimento

### Lista paginada com filtros

```tsx
const [filtroStatus, setFiltroStatus] = useState("");

const fetcher = useCallback(
  (p: number, ps: number) =>
    api.listarAlunos({ pagina: p + 1, tamanhoPagina: ps, status: filtroStatus || undefined })
       .then(r => r.data),
  [filtroStatus]
);

const { items, total, page, pageSize, loading, error, setPage, setPageSize, reload } =
  usePaginatedList<AlunoResponse>({ fetcher });

// Ao mudar filtro: reset para página 0
const handleStatus = (v: string) => { setFiltroStatus(v); setPage(0); };
```

### CRUD completo (criar + editar + excluir)

```tsx
const dialog = useCRUDDialog<PlanoPlataformaResponse>();
const { reload } = usePaginatedList<PlanoPlataformaResponse>({ fetcher });

// Criar
const handleCriar = async (data: CriarPlanoForm) => {
  dialog.setCreating(true);
  try {
    await api.criarPlano(data);
    dialog.closeCreate();
    reload();
  } finally {
    dialog.setCreating(false);
  }
};

// JSX
<Button onClick={dialog.openCreate}>Novo</Button>

<CriarPlanoDialog
  open={dialog.createOpen}
  loading={dialog.creating}
  onSubmit={handleCriar}
  onClose={dialog.closeCreate}
/>

<ConfirmDialog
  open={!!dialog.deleteTarget}
  title="Excluir plano?"
  message={`Excluir "${dialog.deleteTarget?.nome}"?`}
  loading={dialog.deleting}
  onConfirm={handleExcluir}
  onClose={dialog.closeDelete}
/>
```

### Dialogs MUI v9

MUI v9 substituiu `PaperProps` por `slotProps`. Todos os `<Dialog>` com altura máxima usam:

```tsx
<Dialog
  open={open}
  onClose={onClose}
  maxWidth="xs"
  fullWidth
  slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } }}
>
```

### Validação de formulário com Zod + React Hook Form

```tsx
const schema = z.object({
  nome: z.string().min(2, "Mínimo 2 caracteres"),
  preco: z.coerce.number().positive("Preço deve ser positivo"),
});

type Form = z.infer<typeof schema>;

const { register, handleSubmit, formState: { errors } } = useForm<Form>({
  resolver: zodResolver(schema),
});

// JSX
<FormTextField
  label="Nome"
  {...register("nome")}
  error={!!errors.nome}
  helperText={errors.nome?.message}
/>
```

### Schemas Zod reutilizáveis (`lib/validations/common.ts`)

| Schema | Uso |
|--------|-----|
| `emailSchema` | E-mail válido |
| `loginPasswordSchema` | Senha login (sem complexidade mínima) |
| `registerPasswordSchema` | Senha cadastro (min 8, uppercase, lowercase, dígito) |
| `loginSchema` | Form de login |
| `cadastroTreinadorSchema` | Form de cadastro treinador |
| `cadastroAlunoSchema` | Form de cadastro aluno (incl. campos de perfil) |

---

## API Client

`src/lib/api/client.ts` — instância Axios com:
- `baseURL`: `NEXT_PUBLIC_API_BASE_URL ?? "/api/backend"` (proxy server-side)
- **Sem** request interceptor — token nunca exposto a JS
- Response interceptor: `401` → `window.location.href = "/login"`
- Respostas de erro seguem **RFC 7807** (`ProblemDetails`)

### Módulos por área

| Módulo | Funções principais |
|--------|-------------------|
| `lib/api/admin.ts` | `listTreinadores`, `aprovarTreinador`, `reprovarTreinador`, `inativarTreinador`, `excluirTreinador`, `atribuirPlano`, `listPlanos`, `criarPlano`, `atualizarPlano`, `excluirPlano`, `listGruposMusculares`, `criarGrupo`, `atualizarGrupo`, `excluirGrupo`, `listExerciciosGlobais`, `criarExercicioGlobal`, `atualizarExercicioGlobal`, `excluirExercicioGlobal` — **visibilidade admin:** `listAlunos`, `getAluno`, `getAlunoVinculo`, `getAlunoFichas`, `getFichaDetalhe`, `getAlunoExecucoes`, `getAlunoProgressao`, `getTreinadorAlunos`, `getTreinadorVinculos`, `getTreinadorTreinos`, `getTreino`, `getTreinadorPacotes` |
| `lib/api/treinador.ts` | `listarAlunos`, `obterAluno`, `atualizarAluno`, `listarVinculos`, `aprovarVinculo`, `desvincularAluno`, `listarTreinos`, `listarExercicios`, `criarExercicio`, `atualizarExercicio`, `excluirExercicio`, `copiarExercicioGlobal`, `listarPacotes`, `criarPacote`, `atualizarPacote` |
| `lib/api/aluno.ts` | `listarFichas`, `obterFicha`, `listarExecucoes`, `registrarExecucao`, `obterVinculo`, `solicitarTrocaTreinador` |
| `lib/api/pagamento.ts` | `iniciarOnboarding`, `verificarOnboarding`, `gerarCobranca`, `obterPagamento`, `listarPagamentosAssinatura`, `obterAssinatura` |
| `lib/api/conta.ts` | `obterPerfil`, `atualizarPerfil`, `alterarSenha`, `logout` |

---

## Tema e Responsividade

### Paleta

Paleta: **amarelo** (`#F5C400`) / **preto** (`#1A1A1A`) / **vermelho** (`#D32F2F`)

| Token MUI | Valor |
|-----------|-------|
| `primary.main` | `#F5C400` |
| `primary.contrastText` | `#1A1A1A` |
| `secondary.main` | `#1A1A1A` |
| `error.main` | `#D32F2F` |
| `background.default` | `#F5F5F5` |
| `background.paper` | `#FFFFFF` |
| Font | Roboto (Google Fonts) |
| `borderRadius` | 8px |

Localização: `ptBR` (MUI + Day.js).

### Responsividade Mobile / iOS

| Problema | Solução |
|----------|---------|
| `100vh` ≠ viewport visível no iOS (barra de endereço) | `100dvh` em todos os layouts (`PublicLayout`, `AppLayout`, `globals.css`) |
| Input `font-size < 16px` causa zoom automático no iOS Safari | Override `MuiOutlinedInput.input { fontSize: 1rem }` no tema; reduz para `0.875rem` em `sm+` |
| BottomNav sobrepõe conteúdo com home indicator (iPhone) | `pb: "env(safe-area-inset-bottom, 0px)"` no Paper + `viewport-fit=cover` no `layout.tsx` |
| Dialogs saem da tela com teclado virtual aberto | `slotProps={{ paper: { sx: { maxHeight: "calc(100dvh - 32px)" } } } }` em todos os Dialogs |
| Tabelas largas overflow no mobile | Todas as tabelas envolvidas em `<Box sx={{ overflowX: "auto" }}>` |
| QR Code Pix distorce em telas pequenas | `width: "100%", maxWidth: 220, height: "auto", aspectRatio: "1 / 1"` |

**Viewport** configurado em `src/app/layout.tsx` via export `Viewport`:

```ts
export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  viewportFit: "cover", // necessário para env(safe-area-inset-*)
};
```

**globals.css**:

```css
html, body {
  min-height: 100%;
  min-height: 100dvh; /* fallback → dvh */
}
body {
  padding-left: env(safe-area-inset-left, 0px);
  padding-right: env(safe-area-inset-right, 0px);
}
```

---

## Segurança

### Cabeçalhos HTTP (`next.config.ts`)

| Cabeçalho | Valor | Propósito |
|-----------|-------|-----------|
| `Content-Security-Policy` | `default-src 'self'` + Next.js + Emotion | Previne XSS |
| `X-Frame-Options` | `DENY` | Previne clickjacking |
| `X-Content-Type-Options` | `nosniff` | Previne MIME sniffing |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Limita Referer header |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | Força HTTPS (HSTS) |
| `Permissions-Policy` | camera, microphone, geolocation: `()` | Bloqueia APIs sensíveis |

### Mecanismos de autenticação

| Mecanismo | Detalhe |
|-----------|---------|
| Token JWT | Nunca exposto ao browser — apenas em cookies httpOnly |
| Token no body | **Removido**: login retorna dados do usuário sem o campo `token` |
| `SameSite: strict` | Cookies não enviados em requests cross-site |
| HMAC verification | `/api/auth/me` usa `jose.jwtVerify` com `JWT_SECRET` — token forjado no cookie é rejeitado |
| Middleware | Base64-decode apenas (performance); verificação HMAC em `/api/auth/me` + backend |
| Proxy server-side | Browser nunca vê a URL real do backend |
| Rate limit | 10 req/60s por IP real (`x-real-ip` nginx, não spoofável via `x-forwarded-for`) |

### Outros mecanismos

| Mecanismo | Detalhe |
|-----------|---------|
| Validação de formulários | Zod + React Hook Form em todos os forms públicos |
| Senhas de cadastro | Mínimo 8 chars, uppercase, lowercase, dígito |
| Mensagens de erro | Login/cadastro nunca exibe detalhes internos do backend |
| Stripe Publishable Key | `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` é pública por design — tokeniza cartões no browser sem expor dados ao servidor; a chave secreta fica exclusivamente no backend |
| Formula injection (Excel) | `safeCell` prefixia strings com `'` para prevenir execução de fórmulas no ExcelJS |

Build configurado como `output: "standalone"` para deploy em container.

---

## Testes

Harness completo em camadas. Os documentos de plano/rationale por fase ficam em `docs/` (gitignored — ver `.gitignore` na raiz); consulte-os localmente se presentes.

### Vitest — 3 projects

`vitest.config.mts` define 3 projects com env e setup próprios. Testes são **co-localizados** (ao lado do código); só infra fica em `src/test/`.

| Project | Env | Escopo | Setup |
|---------|-----|--------|-------|
| `unit` | node | `src/lib/**`, `src/hooks/**`, `middleware` | `src/test/setup/unit.ts` |
| `integration` | jsdom | componentes e páginas React (`*.test.tsx`) | `src/test/setup/integration.ts` |
| `api` | node | route handlers (`src/app/api/**`) | `src/test/setup/api.ts` |

> jsdom (não happy-dom): a Fase 1 migrou para jsdom@26 + determinismo — time/random/uuid/motion travados em `src/test/determinism/` para zero flake.

### Mock de API — MSW

`apiClient` real exercitado contra **MSW** (sem `vi.mock` do client — pega bug de URL/serialização). Handlers em `src/test/msw/handlers/`, factories zod em `src/test/factories/`, tipos gerados do OpenAPI do backend (`npm run openapi:sync`). Drift de contrato barrado por `openapi:check`.

### Property-based, a11y e visual

- **fast-check** (`*.property.test.ts`): invariantes de formatação/validação.
- **vitest-axe**: a11y de componentes (WCAG 2.1 AA) no project integration.
- **@axe-core/playwright** + snapshots visuais: a11y e regressão visual de páginas no E2E.

### E2E — Playwright

5 projects (3 desktop + 2 mobile) + project `setup` que gera storage states de auth por papel. Specs em `e2e/specs/` (`smoke`, `critical`, `security`, `lgpd`, `multi-tab`, `network`, `a11y`, `visual`). **Fail-loud**: falham com mensagem clara quando faltam `E2E_BASE_URL`/`E2E_*`, não silenciam. Rodam **pós-deploy** contra homolog (`smoke.yml`), fora do gate de PR.

### Storybook / mutation / contract

- **Storybook 10** (`.storybook/`) com `addon-a11y` + `msw-storybook-addon`; `storybook:test` roda interações via test-runner.
- **Stryker** (`test:mutation`): mutation score em `src/lib` + `src/hooks` (thresholds 85/75, break 75). Semanal no CI.
- **Pact** (`test:contract`): contratos consumer-driven publicados num broker self-hosted; `can-i-deploy` antes do deploy. A verificação do provider (backend) roda no workflow `pact-provider.yml`.

### Observabilidade

Sentry (`instrumentation*.ts`, `sentry.*.config.ts`) + Web Vitals RUM (`src/components/observability/WebVitals.tsx`). Gated por `NEXT_PUBLIC_SENTRY_DSN` (no-op sem DSN). `global-error.tsx` captura erros do root layout.

### Cobertura

Thresholds **por camada** em `vitest.config.mts` (lines/branches/functions/statements):

| Camada | Threshold |
|--------|-----------|
| `src/lib/**` | 95 / 90 / 95 / 95 |
| `src/hooks/**` | 90 / 85 / 90 / 90 |
| `src/components/**` | 85 / 75 / 85 / 85 |
| `src/app/api/**` | 90 / 85 / 90 / 90 |

Relatório comentado no PR via `vitest-coverage-report-action` (sem SaaS). Backend: resumo no run.

### Suíte

Testes **co-localizados** com o código (`*.test.ts`/`*.test.tsx` ao lado do alvo) — não há mais um diretório central de specs. Os projects do Vitest selecionam por path (ver tabela acima). Property tests em `*.property.test.ts`, a11y em `*.a11y.test.tsx`, contratos Pact em `src/test/pact/`, E2E em `e2e/specs/`.

Rode `npm run test:coverage` para o panorama atual.

### Armadilhas conhecidas

| Problema | Causa | Solução |
|---------|-------|---------|
| `NextRequest.cookies` não parseia header em testes | API interna do Next.js não disponível em unit tests | Mock direto do objeto `{ cookies: { get: vi.fn() } }` |
| `jwtVerify` falha em testes de `/api/auth/me` | Necessita JWT assinado com HMAC real | Usar `jose.SignJWT` com `TEST_SECRET` + `beforeAll(() => { process.env.JWT_SECRET = TEST_SECRET })` |
| Formatação de moeda com non-breaking space | `toLocaleString("pt-BR")` produz `R$ 99,90` — `getByText("R$ 99,90")` falha | Usar `getByText((c) => c.includes("99,90"))` |
| Base64 padding em JWT de teste | `btoa(payload).replace(/=/g, "")` → `atob` falha silenciosamente | Usar `btoa` sem strip dos `=` |
| `onTimeout` chamado múltiplas vezes | `setInterval` continua após `TIMEOUT_MS` | Usar `toHaveBeenCalled()`, não `toHaveBeenCalledOnce()` |
| Mock de módulo com constructor (`new ExcelJS.Workbook()`) | `vi.fn(() => instance)` com arrow function não é construtível | Usar `vi.hoisted` + `class WorkbookMock { addWorksheet = fn; xlsx = { writeBuffer: fn }; }` e referenciar em `vi.mock("exceljs", () => ({ default: { Workbook: excelMocks.WorkbookMock } }))` |
| MUI Select + `getByLabelText` | MUI Select não associa label ao controle via atributo `for` | Usar `getByRole("combobox")` em vez de `getByLabelText("Status")` |
| `@stripe/stripe-js` em testes | `loadStripe` dispara fetch para o SDK Stripe, falha em happy-dom | Mockar `@stripe/stripe-js` e `@stripe/react-stripe-js` com `vi.mock` antes dos imports |
| `PaperProps` em MUI v9 | MUI v9 removeu `PaperProps` do `Dialog` em favor de `slotProps` | Usar `slotProps={{ paper: { sx: ... } }}` em todos os `<Dialog>` |
