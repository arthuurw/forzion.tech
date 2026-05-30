# specification-frontend — frontend (forzion.tech)

DOC PARA AGENTES. Fonte de verdade da arquitetura do frontend. Formato denso, agent-oriented. Consultar antes de qualquer alteração de rota, auth, layout, API proxy, testes ou segurança. Cross-ref: [specification-infrastructure] (build/deploy/nginx), [specification-db] (domínio/enums), [specification-frontend-ui] (design tokens/componentes/a11y), [specification-seo] (metadata/OG/crawl), [specification-observability] (RUM Web Vitals/Sentry), [specification-security] (CSP/headers/rate-limit frontend).

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA de mudança relevante em: rotas, grupos de layout, auth, API proxy, componentes estruturais, strategy de testes, deps críticas, headers de segurança.
- Vive em `specs/` (versionado; commitar). NÃO duplicar infra/DB — referenciar specs próprios.

## STACK
- Next.js 16 (App Router) + React 19 + TypeScript 6. `frontend/`.
- UI: MUI v9 (`@mui/material`) + Emotion. Locale `ptBR`. Tema em `src/lib/theme/index.ts`.
- Forms: react-hook-form v7 + Zod v4 (`@hookform/resolvers`).
- HTTP client: axios (instância `apiClient` em `src/lib/api/client.ts`).
- JWT client-side: `jose` (verificação no Route Handler `/api/auth/me`).
- Pagamentos: `@stripe/react-stripe-js` + `@stripe/stripe-js`.
- Relatórios: `exceljs` (geração de planilhas cliente).
- Estado global: Zustand (disponível; uso pontual).
- Datas: `dayjs`.
- Observabilidade: Sentry (`@sentry/nextjs`) via `withSentryConfig` em `next.config.ts`.

## CONFIGURAÇÃO DE BUILD (`next.config.ts`)
- `output: "standalone"` — container otimizado; necessário para o `docker-compose.homolog.yml`.
- `turbopack` (dev): `root: path.resolve(__dirname)`.
- `optimizePackageImports`: `@mui/material`, `@mui/icons-material` (reduz bundle).
- `withSentryConfig`: source maps só sobem com `SENTRY_AUTH_TOKEN`; `next build` sem token funciona (sem upload). `sourcemaps.disable: !SENTRY_AUTH_TOKEN`.
- `withBundleAnalyzer`: ativo via `ANALYZE=true` (script `analyze`).
- `API_BASE_URL` obrigatório em `NODE_ENV=production` (throw em build se ausente).

## SEGURANÇA — HEADERS HTTP
Aplicados via `next.config.ts` em todas as rotas (`source: "/(.*)"`) com `securityHeaders`:

| Header | Valor |
|--------|-------|
| `Content-Security-Policy` | `default-src 'self'; script-src 'self' 'unsafe-inline'[+'unsafe-eval' só dev] https://js.stripe.com; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: https://*.stripe.com; font-src 'self'; connect-src 'self' https://api.stripe.com https://*.sentry.io; frame-src https://js.stripe.com; worker-src 'self' blob:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'` |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` |
| `X-Frame-Options` | `DENY` |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |
| `Content-Security-Policy-Report-Only` | igual ao CSP, só se `CSP_REPORT_ONLY=true` (homolog) |

- `'unsafe-inline'` em `script-src`: necessário para hidratação Next.js sem nonce.
- `'unsafe-eval'` em `script-src`: apenas dev (MUI Emotion + hot-reload).
- `blob:` em `worker-src`: Sentry Session Replay.

## TYPESCRIPT
- `tsconfig.json`: `target=ES2017`, `module=esnext`, `moduleResolution=bundler`, `strict=true`, `isolatedModules=true`, `jsx=react-jsx`. Path alias `@/*` → `./src/*`.
- TS6 ativo. `.npmrc` com `legacy-peer-deps=true` (madge@8 peerOptional TS `^5.4.4` conflita com TS6; funcional em runtime).

## ESTRUTURA DE DIRETÓRIOS (`src/`)
```
src/
  app/
    (admin)/           — grupo Admin (layout próprio, AppLayout)
    (aluno)/           — grupo Aluno (layout próprio, AppLayout)
    (treinador)/       — grupo Treinador (layout próprio, AppLayout)
    (public)/          — grupo público (layout próprio, PublicLayout)
    _landing/          — componentes da homepage
    api/
      auth/            — Route Handlers de autenticação (login, logout, me, register, etc.)
      backend/[...path]/ — proxy reverso → backend .NET
    perfil/            — página de perfil (acessível a todos os roles)
    layout.tsx         — root layout
    page.tsx           — homepage /
    error.tsx          — error boundary de rota
    global-error.tsx   — error boundary global
    not-found.tsx
  components/
    forms/             — FormTextField, FormSelect, PasswordField
    layout/            — AppLayout, AppHeader, PublicLayout, NavConfig
    observability/     — WebVitals
    pagamento/         — PagamentoCartao, PagamentoPix
    treinador/         — componentes específicos do treinador
    ui/                — componentes compartilhados (ErrorBoundary, SnackbarProvider, LoadingSpinner, DataList, etc.)
  hooks/               — useInactivity, usePaginatedList, useCRUDDialog
  lib/
    api/               — client.ts + módulos por domínio (admin, aluno, treinador, conta, pagamento)
    auth/              — context.tsx, jwt.ts, session.ts
    constants/         — enrollmentOptions, labels
    rateLimit.ts       — rate limiter em memória (login)
    theme/             — ThemeRegistry.tsx, index.ts (MUI theme)
    utils/             — formatting.ts, excel.ts
    validations/       — common.ts (schemas Zod)
  middleware.ts        — RBAC redirect (Next.js middleware)
  styles/globals.css
  test/                — infraestrutura de testes (MSW, factories, setup, pact)
  types/index.ts       — tipos de domínio compartilhados
```

## ROOT LAYOUT (`src/app/layout.tsx`)
Árvore de providers (ordem importa):
```
html[lang=pt-BR]
  body
    WebVitals
    AppRouterCacheProvider (MUI v16-appRouter)
      ThemeRegistry
        ErrorBoundary
          AuthProvider
            SnackbarProvider
              {children}
```
- `metadata`: title="forzion.tech", description.
- `viewport`: `width=device-width, initialScale=1, viewportFit=cover`.

## GRUPOS DE ROTA E LAYOUTS
| Grupo | Prefixo | Layout | Auth |
|-------|---------|--------|------|
| `(public)` | `/login`, `/cadastro/*`, `/forgot-password`, `/reset-password`, `/verify-email`, `/resend-verification` | PublicLayout | livre |
| `(admin)` | `/admin/*` | AppLayout | SystemAdmin |
| `(treinador)` | `/treinador/*` | AppLayout | Treinador |
| `(aluno)` | `/aluno/*` | AppLayout | Aluno |
| raiz | `/`, `/perfil` | root layout | livre/all |

## MIDDLEWARE (`src/middleware.ts`)
Executa em todas as rotas exceto `_next/static`, `_next/image`, `favicon.ico`, `api/`. Lê cookies `token` + `session_guard` (ambos necessários); extrai `TipoConta` via `extractTipoConta` (decodifica JWT client-side, valida `exp`).

Lógica de redirect:
1. `/cadastro/*` → sempre `next()` (livre).
2. `!tipoConta && !isPublic` → redirect `/login`.
3. `tipoConta && pathname === "/login"` → redirect para área do role (`/admin`, `/treinador`, `/aluno`).
4. `tipoConta` acessando área de outro role → redirect para área correta.

## AUTH FLOW
```
Cliente → POST /api/auth (Next.js Route Handler)
  → rate limit 10 req/60s por IP
  → fetch API_BASE/auth/login
  → resposta: { token, tipoConta, contaId, perfilId }
  → seta cookies httpOnly: token (Bearer JWT, maxAge=exp-now), session_guard=1 (flag)
  → retorna { tipoConta, contaId, perfilId } (token NÃO exposto ao JS)

GET /api/auth/me → jwtVerify (jose, JWT_SECRET + JWT_ISSUER + JWT_AUDIENCE)
  → extrai conta_id, tipo_conta, perfil_id do payload
  → retorna SessionUser | null

POST /api/auth/logout
  → chama API_BASE/conta/logout com Bearer (invalida JTI no backend)
  → delete cookies token + session_guard
  → falha silenciosa (cookies deletados de qualquer forma)
```

**AuthProvider** (`src/lib/auth/context.tsx`): estado `user: SessionUser | null` + `isLoading`. Chama `/api/auth/me` no mount. Expõe `login(data)`, `logout()`, `homeRouteFor(tipoConta)`.

**SessionUser** nunca contém o token — token permanece só em cookie httpOnly.

## API PROXY (`src/app/api/backend/[...path]/route.ts`)
- Proxeia todos os métodos HTTP (GET/POST/PUT/PATCH/DELETE) para `API_BASE/<path>?<query>`.
- `API_BASE`: env `API_BASE_URL` ?? `https://localhost:7220`.
- Sanitiza path: rejeita segmentos `.` ou `..` (400).
- Repassa apenas headers `content-type` e `accept` do cliente (nunca Cookie, Authorization, etc.).
- Injeta `Authorization: Bearer <token>` do cookie httpOnly.
- Nunca expõe o token ao cliente.

## API ROUTES DE AUTH (Next.js Route Handlers)
| Rota | Método | Descrição |
|------|--------|-----------|
| `/api/auth` | POST | Login → seta cookies httpOnly |
| `/api/auth/me` | GET | Verifica JWT → retorna SessionUser |
| `/api/auth/logout` | POST | Invalida backend + limpa cookies |
| `/api/auth/register/treinador` | POST | Cadastro treinador (rate limit, proxy) |
| `/api/auth/register/aluno` | POST | Cadastro aluno (rate limit, proxy) |
| `/api/auth/verify-email` | POST | Verificação de e-mail |
| `/api/auth/forgot-password` | POST | Solicita reset |
| `/api/auth/reset-password` | POST | Executa reset |
| `/api/auth/resend-verification` | POST | Reenvio de verificação |
| `/api/auth/treinadores` | GET | Listagem pública de treinadores (cadastro aluno) |
| `/api/auth/treinadores/[id]/pacotes` | GET | Pacotes do treinador (cadastro aluno) |

**Rate limit** (`src/lib/rateLimit.ts`): 10 req/60s por IP. Mapa em memória por processo (não persistido entre restarts). Aplicado em login e register.

## API CLIENT (`src/lib/api/client.ts`)
```
apiClient = axios.create({ baseURL: NEXT_PUBLIC_API_BASE_URL ?? "/api/backend" })
interceptor resposta:
  401 → window.location.href = "/login"
  403 + data.code === "ASSINATURA_INADIMPLENTE" → dispatch CustomEvent
        `forzion:assinatura-inadimplente` em window (NÃO redireciona; só notifica).
        AppLayout escuta e renderiza toast (regularizar em Pagamentos).
```
- `ASSINATURA_INADIMPLENTE_EVENT` + `ASSINATURA_INADIMPLENTE_MESSAGE` exportados do client.
- Enforcement server-side: backend `RequireAssinaturaAtivaFilter` retorna 403 `ASSINATURA_INADIMPLENTE` em endpoints restritos (ex.: POST execuções). Cross-ref inadimplência: [specification-stripe].
- Módulos de domínio em `src/lib/api/`: `admin.ts`, `aluno.ts`, `treinador.ts`, `conta.ts`, `pagamento.ts`.

## APPLAYOUT (autenticado)
- Verifica `!isLoading && !user` → chama `/api/auth/logout` (limpa cookies) → `router.replace("/login")`.
- Desktop (≥md): `Drawer` permanente, colapsável (232px ↔ 68px ícones).
- Mobile (<md): `Drawer` temporário + `BottomNavigation` fixo (inferior, com `safe-area-inset-bottom`).
- `NavConfig` por `TipoConta`: items de navegação derivados do role.
- **Inatividade**: `useInactivity` — warn aos N minutos, logout automático aos 20 min.

## TEMA MUI (`src/lib/theme/index.ts`)
- `primary`: #F5C400 (amarelo), `secondary`: #1A1A1A. `background.default`: #F7F8FA.
- Font: Inter (Google Fonts, pesos 400/500/600/700, `display: swap`).
- `borderRadius`: 12 (global), 16 (Card/Paper), 10 (Button/Input).
- `ptBR` locale aplicado.
- Input `fontSize: 1rem` em mobile → previne zoom automático iOS Safari.

## RESPONSIVIDADE
- Breakpoints MUI (`xs/sm/md/lg`): layout adapta em `<md` para mobile.
- `viewportFit: "cover"` + `env(safe-area-inset-bottom)` para iPhone notch.
- BottomNavigation com `showLabels: navItems.length <= 4`.
- Padding do main: `p: {xs:2.5, md:3.5}`, `pb: {xs:"calc(72px+safe-area)", md:3.5}`.

## TIPOS DE DOMÍNIO (`src/types/index.ts`)
- `TipoConta`: `SystemAdmin | Treinador | Aluno`
- `SessionUser`: `{ contaId, tipoConta, perfilId }` (sem token)
- `LoginResponse`: `{ token, tipoConta, contaId, perfilId }`
- `ProblemDetails`: RFC 7807 `{ title, detail?, status, errors?, code? }`
- `PaginatedResponse<T>`: `{ items, total, pagina, tamanhoPagina }`
- Responses de domínio: `AlunoResponse`, `TreinadorResponse`, `VinculoResponse`, `TreinoResponse`, `ExercicioResponse`, `PlanoPlataformaResponse`, `GrupoMuscularResponse`, `PacoteResponse`, `AssinaturaAlunoResponse`, `PagamentoResponse`, `ExecucaoTreinoResponse`, etc.
- Enums: `AlunoStatus`, `TreinadorStatus`, `VinculoStatus`, `TreinoAlunoStatus`, `ObjetivoTreino`, `DificuldadeTreino`, `FinalidadeTreino`, `NivelCondicionamento`, `TempoDisponivel`, `AssinaturaAlunoStatus`, `PagamentoStatus`, `MetodoPagamento`, `TierPlano`.

## ELITE "EM BREVE"
Plano tier=Elite indisponível para seleção/atribuição. Três pontos de aplicação:
- **Landing** (`_landing/`): card Elite não-clicável (sem link de ação) + badge "Em breve".
- **Admin — planos** (`(admin)/planos`): dropdown `TierPlano` com opção `Elite` `disabled` (visível mas não selecionável).
- **Admin — treinadores** (`(admin)/treinadores`): formulário de atribuição de plano exclui `Elite` das opções listadas.
Backend rejeita `AtribuirPlano` com tier=Elite → `PlanoPlataformaErrors.EliteIndisponivel` (422). Cross-ref [specification-model].

## TESTES (`vitest.config.mts`)
3 projects vitest:

| Project | Env | Pool | Setup | Include |
|---------|-----|------|-------|---------|
| `unit` | node | threads | `src/test/setup/unit.ts` | `src/lib/**/*.test.ts`, `src/hooks/**/*.test.ts`, `src/middleware.test.ts` |
| `integration` | jsdom | forks | `src/test/setup/integration.ts` | `src/components/**/*.test.tsx`, `src/app/**/__tests__/*.test.tsx`, `src/lib/utils/excel.test.ts`, `src/lib/auth/context.test.tsx`, `src/lib/api/admin.msw.test.ts` |
| `api` | node | threads | `src/test/setup/api.ts` | `src/app/api/**/*.test.ts` |

**Coverage (v8)**:
| Camada | Lines | Branches | Functions |
|--------|-------|----------|-----------|
| `src/lib/**` | 95% | 90% | 95% |
| `src/hooks/**` | 90% | 85% | 90% |
| `src/components/**` | 85% | 75% | 85% |
| `src/app/api/**` | 90% | 85% | 90% |
| `src/app/**` | 70% | 60% | 55% |

**MSW** (`msw@2`): handlers em `src/test/msw/handlers/`. `public/mockServiceWorker.js` para Storybook. Tipos gerados de `openapi.json` via `openapi-typescript` → `src/test/msw/types.ts`.

**Pact** (contract tests): config separada `vitest.pact.config.mts`. Consumer em `src/test/pact/consumer.test.ts`. API: `PactV3 + MatchersV3`. Tests: listFichas, getVinculo, getPerfil, listAlunos.

**Storybook** (v10): `@storybook/nextjs`, `msw-storybook-addon`, `@storybook/addon-a11y`. Port 6006.

**E2E** (Playwright v1.60): `e2e/`. Smoke, security, LGPD tests. Runs pós-deploy no CI (`smoke.yml`).

**Property tests**: `@fast-check/vitest` + `fast-check`. Arquivos `*.property.test.ts`.

**Mutation tests**: Stryker (`@stryker-mutator/vitest-runner`). Script `test:mutation`.

**Acessibilidade**: `vitest-axe` + `@axe-core/playwright` + `@storybook/addon-a11y`.

## FERRAMENTAS DE QUALIDADE / ANÁLISE
| Ferramenta | Script | Função |
|------------|--------|--------|
| ESLint 9 (flat config) | `lint` | Lint TS/TSX, plugins: next, jest-dom, testing-library, playwright, security |
| TypeScript 6 | `typecheck` | `tsc --noEmit` |
| madge | `deadcode` | Circular imports em src/ |
| knip | `knip` | Código não utilizado |
| license-checker | `license` | Fail em GPL/AGPL/LGPL/CDDL/EPL |
| cyclonedx-npm | `sbom` | SBOM CycloneDX JSON (`--ignore-npm-errors --package-lock-only`) |
| `npm audit` | `audit` | Vulnerabilidades deps prod |
| Linkinator | `links` | Links quebrados (localhost:3000) |
| Lighthouse CI | `lhci` | Performance, a11y, best practices |

## SCRIPTS DE VALIDAÇÃO
| Script | Escopo |
|--------|--------|
| `validate` | `typecheck && lint && test` (todos os 3 projects) |
| `security:all` | `audit && license && sbom` |
| `hygiene` | `deadcode && knip` |
| `openapi:check` | `openapi:sync && git diff --exit-code src/test/msw/types.ts` (drift check) |

## ENV / SECRETS (frontend)
| Variável | Onde | Descrição |
|----------|------|-----------|
| `API_BASE_URL` | server-side | URL backend .NET (obrigatório em prod; default `https://localhost:7220` em dev) |
| `NEXT_PUBLIC_API_BASE_URL` | client-side | Base do apiClient (default `/api/backend`; sem NEXT_PUBLIC só usa proxy) |
| `JWT_SECRET` | server-side | Chave para `jose.jwtVerify` em `/api/auth/me` |
| `JWT_ISSUER` | server-side | Issuer JWT para validação |
| `JWT_AUDIENCE` | server-side | Audience JWT para validação |
| `SENTRY_AUTH_TOKEN` | build | Upload de source maps (optional; build funciona sem) |
| `SENTRY_ORG` | build | Org do Sentry |
| `SENTRY_PROJECT` | build | Projeto do Sentry |
| `CSP_REPORT_ONLY` | runtime | Se `true`, adiciona CSP-Report-Only (homolog) |

## OBSERVABILIDADE
- **Sentry**: erros + replay (RUM). DSN configurado no container (no-op sem DSN). Source maps com `SENTRY_AUTH_TOKEN`.
- **WebVitals** (`src/components/observability/WebVitals.tsx`): coleta e envia Core Web Vitals.
- **ErrorBoundary** global em root layout + por grupo de rota. `global-error.tsx` para erros fora do layout.

## DICAS / GOTCHAS
- `legacy-peer-deps=true` em `.npmrc`: necessário para `npm install` com madge@8 + TS6. NÃO remover sem atualizar madge.
- npm override `"@pact-foundation/pact": { "https-proxy-agent": "^7.0.6" }`: pact v16 bundla `https-proxy-agent@9` (ESM puro); override força CJS-compatível. NÃO remover ao atualizar pact.
- jsdom mantido em `^26` (não `^27`): jsdom 27 tem dep `@csstools/css-calc` ESM-only que quebra vitest pool `forks` (Node 20 sem `--experimental-require-module`).
- `SBOM` usa `--ignore-npm-errors` para tolerar `typescript@6 invalid` (peer dep madge) e pacotes `@emnapi/*`/`@napi-rs/*` extraneous (deps opcionais NAPI-RS do pact v16).
- AppLayout detecta auth via `useAuth`; se `!user` chama `/api/auth/logout` antes de redirecionar para /login (evita loop: middleware veria cookie válido e redirecionaria de volta).
- Middleware NÃO interceta `/api/*` (ver `matcher`). Auth das API routes é responsabilidade dos próprios Route Handlers.
- `NEXT_PUBLIC_API_BASE_URL` não definido em prod → apiClient usa `/api/backend` (proxy Next.js) — correto para produção onde não há acesso direto ao backend.
- `API_BASE_URL` é server-side (sem NEXT_PUBLIC) → NÃO exposto ao browser; usado apenas nos Route Handlers.
- Rate limit é em memória por processo; em deploy multi-instância (horizontal) não é compartilhado — aceitável para homolog/prod single-instance.
