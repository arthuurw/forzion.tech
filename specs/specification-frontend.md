# specification-frontend — frontend (forzion.tech)

DOC AGENTES (denso). Fonte de verdade da arquitetura frontend. Atualizar NA MESMA TAREFA ao mudar rota/layout/auth/API-proxy/componente-estrutural/testes/dep-crítica/header-segurança. NÃO duplicar infra/DB/tokens — referenciar. Cross-ref: [specification-infrastructure] (build/deploy/nginx), [specification-db] (domínio/enums), [specification-frontend-ui] (design tokens/componentes/a11y), [specification-seo] (metadata/OG/crawl), [specification-observability] (RUM/Sentry), [specification-security] (CSP/headers/rate-limit), [specification-stripe] (billing treinador/Pix/onboarding/webhook), [specification-model] (modos de pagamento/máquinas de estado).

## STACK
- Next.js 16 (App Router) + React 19 + TypeScript 6. `frontend/`.
- UI: MUI v9 (`@mui/material`) + Emotion. Locale `ptBR`. Tema em `src/lib/theme/index.ts`.
- Forms: react-hook-form v7 + Zod v4 (`@hookform/resolvers`).
- HTTP client: axios (instância `apiClient` em `src/lib/api/client.ts`).
- JWT client-side: `jose` (verificação no Route Handler `/api/auth/me`).
- Pagamentos: `@stripe/react-stripe-js` + `@stripe/stripe-js`.
- Relatórios: `exceljs` (geração de planilhas cliente).
- Datas: `dayjs`.
- Observabilidade: Sentry (`@sentry/nextjs`) via `withSentryConfig` em `next.config.ts`.

## CONFIGURAÇÃO DE BUILD (`next.config.ts`)
- `output: "standalone"` — container otimizado; necessário para o `docker-compose.homolog.yml`.
- `turbopack` (dev): `root: path.resolve(__dirname)`.
- `optimizePackageImports`: `@mui/material`, `@mui/icons-material` (reduz bundle).
- `withSentryConfig`: source maps só sobem com `SENTRY_AUTH_TOKEN`; `next build` sem token funciona (sem upload). `sourcemaps.disable: !SENTRY_AUTH_TOKEN`.
- `withBundleAnalyzer`: ativo via `ANALYZE=true` (script `analyze`).
- `API_BASE_URL` obrigatório em `NODE_ENV=production` (throw em build se ausente).
- `JWT_SECRET` obrigatório em `NODE_ENV=production` (throw em build se ausente).

## SEGURANÇA — HEADERS HTTP
Headers de segurança + CSP aplicados na camada Next via `next.config.ts` (`headers()`/`buildCsp`, `source: "/(.*)"`). **Tabela completa de headers, string CSP e as 3 camadas (app/Next/edge): canônico em [specification-security] §3** — não reproduzido aqui. Específico do frontend: a CSP vive SÓ na camada Next (só o front serve HTML ao browser); `'unsafe-inline'` em script-src é exigido pela hidratação Next sem nonce, `'unsafe-eval'` só em dev (MUI/HMR), `blob:` em worker-src p/ Sentry Replay. CSP entregue só enforcing (sem Report-Only — ver [specification-security] §3).

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
    aluno/             — SemVinculoAtivoBanner (aviso aluno sem vínculo ativo)
    forms/             — FormTextField, FormSelect, PasswordField
    layout/            — AppLayout, AppHeader, PublicLayout, NavConfig
    observability/     — WebVitals
    pagamento/         — PagamentoCartao, PagamentoPix, PagamentoSignup (anônimo, props-driven)
    treinador/         — componentes específicos do treinador
    ui/                — componentes compartilhados (ErrorBoundary, AlertBanner, LoadingSpinner, DataList, etc.)
  hooks/               — useInactivity, usePaginatedList, useCRUDDialog, useConsent, useCursorList, useExecucaoDraft, useExecucaoRetryQueue
  lib/
    api/               — client.ts, extractApiError.ts + módulos por domínio (admin, aluno, treinador, conta, pagamento)
    auth/              — context.tsx, jwt.ts, helpers.ts, buildPlaceholder.ts
    constants/         — enrollmentOptions, labels
    rateLimit.ts       — rate limiter em memória (login)
    theme/             — ThemeRegistry.tsx, index.ts (MUI theme)
    utils/             — formatting.ts, excel.ts
    validations/       — common.ts (schemas Zod)
  proxy.ts             — RBAC redirect (Next.js proxy; ex-middleware, runtime nodejs)
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
            ConsentProvider
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

## PROXY (`src/proxy.ts`)
Convenção `middleware.ts` foi deprecada no Next 16 → arquivo é `proxy.ts`, export nomeado `proxy`; runtime é `nodejs` (proxy NÃO suporta edge). Guard usa `jose`/`NextResponse` (agnóstico de runtime); `config.matcher` inalterado.
Executa em todas as rotas exceto `_next/static`, `_next/image`, `favicon.ico`, `api/`. Verifica `token` + `session_guard` via `jwtVerify` (assinatura+`exp` autoritativos). Lê também `refresh` (httpOnly) e o hint `tipo_conta` (NÃO-httpOnly, R-FE — só roteamento; backend autoritativo via jti/policies). `tipoConta` efetivo = verificado ?? hint (quando há refresh).

Lógica de redirect:
1. `/cadastro/*` → sempre `next()` (livre).
2. `!verificado && !refresh && !isPublic` → redirect `/login`. **Com `refresh` presente (access expirado), NÃO bounceia** — deixa passar p/ a renovação silenciosa client-side (`/api/auth/refresh`) / `/api/auth/me` salvar a sessão.
3. `tipoConta && pathname === "/login"` → redirect para área do role.
4. `tipoConta` acessando área de outro role → redirect para área correta (hint roteia attacker p/ a própria área; backend nega o resto).

## AUTH FLOW
```
Cliente → POST /api/auth (Next.js Route Handler)
  → rate limit 10 req/60s por IP
  → fetch API_BASE/auth/login
  → resposta: { token, refreshToken, tipoConta, contaId, perfilId, nome }
  → seta cookies (helper applySessionCookies): token httpOnly (maxAge=exp-now),
    refresh httpOnly (maxAge=idle do papel: Admin 2h / demais 7d),
    tipo_conta NÃO-httpOnly (hint de roteamento), session_guard httpOnly (flag)
  → retorna { tipoConta, contaId, perfilId, nome } (token E refresh NÃO expostos ao JS)

  MFA habilitado: backend responde { mfaRequerido, mfaPendingToken, mfaPendingExpiraEm }
  → route seta cookie httpOnly `mfa_pending` (applyMfaPendingCookie); devolve só
    { mfaRequerido:true, mfaPendingExpiraEm } — o token pending NUNCA vai ao JS.
  → login envia cookie `trusted_device` ao backend (se presente) p/ pular o 2º fator.

POST /api/auth/mfa/verificar (conclui o 2º fator)
  → lê cookie httpOnly `mfa_pending` → Bearer → API_BASE/auth/mfa/verificar { codigo, fator, lembrarDispositivo }
  → sucesso: applySessionCookies + clearMfaPendingCookie (+ applyTrustedDeviceCookie se lembrou) → SessionUser
POST /api/auth/mfa/email/enviar → repassa `mfa_pending` → envia OTP por e-mail

POST /api/auth/refresh (proxy de renovação silenciosa)
  → repassa cookie httpOnly `refresh` → API_BASE/auth/refresh (rotação single-use + reuse detection)
  → sucesso: reescreve token+refresh+tipo_conta rotacionados | 401: limpa cookies de sessão

GET /api/auth/me → jwtVerify (jose). Access válido → SessionUser.
  → access expirado/ausente MAS refresh presente: dispara o refresh server-side
    (rotaciona + reescreve cookies) antes de devolver → sessão sobrevive a reload com access vencido.
  → sem refresh / refresh morto → null (+ limpa cookies)

client.ts (interceptor axios): 401 ⇒ tenta /api/auth/refresh UMA vez (flag `_retry` anti-loop;
  promise compartilhada anti-tempestade de refresh concorrente) → refaz a request original;
  refresh falho ⇒ window.location='/login'.

POST /api/auth/logout
  → chama API_BASE/conta/logout com Bearer (invalida JTI + revoga família do refresh no backend)
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
| `/api/auth` | POST | Login → seta cookies httpOnly (token+refresh+session_guard) + hint tipo_conta |
| `/api/auth/refresh` | POST | Renovação silenciosa: repassa cookie refresh → rotaciona token+refresh, ou 401+limpa |
| `/api/auth/me` | GET | Verifica JWT → SessionUser; access vencido + refresh → rotaciona server-side |
| `/api/auth/logout` | POST | Invalida backend (jti + família) + limpa cookies |
| `/api/auth/mfa/verificar` | POST | Conclui 2º fator: lê cookie `mfa_pending` → Bearer; seta sessão + trusted_device |
| `/api/auth/mfa/email/enviar` | POST | Envia OTP de login por e-mail (repassa `mfa_pending`) |
| `/api/auth/register/treinador` | POST | Cadastro treinador (rate limit, proxy). Body inclui `planoPlataformaId`+`modoPagamentoAluno` |
| `/api/auth/register/aluno` | POST | Cadastro aluno (rate limit, proxy) |
| `/api/auth/planos` | GET | Planos da plataforma p/ wizard de cadastro (proxy `/auth/planos`, `cache: "no-store"`, sem rate limit) |
| `/api/auth/treinador/[treinadorId]/pagamento` | POST | Inicia pagamento do plano no signup (rate limit, proxy `/auth/treinador/{id}/pagamento`). Body `{ metodo }` |
| `/api/auth/verify-email` | POST | Verificação de e-mail |
| `/api/auth/forgot-password` | POST | Solicita reset |
| `/api/auth/reset-password` | POST | Executa reset |
| `/api/auth/resend-verification` | POST | Reenvio de verificação |
| `/api/auth/treinadores` | GET | Listagem pública de treinadores (cadastro aluno) |
| `/api/auth/treinadores/[treinadorId]/pacotes` | GET | Pacotes do treinador (cadastro aluno) |

**Rate limit** (`src/lib/rateLimit.ts`): 10 req/60s por IP. Mapa em memória por processo (não persistido entre restarts). Aplicado em login e register.

**Cliente das auth routes** (`src/lib/api/auth.ts`): módulo `authApi` (wrapper `fetch` fino — NÃO `apiClient`, pois estas rotas setam cookie httpOnly e não usam Bearer). Páginas públicas (login, cadastro aluno/treinador) chamam `authApi.*` em vez de `fetch("/api/auth/...")` cru. Erros via `AuthApiError` (carrega `status` + `ProblemDetails`).

## API CLIENT (`src/lib/api/client.ts`)
```
apiClient = axios.create({ baseURL: NEXT_PUBLIC_API_BASE_URL ?? "/api/backend" })
interceptor resposta:
  401 → tenta /api/auth/refresh UMA vez (flag _retry), refaz; senão → "/login"
  403 + code === "step_up_requerido" → requestStepUp() UMA vez (flag _stepUpRetry);
        token obtido → refaz a request com header `X-Step-Up-Token`.
  403 + data.code === "ASSINATURA_INADIMPLENTE" → dispatch CustomEvent
        `forzion:assinatura-inadimplente` em window (NÃO redireciona; só notifica).
        AppLayout escuta e renderiza toast (regularizar em Pagamentos).
```
- **Step-up** (`lib/auth/stepUpController.ts`): registry `registerStepUpHandler`/`requestStepUp` (promise in-flight compartilhada anti-tempestade). `StepUpProvider` (`components/seguranca/`) registra um handler que abre `StepUpDialog` (pede TOTP ou OTP por e-mail → `POST /auth/step-up/iniciar`+`/verificar`) e resolve com o token `step_up`; o interceptor injeta no header e refaz a request. Token NÃO persistido — vive só na request retried.
- `ASSINATURA_INADIMPLENTE_EVENT` + `ASSINATURA_INADIMPLENTE_MESSAGE` exportados do client.
- Enforcement server-side: backend `RequireAssinaturaAtivaFilter` retorna 403 `ASSINATURA_INADIMPLENTE` em endpoints restritos (ex.: POST execuções). Cross-ref inadimplência: [specification-stripe].
- Módulos de domínio em `src/lib/api/`: `admin.ts`, `aluno.ts`, `treinador.ts`, `conta.ts`, `pagamento.ts`, `nfse.ts`.

## APPLAYOUT (autenticado)
- Verifica `!isLoading && !user` → chama `/api/auth/logout` (limpa cookies) → `router.replace("/login")`.
- Desktop (≥md): `Drawer` permanente, colapsável (232px ↔ 68px ícones).
- Mobile (<md): `Drawer` temporário + `BottomNavigation` fixo (inferior, com `safe-area-inset-bottom`).
- `NavConfig` por `TipoConta`: items de navegação derivados do role. `NavItem.drawerOnly?: boolean` marca itens secundários que aparecem só no `Drawer`, nunca na `BottomNavigation` mobile. Treinador: 8 itens no drawer (Alunos, Fichas, Exercícios, Pacotes, Notas fiscais, Recebimentos[drawerOnly], Plano[drawerOnly], Suporte); bottom-nav filtra `drawerOnly` → 6.
- **Inatividade**: `useInactivity` — warn aos 25 min (5 min antes), logout automático aos 30 min.

## SEGURANÇA / MFA (`src/app/seguranca/`)
Página autenticada de segurança da conta (link no `AppHeader`/nav). Cliente em `lib/api/mfa.ts` (via `apiClient` → `/conta/mfa/*`). Tipos em `types/index.ts` (`MfaStatus`, `CompletarMfaResponse`, `LoginResponse.mfaRequerido/mfaPendingToken`, etc.).
- **Ativar TOTP**: `POST /conta/mfa/totp/iniciar` → exibe segredo + QR (URI `otpauth://`) → usuário confirma código → `POST /conta/mfa/totp/confirmar` → mostra os **10 recovery codes UMA vez** (`RecoveryCodesPanel`, download/cópia; raw não re-exibido).
- **Status/gestão**: `GET /conta/mfa/status` (habilitado, recovery restantes, dispositivos confiáveis); `POST /desabilitar` e `POST /recovery/regenerar` exigem **step-up** (disparam `StepUpDialog` via interceptor `step_up_requerido`).
- **Login 2ª etapa** (`(public)/login/page.tsx`): resposta `mfaRequerido` → tela do 2º fator (TOTP / recovery / "enviar código por e-mail" → `/api/auth/mfa/email/enviar`) + checkbox "lembrar dispositivo" → `/api/auth/mfa/verificar`. O token pending vive só no cookie httpOnly `mfa_pending` (nunca no JS).
- a11y (foco/erro/labels) conforme [specification-frontend-ui].

## TEMA MUI (`src/lib/theme/index.ts`)
Básico aqui; tokens exatos (paleta/radius/tipografia/component-defaults/anti-zoom) em [specification-frontend-ui] §DESIGN TOKENS — NÃO duplicar.
- Marca: `primary` amarelo, `secondary` quase-preto, `background.default` cinza claro. Font Inter (Google Fonts, `display:swap`). `ptBR` locale.

## RESPONSIVIDADE
- Breakpoints MUI default; layout adapta em `<md` para mobile (detalhes/tokens em [specification-frontend-ui] §RESPONSIVIDADE).
- **Alvo de verificação = 360px; target-size AA = 24px (não 44px=AAA); CHECKLIST anti-regressão mobile + padrões (kebab de ações, hit-area de dot, ResponsiveTable obrigatória) em [specification-frontend-ui] §RESPONSIVIDADE — aplicar ao mexer em qualquer página/componente.**
- `viewportFit: "cover"` + `env(safe-area-inset-bottom)` para iPhone notch.
- BottomNavigation opera sobre `navItems.filter(i => !i.drawerOnly)` (itens `drawerOnly` ficam só no drawer); `showLabels: bottomNavItems.length <= 4`.
- Padding do main: `p: {xs:2.5, md:3.5}`, `pb: {xs:"calc(72px+safe-area)", md:3.5}`.

## TIPOS DE DOMÍNIO (`src/types/index.ts`)
- `TipoConta`: `SystemAdmin | Treinador | Aluno`
- `SessionUser`: `{ contaId, tipoConta, perfilId }` (sem token)
- `LoginResponse`: `{ token, tipoConta, contaId, perfilId }`
- `ProblemDetails`: RFC 7807 `{ title, detail?, status, errors?, code? }`
- `PaginatedResponse<T>`: `{ items, total, pagina, tamanhoPagina }`
- Responses de domínio: `AlunoResponse`, `TreinadorResponse`, `VinculoResponse`, `TreinoResponse`, `ExercicioResponse`, `PlanoPlataformaResponse`, `GrupoMuscularResponse`, `PacoteResponse`, `AssinaturaAlunoResponse`, `PagamentoResponse`, `ExecucaoTreinoResponse`, etc.
- Enums: `AlunoStatus`, `TreinadorStatus`, `VinculoStatus`, `TreinoAlunoStatus`, `ObjetivoTreino`, `DificuldadeTreino`, `FinalidadeTreino`, `NivelCondicionamento`, `TempoDisponivel`, `AssinaturaAlunoStatus`, `PagamentoStatus`, `MetodoPagamento`, `TierPlano`.
- Billing treinador / modo de pagamento (cross-ref [specification-stripe], [specification-model]):
  - `TreinadorStatus`: `AguardandoPagamento | AguardandoAprovacao | Ativo | Inativo` (`AguardandoPagamento` = plano pago aguardando pagamento no signup).
  - `ModoPagamentoAluno`: `Plataforma | Externo` (como o aluno paga o treinador).
  - `IniciarPagamentoPlanoResponse`: `{ pagamentoId, valor, status, metodoPagamento, stripePaymentIntentId?, pixQrCode?, pixQrCodeUrl?, pixExpiracao?, clientSecret?, createdAt }` — payload do signup (Pix ou Cartão).
  - `OnboardingStatusResponse`: `{ onboardingCompleto, contaConfigurada, modoPagamentoAluno, modoPagamentoPodeAlterarEm }` (`modoPagamentoPodeAlterarEm`: data em que a troca de modo volta a ser permitida — servidor já soma o cooldown de 90d; null se nunca trocou; UI só compara com a data atual).
  - `AssinaturaTreinadorResponse` (`status: AssinaturaTreinadorStatus = Pendente|Ativa|Inadimplente|Cancelada`), `TrocarPlanoTreinadorResponse` (`tipo: TipoTrocaPlano = Upgrade|Downgrade|InadimplenteRegularizacao|UpgradeImediato`), `PagamentoTreinadorStatusResponse`.

## ELITE "EM BREVE"
Plano tier=Elite indisponível para seleção/atribuição. Três pontos de aplicação:
- **Landing** (`_landing/`): card Elite não-clicável (sem link de ação) + badge "Em breve".
- **Admin — planos** (`(admin)/planos`): dropdown `TierPlano` com opção `Elite` `disabled` (visível mas não selecionável).
- **Admin — treinadores** (`(admin)/treinadores`): formulário de atribuição de plano exclui `Elite` das opções listadas.
Backend rejeita `AtribuirPlano` com tier=Elite → `PlanoPlataformaErrors.EliteIndisponivel` (422). Cross-ref [specification-model].

## BILLING TREINADOR + MODO DE PAGAMENTO DO ALUNO
Fluxos de cadastro/cobrança e como o `modoPagamentoAluno` muda a UI. Regra de negócio/webhook/Pix em [specification-stripe]; máquinas de estado em [specification-model]. Aqui só o frontend.

### Wizard de cadastro do treinador (`(public)/cadastro/treinador/page.tsx`)
2 passos client-side (estado `step: 1|2`, sem rota nova):
- **Passo 1** — form (react-hook-form + Zod `cadastroTreinadorSchema`): dados + plano (radio, `GET /api/auth/planos` filtrando `isAtivo !== false`) + `modoPagamentoAluno` (radio Plataforma/Externo, default `Plataforma`). Submit → `POST /api/auth/register/treinador` com `planoPlataformaId`+`modoPagamentoAluno`.
  - Resposta `status === "AguardandoPagamento"` (plano pago) → guarda `treinadorId`, vai p/ passo 2.
  - Senão (Free → `AguardandoAprovacao`) → tela final `"analise"` ("cadastro em análise").
- **Passo 2** — escolhe Pix/Cartão → `POST /api/auth/treinador/{id}/pagamento` `{ metodo }` → recebe `IniciarPagamentoPlanoResponse` → renderiza `<PagamentoSignup>`. Pix marca tela final imediata; Cartão marca final via callback `onPagoCartao`.
- Telas finais (`finalizado: analise|pix|cartao`): card com CTA "Ir para o login". Pix embute `<PagamentoSignup>` (sem polling); Cartão/analise mostram mensagem ("verifique e-mail" / "em análise").

### `PagamentoSignup` (`components/pagamento/PagamentoSignup.tsx`)
Componente ANÔNIMO, props-driven `{ pagamento: IniciarPagamentoPlanoResponse, onPagoCartao }` — NÃO usa `apiClient` autenticado (signup pré-conta).
- **Pix**: QR (`pixQrCodeUrl`) + copia-e-cola (`pixQrCode`, botão copiar) + expiração. SEM polling — webhook backend finaliza e dispara e-mail de verificação (Alert informa).
- **Cartão**: Stripe `<Elements>` com `clientSecret` + `<PaymentElement>` → `stripe.confirmPayment({ redirect: "if_required" })` → `onPagoCartao()`. Sem `clientSecret` → Alert de erro. `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` (ausente → `stripePromise=null`). Erro de recusa exibido via `mapStripeError(error)` (`lib/pagamento/stripeErro`): mapeia `decline_code`/`code` conhecidos → cópia pt-BR curada; desconhecido → SEMPRE `FALLBACK` pt-BR (NUNCA ecoa `error.message` em inglês do Stripe ao usuário).

### Troca de plano (`(treinador)/treinador/plano/page.tsx`)
Plano atual (`GET /treinador/plano/assinatura` via `pagamentoApi.obterAssinaturaTreinador`) + chip status (Ativa=success, Inadimplente=error, Cancelada=default, demais=warning) + lista de planos (`listarPlanosPlataforma`, exclui `Elite`/inativos). Dialog de troca (`pagamentoApi.trocarPlano`):
- `Downgrade`/`UpgradeImediato` → aplica direto, recarrega.
- Upgrade c/ proração via Pix → exibe QR + **polling** 5s (`obterStatusPagamentoTreinador` até `Pago`) → sucesso. **Proração (T9)**: `estimarProracao` (renomeado de `calcularProracao`) calcula `(novoPlano.preco - planoAtual.preco) * diasRestantes / 30` client-side — exibida como **estimativa** (label "Proração estimada: R$ X") nos cards de plano para UX de preview. O **valor autoritativo** é o que o backend retorna em `TrocarPlanoTreinadorResponse.valorPagamento` após `confirmarTroca()` — exibido no dialog de pagamento. O frontend nunca envia o valor calculado ao backend (só `novoPlanoId`).
- Inadimplente → Alert no card + regularização via mesma lista.

### Dashboard treinador (`(treinador)/treinador/page.tsx`)
- Banner onboarding Stripe ("Configure seus recebimentos" → `/treinador/pagamentos`): só quando `onboardingPendente && !modoExterno`. `modoExterno` = `OnboardingStatusResponse.modoPagamentoAluno === "Externo"` (via `verificarOnboarding`); modo Externo NÃO exige Stripe → banner oculto.
- Banner "regularizar pagamento" (erro): quando `obterAssinaturaTreinador().status === "Inadimplente"` → CTA `/treinador/plano`.

### Recebimentos (`(treinador)/treinador/pagamentos/page.tsx`)
Decide por `OnboardingStatusResponse.modoPagamentoAluno`:
- **Externo**: orientação de controle manual (sem Stripe; combinar valor direto, gerenciar acesso por vínculo). Chip "Pagamento externo".
- **Plataforma**: onboarding Stripe (chip status: Ativo/Cadastro incompleto/Não configurado; `iniciarOnboarding` redireciona p/ URL Stripe). A taxa da plataforma NÃO é hardcoded no front — vem do backend (`taxaPlataformaPercent` no envelope de `listarRecebimentos`, authoritative do `PaymentSettings`) e o chip "Taxa da plataforma: N%" mora no bloco de histórico (onde a taxa é aplicada).
- **Troca de modo (opt-out/opt-in)**: ambos os modos exibem ação de alternar (`alterarModoPagamento` → `POST /treinador/modo-pagamento`) com `ConfirmDialog` de consequências (→Externo cancela assinaturas dos alunos; →Plataforma cria assinaturas + exige Stripe) + aceite do cooldown. Cooldown lido direto de `OnboardingStatusResponse.modoPagamentoPodeAlterarEm` (sem constante local): se futura, ação desabilitada + "Novo ajuste disponível em <data>". Erro (422 `configure_stripe_primeiro`/`cooldown_modo_pagamento`) exibido DENTRO do dialog via `extractApiError` (dialog permanece aberto p/ retry).
- **Preview da troca**: `abrirTroca` busca `GET /treinador/modo-pagamento/preview` (`previewModoPagamento`) e injeta frase de impacto no dialog (→Externo "N assinatura(s) serão canceladas"; →Plataforma "Até N assinatura(s) serão criadas"). Falha do preview → dialog abre com aviso e ainda permite confirmar (troca não bloqueada por preview indisponível). Resultado da troca (`assinaturasCriadas`/`vinculosIgnorados`) vira `Alert` de sucesso.
- **Histórico de recebimentos** (`HistoricoRecebimentos`, só modo Plataforma): lista keyset (`listarRecebimentos(cursor?)` → `GET /treinador/pagamentos/recebimentos`; `proximoCursor` → "Carregar mais"). Header tem o chip "Taxa da plataforma: N%" (de `taxaPlataformaPercent` do envelope). Lista TODOS os 6 status; cada item: aluno, data, método, status (chip — Estornado=warning, EmDisputa=error), bruto, taxa%, líquido **estimado** (rotulado; calculado no backend via centavos). `taxaPercent`/`liquidoEstimado` `null` (status `Falhou`/`Expirado` — nunca houve recebimento) → renderiza "—" (não `R$ 0,00`). Vazio = estado amigável. Modo Externo = placeholder (sem lista — pagamentos não passam pela plataforma).

### Aluno sem vínculo ativo
- `components/aluno/SemVinculoAtivoBanner.tsx`: lê `GET /aluno/vinculo` (`alunoApi.getMeuVinculo`). Estado `ativo` (oculto) | `pendente` (aguardando aprovação) | `sem-vinculo`. Mensagem: histórico consultável, registro de novos treinos bloqueado. Renderizado no dashboard (`(aluno)/aluno/page.tsx`) e no histórico (`(aluno)/aluno/historico/page.tsx`).
- Execução (`(aluno)/aluno/fichas/[fichaId]/executar/page.tsx`): trata `403` do registro com mensagem clara ("Você não tem um treinador ativo. Não é possível registrar novos treinos.").

### Orientação de execução do exercício (dicas)
- `components/aluno/ExercicioOrientacao.tsx`: renderiza "Como executar" (texto) + facade de vídeo YouTube (thumbnail `i.ytimg.com` → iframe `youtube-nocookie.com?rel=0` SÓ após clique do aluno; sem `dangerouslySetInnerHTML`; re-valida o `videoId` via `lib/utils/youtube.ts` antes de montar URL/thumb; `aria-label`/`title` com nome do exercício). Não renderiza nada se texto e vídeo ausentes (R5 — card idêntico ao anterior). Wired na tela de execução (após nota do treinador, antes de "Planejado"). Campos `comoExecutar`/`videoId` chegam via `TreinoExercicioResponse` (ficha do aluno enriquecida por `ObterInfoPorIdsAsync`). CSP habilitada em [specification-security] §3.
- `lib/utils/youtube.ts`: `parseYouTubeId` (espelha o VO `YouTubeVideoId` do backend — mesmas formas), `youtubeThumb` (`i.ytimg.com`), `youtubeEmbedUrl` (`youtube-nocookie.com`, `rel=0`) — todos re-validam o id (null se inválido).
- Autoria (treinador `(treinador)/treinador/exercicios`, admin `(admin)/admin/exercicios`): textarea "Como executar" (maxLength 2000) + input "Link do vídeo (YouTube)" com validação client (`parseYouTubeId` null ⇒ helper + submit bloqueado). Editar pré-preenche `videoId` (id puro re-parseável); enviar vazio limpa (PATCH parcial).

### Execução de treino resiliente offline (draft + retry idempotente)
Sessão de execução não perde dados em reload/queda de rede; finalização sobrevive offline sem duplicar. SEM Service Worker/PWA — só `localStorage` + idempotência server-side ([specification-backend], [specification-concurrency §4]).
- **`hooks/useExecucaoDraft.ts`** — autosave/restore/reconcile do rascunho vivo. Chave `exec-draft:{alunoId}:{treinoId}` (NOTA: `treinoId` aqui = `fichaId` da rota = `treinoAlunoId`, escolhido por estar disponível no 1º render e estável no ciclo de load; o hook fica incondicional). Payload versionado `v:1` { idempotencyKey, treinoExercicioIds, execData, obsData, observacao, currentIndex, updatedAt }. `idempotencyKey` = `crypto.randomUUID()` (fallback RFC4122 manual em contexto inseguro), REUSADO entre reloads (mesma sessão → mesma key → dedup) e REGENERADO no `discard`. Autosave debounced 500ms; `restore()` lê sem aplicar (decisão do usuário); `reconcile(exercicios)` casa por `treinoExercicioId` (mantém set do draft, `initExecData` p/ exercício novo, dropa órfão, clampa currentIndex, filtra obsData) e sinaliza `reconciled`; TTL 48h (expirado → descarta + remove); JSON corrompido → descarta seguro.
- **`hooks/useExecucaoRetryQueue.ts`** — fila de finalização offline em `exec-queue` (array de { idempotencyKey, payload, alunoId, treinoId, enqueuedAt, lastError? }). `enqueue`; `drain` reenvia em ordem via `alunoApi.criarExecucao(payload, { idempotencyKey })`: 2xx → remove + `onSuccess(treinoId)`; transitório (status null/offline ou ≥500) → MANTÉM e PARA (preserva ordem); permanente (4xx) → mantém com `lastError` e CONTINUA (sem loop infinito). Drain dispara no mount, no evento `window 'online'`, e manual. Idempotência server-side garante que reenvio/double-drain não duplica.
- **Degradação graciosa (EXOFF-06)**: todo acesso a `localStorage`/`crypto` envolto em try/catch + guard `typeof ... === "undefined"` (SSR/Safari privado/quota) → no-op silencioso, a página NUNCA quebra; sem persistência apenas perde-se o draft/fila.
- **Página executar** integra: autosave do state vivo; banner "Treino em andamento encontrado" (Continuar aplica reconcile + aviso `AlertBanner` se a ficha mudou / Descartar limpa); `handleSubmit` POST com `idempotencyKey` → sucesso limpa draft + tela "Sessão registrada"; offline/5xx → `enqueue` + draft limpo + tela "Sessão salva no aparelho / enviada ao reconectar"; permanente (400/403/404/422) mantém mensagem de erro existente (sem enfileirar falso-pendente).
- **`components/aluno/ExecucaoPendenteBanner.tsx`** (montado no `(aluno)/layout.tsx`): usa `useExecucaoRetryQueue`; oculto quando fila vazia; mostra contagem (singular/plural) + botão "Tentar enviar agora" (dispara `drain`, rotula "Enviando…"/desabilita durante). `role="status"` (info não-bloqueante, contraste com o `role="alert"` do `AlunoInadimplenteBanner`).
- **`lib/execucao/execData.ts`**: `initExecData` + tipo `SetState` extraídos da página p/ reuso pelo hook (módulo compartilhável).

### NFS-e (notas fiscais)
Cliente `lib/api/nfse.ts` (`nfseApi`) + validação/máscaras `lib/validations/dadosFiscais.ts` (CPF/CNPJ por `tipoDocumento`, CEP, IBGE 7 dígitos, UF; máscara só na UI, payload envia dígitos crus). Enums NFS-e como string-literal no módulo (label/cor de status). Nav item "Notas fiscais" em treinador e admin (`ReceiptLongIcon`).
- **Treinador** `(treinador)/treinador/dados-fiscais` — form RHF+Zod (tomador da NFS-e); carrega `GET /treinador/dados-fiscais` (null = nunca preenchido), salva `PUT`. `(treinador)/treinador/notas-fiscais` — lista keyset (`proximoCursor` → "Carregar mais"), download DANFSe (`GET .../danfse` → `window.open(danfseRef)`); só notas com `temDanfse`. Botão "Dados fiscais" no header.
- **Admin** `(admin)/admin/notas-fiscais` — `ResponsiveTable` + filtro de status + retry (`POST .../reprocessar`, só status `Erro` mostra ação); coluna de erro (código+motivo) com tooltip.

### Contato com suporte (`(aluno)/aluno/suporte` + `(treinador)/treinador/suporte`)
- Form compartilhado `components/suporte/SuporteForm.tsx`; as duas páginas são thin (só renderizam o form). Item "Suporte" (`SupportAgentIcon`) no `NavConfig` de aluno e treinador (Admin não vê).
- react-hook-form + `zodResolver(suporteSchema)` (`lib/validations/suporte.ts`: categoria enum Duvida/Sugestao/Outro, assunto 3–120, descrição 20–2000 com `.trim()` espelhando o backend). Nome/E-mail são `disabled`, pré-preenchidos via `contaApi.getPerfil()` (server-authoritative — NÃO do token, que não traz e-mail).
- Submit via `apiClient.post("/suporte/mensagens", {categoria, assunto, descricao})` (catch-all `/api/backend` com Bearer; SEM route proxy dedicado — identidade vem do token no backend). 202 → tela de sucesso; erro → `AlertBanner` (`extractApiError`).

## TESTES (`vitest.config.mts`)
3 projects vitest:

| Project | Env | Pool | Setup | Include |
|---------|-----|------|-------|---------|
| `unit` | node | threads | `src/test/setup/unit.ts` | `src/lib/**/*.test.ts`, `src/lib/**/*.property.test.ts`, `src/hooks/**/*.test.ts`, `src/hooks/**/*.property.test.ts`, `src/proxy.test.ts`, `src/proxy.signature.test.ts` (exclui: hooks RTL/DOM e excel/downloadBlob/admin.msw/auth-context que rodam em `integration`) |
| `integration` | jsdom | forks | `src/test/setup/integration.ts` | `src/components/**/*.test.tsx`, `src/components/**/__tests__/*.test.tsx`, `src/app/**/__tests__/*.test.tsx`, `src/lib/utils/excel.test.ts`, `src/lib/utils/downloadBlob.test.ts`, `src/lib/auth/context.test.tsx`, `src/lib/api/admin.msw.test.ts`, hooks RTL: `useInactivity`, `useConsent`, `usePaginatedList`, `useCRUDDialog`, `useCursorList`, `useExecucaoDraft`, `useExecucaoRetryQueue` |
| `api` | node | threads | `src/test/setup/api.ts` | `src/app/api/**/*.test.ts` |

**Coverage (v8)**: thresholds por glob (l/b/f por camada) — canônico em [specification-tests] §8 (enforced em `vitest run --coverage`).

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

## OBSERVABILIDADE
- **Sentry**: erros + replay (RUM). DSN configurado no container (no-op sem DSN). Source maps com `SENTRY_AUTH_TOKEN`.
- **WebVitals** (`src/components/observability/WebVitals.tsx`): coleta e envia Core Web Vitals.
- **ErrorBoundary**: `app/error.tsx` (root) + `error.tsx` por route-group — `(aluno)`, `(treinador)`, `(admin)` — cada um client (`{error, reset}`) que DELEGA ao componente compartilhado `components/ui/RouteGroupError` (props `homeHref`/`homeLabel`/`bodyText`); layout/Sentry/retry vivem em 1 lugar (sem triplicar markup). Render pt-BR + botão "Tentar novamente" (`reset()`) + link ao painel do grupo + `Sentry.captureException(error)`. `global-error.tsx` para erros fora do layout. O boundary de grupo contém crash de render do segmento preservando o shell.

## TRATAMENTO DE ERRO (padrão canônico)
- **Erro de chamada de API**: capturar no `catch (err)` e exibir via `AlertBanner` (ou `Alert` MUI) com `extractApiError(err, "<fallback pt-BR>")` — surfaça o `detail` (→`title`→`message`) pt-BR do backend; `<fallback>` só quando a resposta não traz texto útil. `extractApiErrorInfo` quando precisar de `status`/`code` (ex.: telas que mapeiam status, como login/executar). NÃO hardcodar mensagem genérica descartando o `detail` do backend.
- **Falha de load de página de detalhe**: `return null` em estado de erro é BANIDO (causa tela branca). Renderizar estado de erro (mensagem + retry) — ver `DetalheErro`/`AlertBanner`.
- `SnackbarProvider`/`useSnackbar` (toast global genérico) foi removido (0 consumidores); o canal de feedback de erro é `AlertBanner` + `extractApiError`. (O MUI `<Snackbar>` do banner de inadimplência no `AppLayout` é caso à parte — ver [specification-stripe].)

## DICAS / GOTCHAS
- `legacy-peer-deps=true` em `.npmrc` (madge@8 + TS6) — ver §TYPESCRIPT; NÃO remover sem atualizar madge.
- npm override `"@pact-foundation/pact": { "https-proxy-agent": "^7.0.6" }`: pact v16 bundla `https-proxy-agent@9` (ESM puro); override força CJS-compatível. NÃO remover ao atualizar pact.
- jsdom mantido em `^26` (não `^27`): jsdom 27 tem dep `@csstools/css-calc` ESM-only que quebra vitest pool `forks` (Node 20 sem `--experimental-require-module`).
- `SBOM` usa `--ignore-npm-errors` para tolerar `typescript@6 invalid` (peer dep madge) e pacotes `@emnapi/*`/`@napi-rs/*` extraneous (deps opcionais NAPI-RS do pact v16).
- AppLayout-logout-antes-de-redirect (evita loop com o proxy): ver §APPLAYOUT.
- Proxy NÃO interceta `/api/*`; auth das API routes é dos próprios Route Handlers: ver §PROXY.
- `API_BASE_URL`/`NEXT_PUBLIC_API_BASE_URL` (server vs client, proxy `/api/backend` em prod): ver §ENV + §API PROXY.
- Rate limit é em memória por processo; em deploy multi-instância (horizontal) não é compartilhado — aceitável para homolog/prod single-instance.
- **Asset `/public` trocado in-place serve versão velha (CANÔNICO)**: sobrescrever um arquivo em `public/` mantendo o MESMO path → URL idêntica não invalida por conteúdo. Stale em 3 camadas: cache do browser, otimizador `next/image` (`/_next/image`, in-memory no dev + `.next/cache/images`), e em prod/homolog o `Cache-Control` longo+`immutable` do nginx/CDN (assets `/public` via `src` string NÃO são content-hashed pelo Next — só imports estáticos são). Reload ou trocar o arquivo NÃO basta; no dev exige `rm -rf .next && restart`. Fix durável quando precisar de asset binário: versionar o NOME (`x-v2.webp`) ao mudar conteúdo. **A landing evita o problema de raiz**: a seção "Como funciona" (`_landing/HowItWorks.tsx`) usa mockups SVG inline (`_landing/StepMockup.tsx`, `variant: ficha|alunos|historico`), não screenshot real — vetorial, sempre nítido/cheio, sem asset em `/public` nem cache stale.
