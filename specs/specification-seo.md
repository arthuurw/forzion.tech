# specification-seo — SEO & metadata (forzion.tech)

DOC AGENTES (denso). Fonte de verdade de SEO/metadata frontend (metadata por rota, OpenGraph, crawl control, structured data). Base impl (T5): metadataBase/title-template/OG dinâmica/robots/sitemap/JSON-LD/canonical/noindex. Aberto: perfil público de treinador + gate SEO `warn`→`error`. Atualizar NA MESMA TAREFA ao mudar `metadata`/`generateMetadata`/`sitemap.ts`/`robots.ts`/OG/JSON-LD/`lang`/`metadataBase`/threshold SEO lighthouse. Rótulos OBRIGATÓRIOS por afirmação: `[ATUAL]` (existe hoje) / `[REC]`/`[GAP]` (recomendação/ausência); ao implementar `[REC]`/`[GAP]` reclassificar p/ `[ATUAL]` + path. Vive em `specs/` (commitar); NÃO duplicar rotas/headers — referenciar [specification-frontend]. Cross-ref: [specification-frontend-ui] (landing/componentes), [specification-observability] (perf budgets), [specification-tests] (gate lighthouse CI).

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

OG image por CONVENÇÃO de arquivo `frontend/src/app/opengraph-image.tsx` (`next/og` `ImageResponse`, 1200×630, `runtime="nodejs"`) — não listada em `openGraph.images`. ÚNICA referência canônica desta convenção (demais seções só apontam aqui). `[ATUAL]` (T5)
Env `NEXT_PUBLIC_SITE_URL` (default `https://forzion.tech`, documentada em `frontend/.env.example`) é a base de `metadataBase`/`sitemap`/`robots`/JSON-LD — fonte única deste default. `[ATUAL]` (T5)
Metadata por rota via `layout.tsx` server por rota (§2.2). Nenhuma rota usa `generateMetadata` ainda (só `export const metadata` estático).

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
- `metadataBase` é PRÉ-REQUISITO para canonical/OG absolutos (`NEXT_PUBLIC_SITE_URL` — ver §1).

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

`openGraph`/`twitter` no root `layout.tsx`; OG image em `app/opengraph-image.tsx` (ver §1: fundo `#1A1A1A`, nome `#F5C400`, tagline "Gestão para Personal Trainers", fontes default sem fetch, `runtime="nodejs"` p/ `output:"standalone"`). Relevância B2C: previews em WhatsApp/LinkedIn/Instagram (canal de aquisição).

Root `layout.tsx` `metadata.openGraph` (const `SITE_DESCRIPTION`; herda p/ todas as rotas, override por página):
```ts
openGraph: {
  type: "website",
  siteName: "forzion.tech",
  locale: "pt_BR",
  url: "/",
  title: "forzion.tech — Gestão para Personal Trainers",
  description: SITE_DESCRIPTION, // "Plataforma de gestão de treinos para personal trainers"
},
twitter: { card: "summary_large_image" },
```
- OG image: ver §1 (file convention `app/opengraph-image.tsx`, sem `openGraph.images`).
- `[REC]` alinhar `og:description` ao hero da landing (`LandingPage` hero `Typography h6`: "Da prescrição ao acompanhamento — com controle, histórico e estrutura centralizados.") — hoje reusa `SITE_DESCRIPTION`.
- CSP: `next/og` runtime nodejs — sem impacto no CSP do cliente; OG servida same-origin (`img-src 'self'`). Ver [specification-frontend] §headers.

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
- `(public)` index/noindex por rota: ver tabela §2.2 (cadastro/login = index; forgot/reset/verify/resend = noindex).
- `(admin)`/`(treinador)`/`(aluno)`/`/perfil` = `noindex, nofollow` via `export const metadata = { robots:{ index:false, follow:false } }` no layout do grupo (server) + disallow robots.txt.

NOTA defesa-em-profundidade: áreas autenticadas já protegidas por middleware (redirect `/login` sem token — [specification-frontend] §middleware); robots/noindex é camada adicional contra vazamento de URLs em SERP.

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
Injetado na landing (`LandingPage`, Server Component) via `<script type="application/ld+json">` com `dangerouslySetInnerHTML` (const `organizationJsonLd`):
```tsx
{ "@context": "https://schema.org", "@type": "Organization",
  name: "forzion.tech", url: SITE_URL, // NEXT_PUBLIC_SITE_URL ?? "https://forzion.tech"
  description: "Plataforma de gestão de treinos para personal trainers" }
```
- CSP: `script-src 'self' 'unsafe-inline'` ([specification-frontend] §headers) já permite JSON-LD inline. SEM mudança de CSP necessária.
- Alternativa SoftwareApplication/Service (SaaS) — avaliar; Organization é o mínimo seguro.

### 5.2 Person/Service por treinador — `[GAP]/[REC FUTURO]`
INAPLICÁVEL hoje: não há página pública de treinador (ver §1). SE for criada rota `/treinadores/[id]` (perfil público compartilhável), aí sim:
- `generateMetadata({ params })` → title/OG dinâmicos (nome do treinador + OG image personalizada).
- JSON-LD `Person` (treinador) ou `Service` (pacotes oferecidos).
- Atualizar `sitemap.ts` para gerar entradas dinâmicas (fetch treinadores aprovados).
- Oportunidade de produto (link de treinador compartilhável), não dívida técnica.

## 6. ENFORCEMENT — `[ATUAL]` + `[GAP]`

- `[ATUAL]` Lighthouse roda no CI sobre 4 URLs (incl. landing/cadastros). Config `frontend/lighthouserc.json`. Detalhes de gate/CI em [specification-tests] (NÃO duplicar aqui).
- `[GAP]` SEO é `warn` (`minScore 0.8`) → regressão de SEO **não falha o build**. Todas as outras categorias são `error`. Sem sitemap/robots/canonical, o score SEO do Lighthouse fica capado mesmo com warn passando.
- `[REC]` Após implementar §2-4 (canonical, robots, metadata descriptions), promover `categories:seo` de `warn` → `error` com `minScore` ≥ `0.9` para travar regressões. Atualizar [specification-tests] no mesmo PR.
- `[REC]` Adicionar auditoria de structured data fora do Lighthouse (ex.: validação manual via Rich Results Test) — não há audit automatizado de JSON-LD no CI hoje `[GAP]`.

## ITENS ABERTOS (base T5 toda `[ATUAL]` — ver §1 Artefatos)
- Promover gate SEO `warn`→`error` (`minScore ≥0.9`) — `[GAP]` (§6).
- (Futuro produto) rota pública de treinador `/treinadores/[id]` + `generateMetadata`/JSON-LD dinâmicos — `[REC FUTURO]` (§5.2).
