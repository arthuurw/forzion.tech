# forzion.tech — Frontend

Interface web da plataforma forzion.tech para personal trainers e alunos.

---

## Stack

| | Tecnologia |
|--|------------|
| Framework | Next.js 16 (App Router) |
| UI | MUI v9 (Material UI) + Emotion |
| Linguagem | TypeScript 5 |
| Forms | React Hook Form 7 + Zod 4 |
| HTTP | Axios 1.x |
| Estado global | Zustand 5 |
| Datas | Day.js |
| Runtime | React 19 |

---

## Pré-requisitos

- Node.js 20+
- API backend rodando (ver `../README.md`)

---

## Comandos

```bash
# Instalar dependências
npm install

# Desenvolvimento (http://localhost:3000)
npm run dev

# Build de produção
npm run build

# Iniciar build de produção
npm start

# Testes (single run)
npm run test

# Testes em modo watch
npm run test:watch
```

---

## Testes

| Ferramenta | Uso |
|---|---|
| Vitest 4 | Runner + assertions |
| happy-dom | Simulação de DOM |
| @testing-library/react | Render de componentes + queries |
| @testing-library/user-event | Simulação de interações |
| @testing-library/jest-dom | Matchers extras (`toBeDisabled`, etc.) |

Arquivos em `src/test/`:

| Arquivo | O que testa | Testes |
|---|---|---|
| `validations.test.ts` | Schemas Zod — email, senha, nome, telefone, login, cadastro aluno (incl. campos de perfil obrigatórios) | 24 |
| `auth.test.ts` | `extractTipoConta` + `homeRouteFor` | 8 |
| `api-auth-me.test.ts` | Handler `GET /api/auth/me` (cookies, JWT, expiração) | 5 |
| `useInactivity.test.ts` | Hook de inatividade com fake timers | 6 |
| `components.test.tsx` | `StatusChip`, `EmptyState`, `ConfirmDialog` | 11 |

**Total: 54 testes**

Configuração em `vitest.config.mts` + `src/test/setup.ts`.

---

## Variáveis de Ambiente

Crie um arquivo `.env.local` na raiz de `frontend/`:

```env
# URL base da API backend
API_BASE_URL=https://localhost:7220

# Exposta ao browser (usada pelo cliente Axios)
NEXT_PUBLIC_API_BASE_URL=https://localhost:7220
```

> Em produção, `API_BASE_URL` é usada pelas Route Handlers do Next.js (server-side). `NEXT_PUBLIC_API_BASE_URL` é usada pelo cliente Axios no browser.

---

## Estrutura

```
src/
├── app/
│   ├── (admin)/              # Área do SystemAdmin
│   │   └── admin/
│   │       ├── page.tsx      # Dashboard admin
│   │       ├── treinadores/  # Listagem e aprovação de treinadores
│   │       └── planos/       # Gestão de planos globais
│   ├── (aluno)/              # Área do Aluno
│   │   └── aluno/
│   │       ├── fichas/       # Lista de fichas
│   │       │   └── [fichaId]/
│   │       │       ├── page.tsx      # Detalhe da ficha
│   │       │       └── executar/     # Modo execução passo a passo
│   │       └── historico/    # Histórico de execuções
│   ├── (treinador)/          # Área do Treinador
│   │   └── treinador/
│   │       ├── alunos/       # Lista de alunos + detalhe
│   │       ├── treinos/      # Fichas de treino + detalhe
│   │       ├── exercicios/   # Biblioteca de exercícios
│   │       └── pacotes/      # Pacotes de fichas
│   ├── (public)/             # Rotas públicas (sem auth)
│   │   ├── login/
│   │   └── cadastro/
│   │       ├── treinador/
│   │       └── aluno/
│   ├── api/                  # Route Handlers (BFF)
│   │   └── auth/
│   │       ├── route.ts            # POST /api/auth — login
│   │       ├── logout/route.ts     # POST /api/auth/logout
│   │       ├── register/
│   │       │   ├── treinador/route.ts
│   │       │   └── aluno/route.ts
│   │       └── treinadores/        # Listagem pública de treinadores/pacotes
│   ├── perfil/               # Página de perfil do usuário autenticado
│   ├── layout.tsx            # Root layout — AuthProvider + SnackbarProvider
│   ├── page.tsx              # Landing page
│   └── not-found.tsx
├── components/
│   ├── forms/                # FormTextField, FormSelect, PasswordField
│   ├── layout/               # AppLayout, AppHeader, PublicLayout, NavConfig
│   ├── treinador/            # ProgressaoAluno
│   └── ui/                   # AlertBanner, ConfirmDialog, DataList, EmptyState,
│                             # InfoLine, LoadingSpinner, Logo, ResponsiveTable,
│                             # SnackbarProvider, StatusChip
├── hooks/
│   ├── useCRUDDialog.ts      # Estado de dialogs criar/editar/excluir
│   ├── useInactivity.ts      # Auto-logout por inatividade
│   └── usePaginatedList.ts   # Lista paginada genérica com loading/error/reload
├── lib/
│   ├── api/                  # Clientes Axios por área (admin, aluno, treinador, conta)
│   ├── auth/                 # AuthContext, session helpers
│   ├── constants/
│   │   ├── labels.ts         # Maps enum → label PT-BR (OBJETIVO_LABEL, GRUPO_MUSCULAR_LABEL, …)
│   │   └── enrollmentOptions.ts  # Opções de select do cadastro aluno
│   ├── theme/                # Tema MUI (paleta + tipografia + componentes)
│   ├── utils/
│   │   └── formatting.ts     # formatarSeries, formatarData, getWeekLabel, periodoParaDatas
│   └── validations/          # Schemas Zod reutilizáveis
├── proxy.ts                  # Middleware de proteção de rotas
└── types/                    # Types/interfaces compartilhados
```

---

## Autenticação e Sessão

O fluxo usa três cookies complementares:

| Cookie | `httpOnly` | Uso |
|--------|-----------|-----|
| `token` | ✅ | Lido pelo middleware e por `/api/auth/me` para validar sessão — não acessível via JS |
| `session_guard` | ✅ | Flag de sessão válida; ausência indica sessão inválida mesmo com `token` presente |
| `token_access` | ❌ | Lido pelo interceptor Axios no browser para injetar `Authorization: Bearer` |

Todos os cookies têm `maxAge` derivado do `exp` do JWT — expiram junto com o token.

### Login

1. Página `/login` chama `POST /api/auth` (Route Handler)
2. Route Handler repassa para `POST /auth/login` na API backend
3. Em caso de sucesso, define os três cookies (`token`, `session_guard`, `token_access`) e retorna dados ao browser
4. `AuthContext.login()` atualiza o estado React
5. Browser redireciona para a área correspondente ao `TipoConta`

`AuthContext` valida a sessão no mount via `GET /api/auth/me` (server-side, lê cookies httpOnly).

### Logout

1. Chama `POST /api/auth/logout`
2. Route Handler apaga todos os cookies
3. `AuthContext.logout()` limpa o estado e redireciona para `/login`

### Middleware de Rotas (`middleware.ts`)

Protege todas as rotas exceto assets estáticos e `/api/`. Lê `token` + `session_guard` (ambos httpOnly) e extrai `tipo_conta` do JWT sem verificar assinatura:

- Não autenticado em área protegida → redireciona para `/login`
- Autenticado em `/login` → redireciona para área correta (evita ver o form desnecessariamente)
- Autenticado em área errada (ex: Aluno em `/admin`) → redireciona para área correta
- `/` e `/cadastro/*` → sempre acessíveis independente de autenticação

---

## Tema

Paleta: **amarelo** (`#F5C400`) / **preto** (`#1A1A1A`) / **vermelho** (`#D32F2F`)

| Token | Valor |
|-------|-------|
| `primary.main` | `#F5C400` |
| `secondary.main` | `#1A1A1A` |
| `error.main` | `#D32F2F` |
| `background.default` | `#F5F5F5` |
| Font | Roboto (Google Fonts) |
| `borderRadius` | 8px |

Localização: `ptBR` (MUI).

---

## Rotas por Perfil

| `TipoConta` | Área | Páginas |
|-------------|------|---------|
| `SystemAdmin` | `/admin` | Dashboard, treinadores (listagem + aprovação + inativação + plano), planos globais |
| `Treinador` | `/treinador` | Dashboard, alunos, fichas, exercícios, pacotes |
| `Aluno` | `/aluno` | Fichas, execução passo a passo, histórico |
| Todos | `/perfil` | Dados da conta |

---

## API Client

`src/lib/api/client.ts` — instância Axios com:
- `baseURL` apontando para a API backend
- Interceptor de request que injeta `Authorization: Bearer <token>` lendo o cookie `token_access`
- Respostas de erro seguem o formato **RFC 7807** (`ProblemDetails`)

Módulos por área:
- `lib/api/admin.ts` — treinadores, planos
- `lib/api/treinador.ts` — alunos, vínculos, fichas, exercícios, pacotes
- `lib/api/aluno.ts` — fichas, execuções
- `lib/api/conta.ts` — perfil

---

## Segurança

O `next.config.ts` define cabeçalhos HTTP em todas as respostas:

| Cabeçalho | Valor |
|-----------|-------|
| `Content-Security-Policy` | `default-src 'self'` + permissões mínimas para Next.js + MUI Emotion |
| `X-Frame-Options` | `SAMEORIGIN` |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | camera, microfone, geolocalização desativados |

Build configurado como `output: "standalone"` para deploy em container.
