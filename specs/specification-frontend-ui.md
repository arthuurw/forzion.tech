# specification-frontend-ui — design system & acessibilidade (forzion.tech)
DOC PARA AGENTES. Fonte de verdade de design tokens, inventário de componentes UI/forms reutilizáveis, governance de componentes e conformance de acessibilidade. Formato denso, agent-oriented. Cross-ref: [specification-frontend] (App Router/forms/validação/TEMA MUI básico/RESPONSIVIDADE — NÃO duplicar), [specification-tests] (harness a11y), [specification-lgpd] (ConsentBanner), [specification-observability] (lighthouse a11y).

## MANUTENÇÃO DESTE ARQUIVO
Atualizar QUANDO a tarefa tocar: tema/design tokens (`src/lib/theme/index.ts`), componentes em `src/components/ui/*` ou `src/components/forms/*` (criar/alterar/remover), padrões de a11y (aria, foco, keyboard nav, reduced-motion), conformance WCAG/harness a11y, responsividade (breakpoints/BottomNav/input fontSize), ou divergência F18 (color-contrast).
Regras: vive em `specs/` versionado/commitado (NUNCA `.specs/`). NÃO duplicar o TEMA MUI básico de [specification-frontend] (§TEMA MUI) nem §RESPONSIVIDADE — aqui se EXPANDE (tokens exatos, taxonomy, governance, conformance) e se REFERENCIA. Notação compacta; toda afirmação ancorada em path real. Ao criar/alterar componente, este doc + story + a11y test são atualizados na MESMA tarefa (ver §GOVERNANCE).

## DESIGN TOKENS (`src/lib/theme/index.ts`)
Single source: `createTheme(..., ptBR)`. Locale `@mui/material/locale > ptBR` aplicado (paginação, datas, validações MUI em pt-BR).

### Paleta (hex exatos)
| Token | Valor | Uso |
|---|---|---|
| `primary.main` | `#F5C400` | amarelo marca (CTA, links, spinner) |
| `primary.light` | `#FFD740` | |
| `primary.dark` | `#C9A000` | |
| `primary.contrastText` | `#1A1A1A` | texto sobre amarelo (preto, não branco) |
| `primary` hover (containedPrimary) | `#E6B800` | override em `MuiButton` |
| `secondary.main` | `#1A1A1A` | quase-preto |
| `secondary.light` | `#2E2E2E` | |
| `secondary.contrastText` | `#FFFFFF` | |
| `error.main` | `#C62828` | 5.6:1 em #FFF / 5.3:1 em #F7F8FA (AA); branco sobre error em botão = 5.6:1 (AA). (sem light/dark/warning/info/success custom → MUI defaults) |
| `background.default` | `#F7F8FA` | cinza claro app |
| `background.paper` | `#FFFFFF` | |
| `text.primary` | `#111827` | 17.7:1 em #FFF / 16.7:1 em #F7F8FA (AA) |
| `text.secondary` | `#4B5563` | gray-600; 7.6:1 em #FFF / 7.1:1 em #F7F8FA (AA) — F18 resolvido (era #6B7280 ≈4.6:1 em #F7F8FA) |
| `divider` | `rgba(0,0,0,0.08)` | (MuiDivider override → `rgba(0,0,0,0.07)`) |

NOTA: `warning`/`info`/`success` NÃO redefinidos → AlertBanner/StatusChip/Snackbar herdam paleta MUI default desses severities.

### Tipografia
- `fontFamily`: `'Inter', 'Roboto', sans-serif`. Inter via Google Fonts (pesos 400/500/600/700, `display: swap` — ver [specification-frontend]).
- Pesos/letter-spacing aplicados: `h1` 700/-0.02em · `h2` 700/-0.015em · `h3` 700/-0.01em · `h4` 700/-0.01em · `h5` 600/-0.005em · `h6` 600 · `subtitle1` 500 · `button` 600/`letterSpacing:0`/`textTransform:none`.
- `MuiButton.sizeLarge`: `fontSize 1rem`. `Logo` sizes: sm `1rem` / md `1.4rem` / lg `2rem` (fora da escala typography — tokens locais).

### Forma / spacing / elevação
- `shape.borderRadius`: **12** (global). Overrides: Button/OutlinedInput/ListItemButton = **10**; Card/Paper(rounded) = **16**; Chip = **8**.
- Spacing: MUI default (base **8px**); usado via `sx` (`p`, `py`, `gap` em múltiplos/frações).
- `shadows`: custom array (25 níveis). Distintos só nos níveis 1–6; **níveis 6–24 todos iguais** = `0 12px 32px rgba(0,0,0,0.12)`. Card usa shadow custom inline (`0 1px 3px… , 0 1px 8px…`), não o array.
- Breakpoints: MUI default (`xs 0 · sm 600 · md 900 · lg 1200 · xl 1536`). Não customizados.

### Component defaults globais (theme.components)
| Componente | defaultProps / override chave |
|---|---|
| `MuiButton` | `disableElevation`; radius 10; pad 10; hover primary `#E6B800`; outlinedSecondary borda `alpha(#1A1A1A,.3)` |
| `MuiTextField` | `variant:outlined`, `size:small`, `fullWidth:true` (defaults globais — forms herdam) |
| `MuiOutlinedInput` | radius 10; `input.fontSize 1rem` mobile / `0.875rem` ≥600px (anti-zoom iOS — ver §RESPONSIVIDADE) |
| `MuiCard` | radius 16 + boxShadow custom |
| `MuiPaper` | rounded radius 16 |
| `MuiChip` | `fontWeight 600`, radius 8 |
| `MuiAppBar`/`MuiDrawer` | `backgroundImage:none` (mata gradient dark-mode MUI) |
| `MuiDivider` | borda `rgba(0,0,0,0.07)` |

## INVENTÁRIO DE COMPONENTES UI (`src/components/ui/*`)
Story = `*.stories.tsx`. A11y test (vitest-axe dedicado) = `*.a11y.test.tsx`. Cobertura a11y UNIT alternativa em `__tests__/a11y.test.tsx` (foco/keyboard/aria, não axe).

| Componente | Propósito | Props-chave | Story? | a11y test? |
|---|---|---|---|---|
| `AlertBanner` | alerta inline colapsável | `open`, `severity`(error\|warning\|info\|success, def error), `title?`, `message`, `onClose?` | ✅ | ✅ vitest-axe (4 severities + sem title + close) |
| `StatusChip` | chip de status domínio | `status`(AlunoStatus\|TreinadorStatus\|VinculoStatus), `size`(small def) | ✅ | ✅ vitest-axe (rotulado: `aria-label="Status: ${label}"`) |
| `EmptyState` | estado vazio + CTA | `message`, `actionLabel?`, `onAction?` | ✅ | ✅ vitest-axe |
| `LoadingSpinner` | spinner (page/inline) | `fullPage`(def false), `label`(def "Carregando" → `aria-label`) | ✅ | ✅ vitest-axe |
| `ConfirmDialog` | dialog confirmação genérico | `open`, `title`, `description`, `confirmLabel`, `cancelLabel`, `destructive`, `loading`, `children?`, `onConfirm`, `onClose` | ✅ | ⚠️ UNIT only (`__tests__/a11y.test.tsx`: aria-describedby + autoFocus) — SEM vitest-axe dedicado |
| `ResponsiveTable` | tabela→cards responsiva | `columns[]`(Column: label/align/mobileRole), `rows`, `rowKey`, `renderCell`, `onRowClick?`, `pagination?` | ✅ | ⚠️ UNIT only (`__tests__/a11y.test.tsx`: row role/tabIndex/keyboard) — SEM vitest-axe dedicado |
| `DataList<T>` | wrapper loading/empty/table | `loading`, `items`, `emptyMessage`, `columns`, `rowKey`, `renderCell`, `onRowClick?`, `pagination?` | ❌ | ❌ (compõe LoadingSpinner+EmptyState+ResponsiveTable já testados) |
| `ConsentBanner` | dialog consentimento LGPD (cookies) | `forceOpen?`, `onClose?` — ver [specification-lgpd] | ❌ | ⚠️ `__tests__/ConsentBanner.test.tsx` (comportamento, não axe) |
| `ConsentProvider` | wrapper `dynamic(ssr:false)` do ConsentBanner (evita hydration mismatch — lê cookie) | — | ❌ | ❌ |
| `SnackbarProvider` | provider toast global + `useSnackbar()` | `showSnackbar(msg, severity?)`; autoHide 4000ms; bottom-center; Alert `variant:filled` | ❌ | ❌ |
| `ErrorBoundary` | class boundary + Sentry capture | `children`, `fallback?` | ❌ | ❌ |
| `InfoLine` | label:value inline (sem `"use client"`) | `label`, `value` | ❌ | ❌ |
| `Logo` | wordmark "forzion.tech" | `size`(sm\|md\|lg), `sx?` | ❌ | ❌ |

Barrel `ui/index.ts` exporta APENAS: Logo, StatusChip, EmptyState, ConfirmDialog, AlertBanner, LoadingSpinner. Os demais (DataList, ResponsiveTable, ConsentBanner, ConsentProvider, SnackbarProvider, ErrorBoundary, InfoLine) importados por path direto.

## INVENTÁRIO FORMS (`src/components/forms/*`)
Integração react-hook-form via `Controller` + `useFormContext` (RHF context obrigatório no ancestral — ver [specification-frontend] §forms/validação). Erro Zod renderizado em `helperText`/`FormHelperText`.

| Componente | Propósito | Props-chave | Story? | a11y test? |
|---|---|---|---|---|
| `FormTextField` | TextField controlado | `name` + `...TextFieldProps`; `error`/`helperText` ← `fieldState.error.message` | ❌ | ❌ |
| `FormSelect` | Select controlado | `name`, `label`, `options[]`(value/label), `required?`; `InputLabel id={name}-label` linkado | ❌ | ❌ |
| `PasswordField` | senha + toggle visibilidade | `name` + `...TextFieldProps` (sem `type`); IconButton `aria-label` "Mostrar/Ocultar senha" | ❌ | ❌ |

Barrel `forms/index.ts`: FormTextField, FormSelect, PasswordField. NENHUM form component tem story nem a11y test dedicado (gap — ver §GOVERNANCE).

## GOVERNANCE
- **`ui/` vs domínio**: `src/components/ui/*` = genérico/reutilizável, ZERO acoplamento a entidade de negócio (exceção pragmática: `StatusChip` importa enums de `@/types`). Componente acoplado a domínio (aluno/treinador/admin/pagamento) vive em `src/components/<dominio>/*` (ex. `components/aluno/AlunoInadimplenteBanner.tsx`), NÃO em `ui/`.
- **Forms**: `src/components/forms/*` = primitivos RHF reutilizáveis. Form de domínio compõe estes; não duplicar lógica `Controller`.
- **Requisito ao CRIAR componente em `ui/`**: idealmente story (`*.stories.tsx`) + a11y test. ESTADO REAL: cumprido só pelos 4 primeiros da tabela (AlertBanner/StatusChip/EmptyState/LoadingSpinner têm story+vitest-axe); ConfirmDialog/ResponsiveTable têm story+a11y-unit; demais não têm nenhum. Padrão a alvejar, não invariante atual.
- **Padrão de props**: discriminantes via enum string — `severity`(error\|warning\|info\|success), `size`(small\|medium), `status`(enums domínio), `mobileRole`(primary\|secondary\|actions\|hidden). Booleans de modo: `destructive`, `loading`, `fullPage`, `forceOpen`. Callbacks `on*` opcionais habilitam comportamento (ex. `onRowClick` ⇒ row vira `role=button` focável).
- **Barrel**: adicionar export em `ui/index.ts` apenas se o componente for de uso amplo (os de provider/wrapper ficam fora propositalmente).

## ACESSIBILIDADE
**Conformance alvo: WCAG 2.1 AA** (declarado como ALVO; sem doc de auditoria formal de conformidade no repo). Tags axe usadas: `wcag2a + wcag2aa + wcag21a + wcag21aa` (`frontend/e2e/utils/axe.ts`).

### Harness (REFERENCIAR [specification-tests]; não reexecutar disciplina aqui)
| Camada | Ferramenta | Local | Escopo |
|---|---|---|---|
| Unit/componente | `vitest-axe` (`axe()` + `toHaveNoViolations`) | `src/components/ui/*.a11y.test.tsx` | AlertBanner, StatusChip, EmptyState, LoadingSpinner |
| Unit a11y comportamental | RTL (sem axe) | `src/components/ui/__tests__/a11y.test.tsx` | ConfirmDialog (aria-describedby, autoFocus), ResponsiveTable (role/tabIndex/keyboard) |
| Página E2E | `@axe-core/playwright` (`AxeBuilder`) | `e2e/utils/axe.ts` → `e2e/specs/a11y/all-pages-axe.spec.ts` | varre rotas-chave por role/storage-state |
| Storybook | `@storybook/addon-a11y` | `.storybook/main.ts` (addon) + `preview.tsx` (`parameters.a11y.element:#storybook-root`, `manual:false`) | toda story |
| Lighthouse | `categories:accessibility minScore 0.95` (error gate) | `frontend/lighthouserc.json` | `/`, `/login`, `/cadastro/aluno`, `/cadastro/treinador` (REFERENCIAR [specification-observability]) |

`pkg`: `@axe-core/playwright ^4.11.3`. `runAxe(page)` = tags AA (wcag2a/aa + wcag21a/aa) **INCLUINDO `color-contrast`** — sem `disableRules`. Único helper (não há mais `runAxeStrict`).

### CONFORMANCE WCAG 2.1 AA — color-contrast (F18 RESOLVIDO)
- **Conformance real**: WCAG 2.1 AA pleno, **incluindo 1.4.3 contraste**. `color-contrast` gateia no `runAxe` default (`e2e/utils/axe.ts`), aplicado por `e2e/specs/a11y/all-pages-axe.spec.ts` (`expect(violations).toEqual([])`) sobre rotas públicas + autenticadas.
- **Fix de tema** (`src/lib/theme/index.ts`): `text.secondary` `#6B7280`→`#4B5563` (gray-600; 7.6:1 #FFF / 7.1:1 #F7F8FA — antes ≈4.6:1 em #F7F8FA, fonte das violações: secondary, placeholder, disabled, helperText herdam disto); `error.main` `#D32F2F`→`#C62828` (5.6:1 / 5.3:1; branco-sobre-error em botão = 5.6:1 AA). `text.primary #111827` e `primary.contrastText #1A1A1A` sobre `primary #F5C400` (10.6:1) já passavam.
- **Removido**: ratchet spec `color-contrast-ratchet.spec.ts` e helper `runAxeStrict` (não havia outro uso). Gate hard ON — sem mitigação por ratchet.
- **Lighthouse**: a11y 0.95 (que inclui contraste) agora alinhado ao axe E2E — fontes de verdade convergentes.

### Padrões a11y reais encontrados
- **Foco / keyboard nav**:
  - `ConfirmDialog`: `aria-describedby` → id da descrição; `autoFocus` condicional — destrutivo ⇒ foco no **Cancelar** (`autoFocus={destructive}`), não-destrutivo ⇒ foco no **Confirmar** (anti-clique-acidental).
  - `ResponsiveTable`: linha clicável (desktop `TableRow`, mobile card `Box`) recebe `role="button"` + `tabIndex={0}` + `onKeyDown` (Enter/Space com `preventDefault`) SOMENTE quando `onRowClick` definido; coluna `actions` faz `stopPropagation`.
- **`aria-label`**: `LoadingSpinner` (`CircularProgress aria-label={label}`); `StatusChip` (`Chip aria-label={\`Status: ${label}\`}` — `src/components/ui/StatusChip.tsx`, nome acessível explícito do status); `PasswordField` IconButton ("Mostrar/Ocultar senha"); `ConsentBanner` Dialog ("Consentimento de cookies e privacidade LGPD"); IconButtons de ação em páginas treinador/aluno/admin; charts admin via `<figure aria-label=...>` (testado em `admin/__tests__/a11y-dashboard.test.tsx`).
- **`role="alert"`**: `components/aluno/AlunoInadimplenteBanner.tsx` (anúncio assertivo). NOTA: AlertBanner/Snackbar usam MUI `Alert` (role implícito); **não há `aria-live` explícito** custom para erros de formulário — erros vão via `helperText` (associação `aria-describedby` provida pelo MUI TextField/FormControl).
- **`prefers-reduced-motion`**: HONRADO em runtime. `src/styles/globals.css` tem `@media (prefers-reduced-motion: reduce)` que neutraliza animações/transições globalmente (`animation-duration`/`transition-duration: 0.01ms !important`, `animation-iteration-count: 1`, `scroll-behavior: auto`) em `*, *::before, *::after` — cobre transições MUI (`transition: all 0.15s ease` em Button/ListItemButton). Harness de teste `src/test/determinism/motion.ts` força `reduce` via `matchMedia` stub p/ determinismo.

## RESPONSIVIDADE (REFERENCIAR [specification-frontend] §RESPONSIVIDADE — não duplicar)
- Breakpoints MUI default; corte mobile/desktop em `md` (900px). `ResponsiveTable` usa `useMediaQuery(breakpoints.down("md"))`: ≥md = `<Table>`; <md = cards (primary/secondary/actions via `Column.mobileRole`, fallback `resolveRole`: idx0=primary, último right-aligned=actions, resto=secondary).
- `AppLayout`: Drawer permanente colapsável ≥md; Drawer temporário + `BottomNavigation` fixo <md (detalhes/safe-area em [specification-frontend] §APPLAYOUT/§RESPONSIVIDADE).
- Input `fontSize 1rem` em mobile (`MuiOutlinedInput.input`) previne auto-zoom iOS Safari; `0.875rem` ≥600px. Dialogs (`ConfirmDialog`, `ConsentBanner`) usam `maxHeight: calc(100dvh - 32px)` p/ viewport mobile.
