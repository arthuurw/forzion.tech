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
```

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
│   └── ui/                   # AlertBanner, ConfirmDialog, EmptyState,
│                             # LoadingSpinner, Logo, SnackbarProvider, StatusChip
├── lib/
│   ├── api/                  # Clientes Axios por área (admin, aluno, treinador, conta)
│   ├── auth/                 # AuthContext, session helpers
│   ├── theme/                # Tema MUI (paleta + tipografia + componentes)
│   └── validations/          # Schemas Zod reutilizáveis
├── proxy.ts                  # Middleware de proteção de rotas
└── types/                    # Types/interfaces compartilhados
```

---

## Autenticação e Sessão

O fluxo usa dois cookies complementares:

| Cookie | `httpOnly` | Uso |
|--------|-----------|-----|
| `token` | ✅ | Lido pelo middleware (`proxy.ts`) para proteger rotas — não acessível via JS |
| `tipoConta` | ✅ | Lido pelo middleware para redirecionar para a área correta |
| `token_access` | ❌ | Lido pelo interceptor Axios no browser para injetar o `Authorization: Bearer` |
| `user_data` | ❌ | JSON com `{ token, tipoConta, contaId, perfilId }` — lido por `loadSession()` |

### Login

1. Página `/login` chama `POST /api/auth` (Route Handler)
2. Route Handler repassa para `POST /auth/login` na API backend
3. Em caso de sucesso, define os quatro cookies e retorna os dados ao browser
4. `AuthContext.login()` atualiza o estado React com os dados do usuário
5. Browser redireciona para a área correspondente ao `TipoConta`

### Logout

1. Chama `POST /api/auth/logout`
2. Route Handler apaga todos os cookies
3. `AuthContext.logout()` limpa o estado e redireciona para `/login`

### Middleware de Rotas (`proxy.ts`)

Protege todas as rotas exceto assets estáticos e `/api/`:

- Não autenticado tentando acessar área protegida → redireciona para `/login`
- Autenticado tentando acessar rota pública → redireciona para a área do seu `TipoConta`
- Autenticado acessando área errada (ex: Aluno tentando `/admin`) → redireciona para a área correta

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
