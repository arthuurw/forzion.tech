# specification-seo — SEO & metadata (forzion.tech)

DOC PARA AGENTES. Fonte de verdade de SEO/metadata do frontend (metadata por rota, OpenGraph, crawl control, structured data). PARCIALMENTE ASPIRACIONAL — surface atual mínima; maioria é RECOMENDAÇÃO/GAP. Formato denso, agent-oriented. Cross-ref: [specification-frontend] (rotas/grupos de layout/App Router), [specification-frontend-ui] (landing/componentes), [specification-observability] (lighthouse perf budgets/cadência), [specification-tests] (gate lighthouse no CI).

## MANUTENÇÃO DESTE ARQUIVO
- Manter atualizado NA MESMA TAREFA ao: adicionar/alterar `metadata`/`generateMetadata` em rotas, criar `sitemap.ts`/`robots.ts`, adicionar OpenGraph/JSON-LD, mudar `lang`/`metadataBase`, ou alterar threshold SEO no lighthouse.
- Rótulos OBRIGATÓRIOS: cada afirmação marcada `[ATUAL]` (existe no código hoje) ou `[REC]`/`[GAP]` (recomendação/ausência confirmada).
- Vive em `specs/` (versionado; commitar). NÃO duplicar rotas/headers — referenciar [specification-frontend].
- Ao implementar um `[REC]`/`[GAP]`, reclassificar para `[ATUAL]` e citar o path.

## 1. ESTADO ATUAL

### Metadata root — `[ATUAL]`
`frontend/src/app/layout.tsx` (linhas 18-27):
| Campo | Valor atual |
|-------|-------------|
| `metadata.title` | `"forzion.tech"` (string fixa; SEM `template`) |
| `metadata.description` | `"Plataforma de gestão de treinos para personal trainers"` |
| `metadata.metadataBase` | AUSENTE `[GAP]` (URLs OG/canonical resolvem relativas → quebram fora de same-origin) |
| `viewport` | `width=device-width, initialScale=1, viewportFit=cover` (export `Viewport` separado) |
| `<html lang>` | `"pt-BR"` (linha 35) `[ATUAL]` |

`metadata` é o ÚNICO export de metadata em TODO `src/app` (grep confirmado: 1 match, root layout). Nenhuma rota usa `generateMetadata` nem `export const metadata` própria.

### Lighthouse SEO — `[ATUAL]`
`frontend/lighthouserc.json`:
- `categories:seo`: `["warn", { "minScore": 0.8 }]` → **NÃO bloqueia CI** (warn, não error). GAP de enforcement (ver §6).
- Outras categorias SÃO `error` (perf 0.85, a11y 0.95, best-practices 0.9).
- URLs auditadas: `/`, `/login`, `/cadastro/aluno`, `/cadastro/treinador`.
- Cross-ref: [specification-tests] (gates lighthouse), [specification-infrastructure].

### O que NÃO existe (grep/glob confirmados) — `[GAP]`
| Artefato | Status | Verificação |
|----------|--------|-------------|
| `frontend/src/app/sitemap.ts` | AUSENTE | glob `**/sitemap.{ts,tsx}` → 0 |
| `frontend/src/app/robots.ts` | AUSENTE | glob `**/robots.{ts,tsx}` → 0 |
| `frontend/public/robots.txt` | AUSENTE | glob → 0 (public tem só `*.svg`, `.well-known/security.txt`, `mockServiceWorker.js`) |
| `frontend/public/sitemap.xml` | AUSENTE | glob → 0 |
| `generateMetadata` em qualquer rota | AUSENTE | grep `generateMetadata` em `src` → 0 matches |
| `openGraph` / `twitter` | AUSENTE | grep → 0 |
| `robots` (campo metadata) | AUSENTE | grep → 0 |
| `alternates`/`canonical` | AUSENTE | grep → 0 |
| `application/ld+json` (structured data) | AUSENTE | grep → 0 |
| OG image asset | AUSENTE | sem `og*.png`/`opengraph-image.*` em `public/` ou `app/` |
| `favicon`/`icon`/`apple-icon` | NÃO CONFIRMADO via convenção App Router (sem `app/icon.*`/`app/favicon.ico` no glob) `[GAP]` |

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

## 2. METADATA POR ROTA — `[REC]`

Padrão App Router: cada `page.tsx`/`layout.tsx` exporta `metadata` estático ou `generateMetadata` (dinâmico).

### 2.1 Title template no root — `[REC]`
`src/app/layout.tsx`, substituir `title` string por:
```ts
title: {
  default: "forzion.tech — Gestão para Personal Trainers",
  template: "%s | forzion.tech",
},
metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL ?? "https://forzion.tech"),
```
- `metadataBase` é PRÉ-REQUISITO para canonical/OG absolutos. `NEXT_PUBLIC_SITE_URL` NÃO existe hoje (grep env → 0) → adicionar em `frontend/.env*` e documentar em [specification-frontend] §env.

### 2.2 `metadata` por página pública — `[REC]`
| Rota | title (vira `… | forzion.tech`) | canonical | robots |
|------|------|-----------|--------|
| `/` | (usa `default`) | `/` | index, follow |
| `/login` | `Entrar` | `/login` | index, follow |
| `/cadastro/treinador` | `Criar conta — Treinador` | `/cadastro/treinador` | index, follow |
| `/cadastro/aluno` | `Criar conta — Aluno` | `/cadastro/aluno` | index, follow |
| `/forgot-password` etc. | `Recuperar acesso` | (próprio) | `noindex, follow` |

Canonical via `alternates: { canonical: "/rota" }` (resolve sobre `metadataBase`).
NOTA: páginas públicas são Client Components (forms RHF). `export const metadata` exige Server Component — usar `metadata` no `layout.tsx` da rota OU split server-wrapper. Verificar `"use client"` antes de adicionar export (quebra build se na mesma página client).

## 3. OPENGRAPH / SOCIAL CARDS — `[REC]`

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

## 4. CRAWL CONTROL — `[REC]`

### 4.1 `robots.ts` — `[REC]`
Criar `src/app/robots.ts` (Next gera `/robots.txt`):
```ts
export default function robots(): MetadataRoute.Robots {
  return {
    rules: { userAgent: "*", allow: "/", disallow: ["/admin", "/treinador", "/aluno", "/perfil", "/api/"] },
    sitemap: `${process.env.NEXT_PUBLIC_SITE_URL ?? "https://forzion.tech"}/sitemap.xml`,
  };
}
```
- Disallow cobre os grupos autenticados `(admin)`/`(treinador)`/`(aluno)` + `/perfil` + Route Handlers `/api/`.

### 4.2 index vs noindex por grupo — `[REC]`
| Grupo de rota | Política | Como |
|---------------|----------|------|
| `(public)` cadastro/login | `index, follow` | default (sem robots meta) |
| `(public)` forgot/reset/verify/resend | `noindex, follow` | `metadata.robots` no layout/página |
| `(admin)`, `(treinador)`, `(aluno)`, `/perfil` | `noindex, nofollow` | `metadata.robots` no layout do grupo + disallow robots.txt |

NOTA defesa-em-profundidade: áreas autenticadas já protegidas por middleware (redirect `/login` sem token — [specification-frontend] §middleware). robots.txt/noindex é camada adicional (crawlers não logam, mas evita vazamento de URLs em SERP).

### 4.3 `sitemap.ts` — `[REC]`
Criar `src/app/sitemap.ts` listando SÓ rotas públicas indexáveis:
```ts
export default function sitemap(): MetadataRoute.Sitemap {
  const base = process.env.NEXT_PUBLIC_SITE_URL ?? "https://forzion.tech";
  return ["/", "/login", "/cadastro/treinador", "/cadastro/aluno"]
    .map((p) => ({ url: `${base}${p}`, changeFrequency: "weekly", priority: p === "/" ? 1 : 0.7 }));
}
```
- Atualizar a lista QUANDO surgir rota pública nova (ex.: futura `/treinadores/[id]` → gerar dinamicamente).

## 5. STRUCTURED DATA (schema.org JSON-LD) — `[REC]`

### 5.1 Organization (landing) — `[REC]`
Injetar em `src/app/page.tsx` (Server Component, OK):
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
1. `metadataBase` + `NEXT_PUBLIC_SITE_URL` (pré-req de tudo) — `[GAP]`
2. OpenGraph + OG image (CTR em WhatsApp/LinkedIn) — `[GAP]`
3. `robots.ts` + `sitemap.ts` (indexação correta; noindex áreas auth) — `[GAP]`
4. title template + descriptions/canonical por página — `[GAP]`
5. JSON-LD Organization na landing — `[GAP]`
6. Promover gate SEO `warn`→`error` — `[GAP]`
7. (Futuro produto) rota pública de treinador + metadata/JSON-LD dinâmicos — `[REC FUTURO]`
