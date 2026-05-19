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
- [Tema](#tema)
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
| Pagamentos | @stripe/stripe-js + @stripe/react-stripe-js | 9.x / 6.x |
| Testes | Vitest + happy-dom + Testing Library | 4 |

---

## Pré-requisitos

- Node.js 20+
- API backend rodando em `http://localhost:5230` ou `https://localhost:7220` (ver `../README.md`)

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

## Variáveis de Ambiente

Crie `.env.local` na raiz de `frontend/`:

```env
# URL base usada pelo proxy server-side para chamar o backend
# (nunca exposta ao browser)
API_BASE_URL=https://localhost:7220

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
│   │   │       ├── planos/             # CRUD planos globais
│   │   │       ├── grupos-musculares/  # CRUD grupos musculares
│   │   │       └── exercicios/         # Biblioteca global de exercícios
│   │   │
│   │   ├── (treinador)/                # Route group — Treinador
│   │   │   ├── layout.tsx
│   │   │   └── treinador/
│   │   │       ├── page.tsx            # Dashboard (stat cards, donut alunos, vínculos pendentes)
│   │   │       ├── alunos/
│   │   │       │   ├── page.tsx        # Lista de alunos vinculados
│   │   │       │   └── [alunoId]/      # Detalhe do aluno + fichas vinculadas
│   │   │       ├── treinos/
│   │   │       │   ├── page.tsx        # Lista fichas + criar (filtro por objetivo)
│   │   │       │   └── [treinoId]/     # Editor de ficha (exercícios, séries)
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
│   │   │       │       ├── page.tsx    # Detalhe da ficha com exercícios
│   │   │       │       └── executar/   # Execução passo a passo
│   │   │       ├── historico/          # Histórico de execuções com gráficos
│   │   │       ├── assinatura/         # Status da assinatura + pagamento pendente
│   │   │       └── pagamentos/         # Histórico de cobranças (Pix ou cartão)
│   │   │
│   │   ├── (public)/                   # Route group — sem auth
│   │   │   ├── layout.tsx              # PublicLayout
│   │   │   ├── login/
│   │   │   └── cadastro/
│   │   │       ├── treinador/
│   │   │       └── aluno/              # Seleciona treinador + pacote
│   │   │
│   │   ├── api/                        # Route Handlers (BFF — server-side only)
│   │   │   ├── auth/
│   │   │   │   ├── route.ts            # POST /api/auth — login; seta cookies httpOnly
│   │   │   │   ├── me/route.ts         # GET /api/auth/me — valida sessão server-side
│   │   │   │   ├── logout/route.ts     # POST /api/auth/logout — revoga JTI, limpa cookies
│   │   │   │   ├── register/
│   │   │   │   │   ├── treinador/route.ts
│   │   │   │   │   └── aluno/route.ts
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
│   │   ├── layout.tsx                  # Root layout — AuthProvider + SnackbarProvider
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
│   │   │   ├── AppLayout.tsx           # Layout autenticado + proteção client-side
│   │   │   ├── AppHeader.tsx           # Header com nav + avatar + logout
│   │   │   ├── PublicLayout.tsx        # Layout para páginas públicas
│   │   │   └── NavConfig.tsx           # Itens de nav por TipoConta
│   │   ├── pagamento/
│   │   │   ├── PagamentoPix.tsx        # QR Code + copia e cola + polling 30s de status
│   │   │   └── PagamentoCartao.tsx     # Stripe Elements (PaymentElement) + confirmPayment
│   │   ├── treinador/
│   │   │   └── ProgressaoAluno.tsx     # Gráfico de progressão do aluno
│   │   └── ui/
│   │       ├── AlertBanner.tsx         # Banner de erro/sucesso inline
│   │       ├── ConfirmDialog.tsx       # Dialog de confirmação genérico
│   │       ├── DataList.tsx            # Card com loading + empty + tabela
│   │       ├── EmptyState.tsx          # Estado vazio com ícone e mensagem
│   │       ├── InfoLine.tsx            # Label + valor em linha (detalhe)
│   │       ├── LoadingSpinner.tsx      # Spinner centralizado
│   │       ├── Logo.tsx                # Logo forzion.tech
│   │       ├── ResponsiveTable.tsx     # Tabela com paginação MUI
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
│   │   │   ├── admin.ts                # Treinadores, planos, grupos, exercícios globais
│   │   │   ├── treinador.ts            # Alunos, vínculos, fichas, exercícios, pacotes
│   │   │   ├── aluno.ts                # Fichas, execuções, vínculo
│   │   │   ├── pagamento.ts            # Onboarding Stripe, cobranças, pagamentos
│   │   │   └── conta.ts                # Perfil, senha
│   │   ├── auth/
│   │   │   ├── AuthContext.tsx          # Contexto de sessão React
│   │   │   └── session.ts              # extractTipoConta, homeRouteFor
│   │   ├── constants/
│   │   │   ├── labels.ts               # Maps enum → label PT-BR
│   │   │   └── enrollmentOptions.ts    # Opções de select do cadastro aluno
│   │   ├── theme/
│   │   │   └── index.ts                # Tema MUI (paleta + tipografia + overrides)
│   │   ├── utils/
│   │   │   ├── formatting.ts           # Funções de formatação
│   │   │   └── excel.ts               # Exportação de fichas para .xlsx (ExcelJS)
│   │   └── validations/
│   │       └── common.ts               # Schemas Zod reutilizáveis
│   │
│   ├── middleware.ts                   # Proteção de rotas server-side
│   └── types/
│       └── index.ts                    # Types e interfaces compartilhados
│                                       # TierPlano: "Free" | "Basic" | "Pro" | "ProPlus" | "Elite"
│                                       # PlanoTreinadorResponse: inclui tier e descricao (nullable)
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
| `/login` | público | Formulário de login |
| `/cadastro/treinador` | público | Cadastro de treinador (seleciona plano) |
| `/cadastro/aluno` | público | Cadastro de aluno (seleciona treinador + pacote) |
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
| `/treinador/treinos/[treinoId]` | Treinador | Editor de ficha — exercícios, séries, cargas, ordem |
| `/treinador/exercicios` | Treinador | Biblioteca pessoal + copiar exercícios globais |
| `/treinador/pacotes` | Treinador | CRUD pacotes (nome, descrição, preço) |
| `/treinador/pagamentos` | Treinador | Stripe Connect onboarding — configura conta de recebimentos |
| `/treinador/onboarding/retorno` | Treinador | Retorno pós-onboarding Stripe — verifica e exibe status |
| `/aluno` | Aluno | Dashboard |
| `/aluno/fichas` | Aluno | Lista fichas ativas vinculadas ao aluno |
| `/aluno/fichas/[fichaId]` | Aluno | Detalhe da ficha com exercícios e instruções |
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
4. `AuthContext.login()` atualiza o estado React com os dados do usuário
5. `router.push(homeRouteFor(tipoConta))` redireciona para a área correta

### Validação de Sessão no Mount

`AuthContext` dispara `GET /api/auth/me` no mount:

```
Browser → GET /api/auth/me (server-side Route Handler)
            └── lê cookie 'token' (httpOnly) + 'session_guard'
            └── decodifica JWT, valida expiração
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

Executa em cada request (exceto assets estáticos e `/api/`). Lê `token` + `session_guard` (httpOnly) e extrai `tipo_conta` do JWT payload **sem verificar assinatura** (performance — a assinatura é verificada pelo backend em cada chamada autenticada):

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
} = useCRUDDialog<PlanoTreinadorResponse>();
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
| `ResponsiveTable` | `columns, rows, total, page, pageSize, onPageChange, onPageSizeChange` | Tabela com paginação MUI integrada |
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
const dialog = useCRUDDialog<PlanoTreinadorResponse>();
const { reload } = usePaginatedList<PlanoTreinadorResponse>({ fetcher });

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

## Tema

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

### Outros mecanismos

| Mecanismo | Detalhe |
|-----------|---------|
| Token JWT | Nunca exposto ao browser — apenas em cookies httpOnly |
| `SameSite: strict` | Cookies não enviados em requests cross-site |
| Proxy server-side | Browser nunca vê a URL real do backend |
| Validação de formulários | Zod + React Hook Form em todos os forms públicos |
| Senhas de cadastro | Mínimo 8 chars, uppercase, lowercase, dígito |
| Mensagens de erro | Login/cadastro nunca exibe detalhes internos do backend |
| Stripe Publishable Key | `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` é pública por design — tokeniza cartões no browser sem expor dados ao servidor; a chave secreta fica exclusivamente no backend |

Build configurado como `output: "standalone"` para deploy em container.

---

## Testes

Stack: **Vitest 4 + happy-dom + @testing-library/react**

Configuração em `vitest.config.mts` + setup em `src/test/setup.ts`.

| Ferramenta | Uso |
|------------|-----|
| Vitest 4 | Runner + assertions |
| happy-dom | Simulação de DOM (sem conflito ESM — jsdom@27 é incompatível com Vitest 4) |
| @testing-library/react | Render de componentes + queries |
| @testing-library/user-event | Simulação de interações |
| @testing-library/jest-dom | Matchers extras (`toBeDisabled`, `toBeInTheDocument`, etc.) |

### Suíte atual

| Arquivo | O que testa | Testes |
|---------|------------|--------|
| `validations.test.ts` | Schemas Zod — email, senha, nome, telefone, loginSchema, cadastroTreinadorSchema, cadastroAlunoSchema (incl. campos de perfil obrigatórios) | 32 |
| `auth.test.ts` | `extractTipoConta` + `homeRouteFor` | 8 |
| `auth-context.test.tsx` | `AuthProvider` — fetch /api/auth/me, login, logout, useAuth fora do provider | 6 |
| `api-auth-me.test.ts` | Handler `GET /api/auth/me` — cookies, JWT válido, expirado, sem session_guard | 5 |
| `middleware.test.ts` | Proteção de rotas server-side — sem auth, papel errado, autenticado na área correta, rotas públicas | 19 |
| `useInactivity.test.ts` | Hook com `vi.useFakeTimers()` — warn, timeout, reset | 6 |
| `components.test.tsx` | `StatusChip`, `EmptyState`, `ConfirmDialog` | 11 |
| `responsive-table.test.tsx` | `ResponsiveTable` — desktop (headers, row click, actions), paginação, mobile (cards, Divider, propagação) | 13 |
| `formatting.test.ts` | `formatarSeries`, `formatarData`, `getWeekLabel`, `periodoParaDatas` | 24 |
| `excel.test.ts` | `sanitizeFilename` (path traversal, null byte, formula injection), `safeCell` (formula-trigger chars, passthrough seguro), `buildFichaRows` (estrutura, ordenação, imutabilidade, nulls, valores brutos), `exportarFichaParaExcel` async (mock ExcelJS via `vi.hoisted` + class, DOM spy, sanitized filename, safeCell aplicado, column widths) | 50 |
| `admin-api.test.ts` | Todos os métodos de visibilidade de `adminApi` + funções preexistentes. Mock de `@/lib/api/client`. Verifica URL, params e retorno. | 48 |
| `admin-pages.test.tsx` | Páginas admin — alunos (filtros, renderCell, tabs), detalhe aluno (tabs, vínculo, perfil), detalhe treinador (tabs, pacotes), detalhe treino. Mock de `next/navigation`, `adminApi`, `usePaginatedList`, `recharts`. | 33 |
| `pagamento.test.tsx` | `PagamentoPix` (spinner, estados Pago/Expirado/Falhou/Pendente, clipboard, polling), `PagamentosTreinadorPage` (onboarding completo/incompleto/erro, redirect Stripe), `OnboardingRetornoPage` (completo/incompleto/erro), `PagamentosAlunoPage` (estado inicial). | 19 |
| `pagamento-cartao.test.tsx` | `PagamentoCartao` — loading, sem clientSecret, status terminal (Falhou/Expirado), formulário (PaymentElement, status Pago), submit com erro Stripe, submit sem stripe/elements | 8 |

**Total: 282 testes**

### Armadilhas conhecidas

| Problema | Causa | Solução |
|---------|-------|---------|
| Conflito ESM no DOM | jsdom@27 + `@csstools/css-calc` incompatível com Vitest 4 | Usar `happy-dom` |
| `NextRequest.cookies` não parseia header em testes | API interna do Next.js não disponível em unit tests | Mock direto do objeto `{ cookies: { get: vi.fn() } }` |
| `extractTipoConta` não exportada | Era função local | Exportar explicitamente com `export function` |
| Base64 padding em JWT de teste | `btoa(payload).replace(/=/g, "")` → `atob` falha silenciosamente | Usar `btoa` sem strip dos `=` |
| `onTimeout` chamado múltiplas vezes | `setInterval` continua após `TIMEOUT_MS` | Usar `toHaveBeenCalled()`, não `toHaveBeenCalledOnce()` |
| Mock de módulo com constructor (`new ExcelJS.Workbook()`) | `vi.fn(() => instance)` com arrow function não é construtível | Usar `vi.hoisted` + `class WorkbookMock { addWorksheet = fn; xlsx = { writeBuffer: fn }; }` e referenciar em `vi.mock("exceljs", () => ({ default: { Workbook: excelMocks.WorkbookMock } }))` |
| MUI Select + `getByLabelText` | MUI Select não associa label ao controle via atributo `for` | Usar `getByRole("combobox")` em vez de `getByLabelText("Status")` |
| Namespace `Email` colide com VO | Pasta de teste `...Notifications.Email` → `Email` resolve para o namespace, não o tipo | Alias: `using EmailVO = forzion.tech.Domain.ValueObjects.Email;` (backend) |
| `@stripe/stripe-js` em testes | `loadStripe` dispara fetch para o SDK Stripe, falha em happy-dom | Mockar `@stripe/stripe-js` e `@stripe/react-stripe-js` com `vi.mock` antes dos imports |
