# specification-seo — SEO & metadata (forzion.tech)

DOC PARA AGENTES. Fonte de verdade de SEO/metadata do frontend (metadata por rota, OpenGraph, crawl control, structured data). Base implementada (T5): metadataBase/title-template/OG dinâmica/robots/sitemap/JSON-LD/canonical/noindex. Aspiracional restante: perfil público de treinador + promoção do gate SEO `warn`→`error`. Formato denso, agent-oriented. Cross-ref: [specification-frontend] (rotas/grupos de layout/App Router), [specification-frontend-ui] (landing/componentes), [specification-observability] (lighthouse perf budgets/cadência), [specification-tests] (gate lighthouse no CI).

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA ao: adicionar/alterar `metadata`/`generateMetadata` em rotas, criar `sitemap.ts`/`robots.ts`, adicionar OpenGraph/JSON-LD, mudar `lang`/`metadataBase`, ou alterar threshold SEO no lighthouse.
- Rótulos OBRIGATÓRIOS: cada afirmação marcada `[ATUAL]` (existe no código hoje) ou `[REC]`/`[GAP]` (recomendação/ausência confirmada).
- Vive em `specs/` (versionado; commitar). NÃO duplicar rotas/headers — referenciar [specification-frontend].
- Ao implementar um `[REC]`/`[GAP]`, reclassificar para `[ATUAL]` e citar o path.

## 1. ESTADO ATUAL

### Metadata root — `[ATUAL]`
`frontend/src/app/layout.tsx`:
| Campo | Valor atual |
|-------|-------------|
| `metadata.title` | `{ default: "forzion.tech — Gestão para Personal Trainers", template: "%s | forzion.tech" }` `[ATUAL]` (T5) |
| `metadata.description` | `"Plataforma de gestão de treinos para personal trainers"` |
| `metadata.metadataBase` | `new URL(process.env.NEXT_PUBLIC_SITE_URL ?? "https://forzion.tech")` `[ATUAL]` (T5) |
| `metadata.openGraph` | `type=website, siteName=forzion.tech, locale=pt_BR, url="/", title, description` `[ATUAL]` (T5) |
| `metadata.twitter` | `{ card: "summary_large_image" }` `[ATUAL]` (T5) |
| `viewport` | `width=device-width, initialScale=1, viewportFit=cover` (export `Viewport` separado) |
| `<html lang>` | `"pt-BR"` `[ATUAL]` |

OG image resolvida por CONVENÇÃO de arquivo `frontend/src/app/opengraph-image.tsx` (`next/og` `ImageResponse`, 1200×630, `runtime="nodejs"`) — não listada em `openGraph.images`. `[ATUAL]` (T5)
Env: `NEXT_PUBLIC_SITE_URL` documentada em `frontend/.env.example` (default `https://forzion.tech`). `[ATUAL]` (T5)
Metadata por rota agora existe via `layout.tsx` server por rota (ver §2.2). Nenhuma rota usa `generateMetadata` ainda (só `export const metadata` estático).

### Lighthouse SEO — `[ATUAL]`
`frontend/lighthouserc.json`:
- `categories:seo`: `["warn", { "minScore": 0.8 }]` → **NÃO bloqueia CI** (warn, não error). GAP de enforcement (ver §6).
- Outras categorias SÃO `error` (perf 0.85, a11y 0.95, best-practices 0.9).
- URLs auditadas: `/`, `/login`, `/cadastro/aluno`, `/cadastro/treinador`.
- Cross-ref: [specification-tests] (gates lighthouse), [specification-infrastructure].

### Artefatos SEO — estado pós-T5
| Artefato | Status | Path / nota |
|----------|--------|-------------|
| `sitemap.ts` | `[ATUAL]` (T5) | `frontend/src/app/sitemap.ts` → gera `/sitemap.xml` (build confirmou rota) |
| `robots.ts` | `[ATUAL]` (T5+A1) | `frontend/src/app/robots.ts` → `/robots.txt`. ENV-GATED `NEXT_PUBLIC_INDEXABLE` (default noindex total; allow só em prod). §4.1 |
| `generateMetadata` em rota | AUSENTE `[GAP]` | só `export const metadata` estático (suficiente p/ superfície atual; dinâmico fica p/ perfil de treinador §5.2) |
| `openGraph` / `twitter` | `[ATUAL]` (T5) | root `layout.tsx` |
| `robots` (campo metadata) | `[ATUAL]` (T5) | noindex em grupos auth + transacionais (§4.2) |
| `alternates`/`canonical` | `[ATUAL]` (T5) | `/`, `/login`, `/cadastro/treinador`, `/cadastro/aluno` |
| `application/ld+json` (structured data) | `[ATUAL]` (T5) | Organization na landing `page.tsx` (§5.1) |
| OG image | `[ATUAL]` (T5) | `app/opengraph-image.tsx` (dinâmica next/og) → rota `/opengraph-image` no build |
| `favicon`/`icon`/`apple-icon` | NÃO CONFIRMADO via convenção App Router `[GAP]` | sem `app/icon.*`/`app/favicon.ico` |

### Superfície pública indexável — `[ATUAL]`
Rotas públicas (grupo `(public)` + landing) — únicas candidatas a `index` (ver [specification-frontend] §grupos de rota):
| Rota | Arquivo | Natureza SEO |
|------|---------|--------------|
| `/` (landing) | `src/app/page.tsx` | Página de marketing B2B; SSR (`async`, fetch `/auth/planos`). Alvo PRINCIPAL de SEO. |
| `/login` | `(public)/login/page.tsx` | Funcional; baixo valor SEO (indexável, sem destaque) |
| `/cadastro/treinador` | `(public)/cadastro/treinador/page.tsx` | Conversão treinador; alvo SEO secundário |
| `/cadastro/aluno` | `(public)/cadastro/aluno/page.tsx` | Conversão aluno |
| `/forgot-password`, `/reset-password`, `/verify-email`, `/resend-verification` | `(public)/*` | Fluxos transacionais → `noindex` recomendado |

NOTA: NÃO existe rota de perfil público de treinador (`/treinadores/[id]` ou similar). Listagem de treinadores é só via API `/api/auth/treinadores` (consumida no cadastro do aluno). Logo, structured data Person/Service por treinador é hoje INAPLICÁVEL (ver §5).

## 2. METADATA POR ROTA — `[ATUAL]` (T5)

Padrão App Router: cada `page.tsx`/`layout.tsx` exporta `metadata` estático ou `generateMetadata` (dinâmico).

### 2.1 Title template no root — `[ATUAL]` (T5)
`src/app/layout.tsx` implementa:
```ts
title: {
  default: "forzion.tech — Gestão para Personal Trainers",
  template: "%s | forzion.tech",
},
metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL ?? "https://forzion.tech"),
```
- `metadataBase` é PRÉ-REQUISITO para canonical/OG absolutos. `NEXT_PUBLIC_SITE_URL` documentada em `frontend/.env.example` (default `https://forzion.tech`). `[ATUAL]` (T5)

### 2.2 `metadata` por página pública — `[ATUAL]` (T5)
| Rota | title (vira `… | forzion.tech`) | canonical | robots | Onde |
|------|------|-----------|--------|------|
| `/` | (usa `default`) | `/` | index, follow | `app/page.tsx` (server, `export const metadata`) |
| `/login` | `Entrar` | `/login` | index, follow | `(public)/login/layout.tsx` |
| `/cadastro/treinador` | `Criar conta — Treinador` | `/cadastro/treinador` | index, follow | `(public)/cadastro/treinador/layout.tsx` |
| `/cadastro/aluno` | `Criar conta — Aluno` | `/cadastro/aluno` | index, follow | `(public)/cadastro/aluno/layout.tsx` |
| `/forgot-password` | `Recuperar acesso` | (sem canonical) | `noindex, follow` | `(public)/forgot-password/layout.tsx` |
| `/reset-password` | `Redefinir senha` | (sem canonical) | `noindex, follow` | `(public)/reset-password/layout.tsx` |
| `/verify-email` | `Verificar e-mail` | (sem canonical) | `noindex, follow` | `(public)/verify-email/layout.tsx` |
| `/resend-verification` | `Reenviar verificação` | (sem canonical) | `noindex, follow` | `(public)/resend-verification/layout.tsx` |

Canonical via `alternates: { canonical: "/rota" }` (resolve sobre `metadataBase`).
NOTA: as 7 páginas `(public)/*` são Client Components (forms RHF) → `export const metadata` na page quebraria o build. SOLUÇÃO ADOTADA: criado `layout.tsx` server por rota (passa-through `children`) carregando title/canonical/robots. O `(public)/layout.tsx` do grupo permanece como wrapper de UI sem metadata.

## 3. OPENGRAPH / SOCIAL CARDS — `[ATUAL]` (T5)

IMPLEMENTADO: `openGraph`/`twitter` no root `layout.tsx`; OG image dinâmica em `app/opengraph-image.tsx` (next/og `ImageResponse`, fundo `#1A1A1A`, nome em `#F5C400`, tagline "Gestão para Personal Trainers", 1200×630, fontes default sem fetch de binário, `runtime="nodejs"` p/ compat com `output:"standalone"`). Build gera rota `/opengraph-image`. Referência histórica de design abaixo.


Relevância B2C: links compartilhados em WhatsApp/LinkedIn/Instagram (canal primário de aquisição de personal trainers). Sem OG → preview sem imagem/título = baixa CTR.

Root `layout.tsx` `metadata.openGraph` (herda p/ todas as rotas, override por página):
```ts
openGraph: {
  type: "website",
  siteName: "forzion.tech",
  locale: "pt_BR",
  url: "/",
  title: "forzion.tech — Gestão para Personal Trainers",
  description: "Da prescrição ao acompanhamento — controle, histórico e estrutura centralizados.",
  images: [{ url: "/opengraph-image.png", width: 1200, height: 630 }],
},
twitter: { card: "summary_large_image" },
```
- Imagem: `1200x630`. Opções App Router: arquivo estático `app/opengraph-image.png` OU geração dinâmica `app/opengraph-image.tsx` (`next/og` `ImageResponse`). `[GAP]` nenhum existe.
- `og:description` deve refletir o hero da landing (`page.tsx` linha 116: "Da prescrição ao acompanhamento — com controle, histórico e estrutura centralizados.").
- CSP impacto: `next/og` runtime edge — sem impacto no CSP de runtime do cliente. OG image é servida same-origin (`img-src 'self'` já cobre). Ver [specification-frontend] §headers.

## 4. CRAWL CONTROL — `[ATUAL]` (T5)

### 4.1 `robots.ts` — `[ATUAL]` (T5 + review-fix A1)
`src/app/robots.ts` (Next gera `/robots.txt`). **ENV-GATED** (`NEXT_PUBLIC_INDEXABLE`): indexável SÓ quando `=== "true"` (produção); qualquer outro valor/ausente ⇒ `disallow: "/"` (noindex TOTAL). Default seguro = noindex — impede indexar homolog/staging (host público). Quando indexável:
```ts
const indexable = process.env.NEXT_PUBLIC_INDEXABLE === "true";
// !indexable → { rules: { userAgent: "*", disallow: "/" } }
// indexable  → allow "/" + disallow ["/admin","/treinador","/aluno","/perfil","/api/"] + sitemap
```
- Disallow (quando indexável) cobre os grupos autenticados `(admin)`/`(treinador)`/`(aluno)` + `/perfil` + Route Handlers `/api/`.
- **Defesa em profundidade (A1)**: além do env-gate, o nginx do host de homolog (`homologacao.forzion.tech`) injeta `X-Robots-Tag: noindex, nofollow` (ver [specification-security] §3 / [specification-infrastructure] nginx). `NEXT_PUBLIC_INDEXABLE=true` só no ambiente de produção.

### 4.2 index vs noindex por grupo — `[ATUAL]` (T5)
| Grupo de rota | Política | Como |
|---------------|----------|------|
| `(public)` cadastro/login | `index, follow` | `metadata.robots` no `layout.tsx` por rota (§2.2) |
| `(public)` forgot/reset/verify/resend | `noindex, follow` | `metadata.robots` no `layout.tsx` por rota (§2.2) |
| `(admin)`, `(treinador)`, `(aluno)`, `/perfil` | `noindex, nofollow` | `export const metadata = { robots: { index:false, follow:false } }` no layout do grupo (todos server) + disallow robots.txt |

NOTA defesa-em-profundidade: áreas autenticadas já protegidas por middleware (redirect `/login` sem token — [specification-frontend] §middleware). robots.txt/noindex é camada adicional (crawlers não logam, mas evita vazamento de URLs em SERP).

### 4.3 `sitemap.ts` — `[ATUAL]` (T5)
`src/app/sitemap.ts` lista SÓ rotas públicas indexáveis:
```ts
export default function sitemap(): MetadataRoute.Sitemap {
  const base = process.env.NEXT_PUBLIC_SITE_URL ?? "https://forzion.tech";
  return ["/", "/login", "/cadastro/treinador", "/cadastro/aluno"]
    .map((p) => ({ url: `${base}${p}`, changeFrequency: "weekly", priority: p === "/" ? 1 : 0.7 }));
}
```
- Atualizar a lista QUANDO surgir rota pública nova (ex.: futura `/treinadores/[id]` → gerar dinamicamente).

## 5. STRUCTURED DATA (schema.org JSON-LD) — `[REC]`

### 5.1 Organization (landing) — `[ATUAL]` (T5)
Injetado em `src/app/page.tsx` (Server Component) via `<script type="application/ld+json">` com `dangerouslySetInnerHTML`:
```tsx
<script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify({
  "@context": "https://schema.org", "@type": "Organization",
  name: "forzion.tech", url: "https://forzion.tech",
  description: "Plataforma de gestão de treinos para personal trainers",
}) }} />
```
- CSP: `script-src 'self' 'unsafe-inline'` ([specification-frontend] §headers) já permite JSON-LD inline. SEM mudança de CSP necessária.
- Alternativa SoftwareApplication/Service (SaaS) — avaliar; Organization é o mínimo seguro.

### 5.2 Person/Service por treinador — `[GAP]/[REC FUTURO]`
INAPLICÁVEL hoje: não há página pública de treinador (ver §1). SE for criada rota `/treinadores/[id]` (perfil público compartilhável), aí sim:
- `generateMetadata({ params })` → title/OG dinâmicos (nome do treinador + OG image personalizada).
- JSON-LD `Person` (treinador) ou `Service` (pacotes oferecidos).
- Atualizar `sitemap.ts` para gerar entradas dinâmicas (fetch lista de treinadores aprovados).
- Forte alavanca B2C (link de treinador compartilhável em redes). Marcar como oportunidade de produto, não dívida.

## 6. ENFORCEMENT — `[ATUAL]` + `[GAP]`

- `[ATUAL]` Lighthouse roda no CI sobre 4 URLs (incl. landing/cadastros). Config `frontend/lighthouserc.json`. Detalhes de gate/CI em [specification-tests] (NÃO duplicar aqui).
- `[GAP]` SEO é `warn` (`minScore 0.8`) → regressão de SEO **não falha o build**. Todas as outras categorias são `error`. Sem sitemap/robots/canonical, o score SEO do Lighthouse fica capado mesmo com warn passando.
- `[REC]` Após implementar §2-4 (canonical, robots, metadata descriptions), promover `categories:seo` de `warn` → `error` com `minScore` ≥ `0.9` para travar regressões. Atualizar [specification-tests] no mesmo PR.
- `[REC]` Adicionar auditoria de structured data fora do Lighthouse (ex.: validação manual via Rich Results Test) — não há audit automatizado de JSON-LD no CI hoje `[GAP]`.

## RESUMO DE PRIORIZAÇÃO (impacto SEO B2C)
1. `metadataBase` + `NEXT_PUBLIC_SITE_URL` (pré-req de tudo) — `[ATUAL]` (T5)
2. OpenGraph + OG image dinâmica (CTR em WhatsApp/LinkedIn) — `[ATUAL]` (T5)
3. `robots.ts` + `sitemap.ts` (indexação correta; noindex áreas auth) — `[ATUAL]` (T5)
4. title template + canonical/noindex por página — `[ATUAL]` (T5)
5. JSON-LD Organization na landing — `[ATUAL]` (T5)
6. Promover gate SEO `warn`→`error` — `[GAP]` (follow-up; ver §6)
7. (Futuro produto) rota pública de treinador + metadata/JSON-LD dinâmicos — `[REC FUTURO]`
