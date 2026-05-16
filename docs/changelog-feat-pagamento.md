# Changelog — branch `feat/pagamento`

Documento que registra tudo que foi implementado e o que ainda precisa ser feito nesta branch antes de abrir PR para `main`.

---

## Implementado

### Módulo de Pagamentos (Stripe Connect + Pix + Cartão)

**Backend**

- `forzion.tech.Api/Endpoints/Pagamentos/PagamentosEndpoints.cs`
  - Endpoints de onboarding Stripe Connect (iniciar, verificar status)
  - Webhook interno para confirmação de pagamento (Pix e Cartão)
  - Endpoint de geração de cobrança (Pix e Cartão via Payment Intent)
  - Endpoint de consulta de pagamento por ID
  - Endpoint de listagem de pagamentos por assinatura
  - Correção de segurança: `FixedTimeEquals` agora verifica comprimento dos bytes antes de comparar (evitava `ArgumentException` em spans de tamanhos diferentes)

- `forzion.tech.Api/Endpoints/Treinador/TreinadorEndpoints.cs`
  - Endpoints de gestão de pacotes do treinador
  - Endpoint de listagem de vínculos ativos com `pacoteAlunoId`

- `forzion.tech.Application/forzion.tech.Application.csproj`
  - Dependências do módulo de pagamentos adicionadas

- `forzion.tech.Infrastructure/forzion.tech.Infrastructure.csproj`
  - Dependências do módulo de pagamentos adicionadas (Stripe SDK)

**Frontend — Componentes**

- `frontend/src/components/pagamento/PagamentoPix.tsx`
  - Exibe QR Code Pix e código copia-e-cola
  - Polling a cada 30 segundos para verificar status
  - Trata estados: Pago, Expirado, Falhou, Aguardando

- `frontend/src/components/pagamento/PagamentoCartao.tsx`
  - Integra Stripe Elements (`PaymentElement`)
  - Confirma pagamento via `stripe.confirmPayment`
  - Trata retorno 3DS (redirect) e pagamento síncrono

**Frontend — Páginas**

- `frontend/src/app/(aluno)/aluno/assinatura/page.tsx`
  - Exibe status da assinatura ativa do aluno
  - Permite visualizar pacote e valor

- `frontend/src/app/(aluno)/aluno/pagamentos/page.tsx`
  - Tabela de histórico de pagamentos com status e valor
  - Dialog com componente Pix ou Cartão conforme `metodoPagamento`
  - **Atenção: página ainda é stub — ver seção Pendências**

- `frontend/src/app/(treinador)/treinador/pagamentos/page.tsx`
  - Exibe status da conta Stripe do treinador
  - Botão para iniciar/continuar onboarding Stripe Connect

- `frontend/src/app/(treinador)/treinador/onboarding/retorno/page.tsx`
  - Página de retorno pós-onboarding Stripe
  - Verifica se onboarding foi concluído e exibe feedback

---

### Segurança / Dependências

- `frontend/package.json`
  - Removido `xlsx@0.18.5` (7 CVEs ativos)
  - Adicionado `exceljs@^4.4.0` (sem CVEs conhecidos)
  - Adicionado `overrides: { "postcss": "^8.5.10" }` para fixar CVE transitiva sem fazer downgrade do Next.js

- `frontend/src/lib/utils/excel.ts`
  - Reescrito completamente para ExcelJS
  - `safeCell(v)` prefixa com `'` strings que começam com `=`, `+`, `-`, `@`, `|`, `%` — defesa contra injeção de fórmula (Excel avalia esses caracteres como gatilho de fórmula; SheetJS não avaliava)
  - `exportarFichaParaExcel` usa import dinâmico (`await import("exceljs")`) para evitar carregamento no SSR
  - Download via `Blob` + `URL.createObjectURL` + anchor click

- `frontend/src/lib/utils/excel.test.ts`
  - Expandido de 45 para 58 testes
  - Mock do ExcelJS via `vi.hoisted` + `class WorkbookMock` (arrow functions não são construíveis com `new` — padrão obrigatório para mocks de classe no Vitest 4.x)
  - Teste específico de injeção de fórmula: verifica que `=EVIL()` se torna `'=EVIL()`

---

### Fix TypeScript CI/CD — MUI v9

MUI v9 removeu suporte a props de sistema (`p`, `mt`, `mb`, `fontWeight`, `alignItems`, `justifyContent`, `display`, `width`, `maxWidth`, `mx`, `textAlign`, `py`, `px`) como props diretas em `Box`, `Stack` e `Typography`. Devem ir dentro de `sx={{}}`.

Corrigido em 8 arquivos (30+ erros `TS2769` eliminados, `npx tsc --noEmit` → 0 erros):

| Arquivo | Tipo de fix |
|---|---|
| `(aluno)/aluno/assinatura/page.tsx` | 6 props movidas para `sx` |
| `(aluno)/aluno/pagamentos/page.tsx` | 2 props movidas para `sx` |
| `(aluno)/aluno/fichas/[fichaId]/page.tsx` | `void` prefix em callback assíncrono |
| `(treinador)/treinador/onboarding/retorno/page.tsx` | 4 props movidas para `sx` |
| `(treinador)/treinador/pagamentos/page.tsx` | 6 props movidas para `sx` |
| `(treinador)/treinador/treinos/[treinoId]/page.tsx` | `void` prefix em callback assíncrono |
| `components/pagamento/PagamentoCartao.tsx` | 2 props movidas para `sx` |
| `components/pagamento/PagamentoPix.tsx` | 6 props movidas para `sx` |

---

### Features — Dashboards

**Dashboard do Treinador** (`(treinador)/treinador/page.tsx`)

- Fetch de vínculos ativos expandido de `tamanhoPagina: 1` para `100` para obter items (antes só obtinha contagem)
- Cartão **Receita Est./mês (MRR)** calculado como `Σ pacote.preco` por vínculo ativo, cruzando `pacoteAlunoId` com tabela de pacotes via `Map`
- Gráfico **Receita por Pacote** (bar chart horizontal, verde `#388e3c`) com tooltip formatado em BRL — só renderiza quando há dados
- Grid de stat cards expandido de 4 para 5 colunas em `md` (`repeat(5, 1fr)`)

**Dashboard Admin** (`(admin)/admin/page.tsx`) — reescrito completamente

- **6 stat cards** em dois grupos visuais:
  - Treinadores: Ativos (verde) | Pendentes (amarelo) | Inativos (cinza)
  - Alunos: Ativos (azul) | Pendentes (laranja) | Inativos (cinza)
- **3 tabs**:
  - *Visão Geral*: 4 gráficos — donut status treinadores, bar treinadores por plano, donut status alunos, bar alunos por finalidade (`FINALIDADE_LABEL`)
  - *Aprovações*: lista de treinadores pendentes com data de cadastro + botões Aprovar/Reprovar; badge de contagem na aba quando há pendentes
  - *Plataforma*: 3 counters (Planos / Exercícios Globais / Grupos Musculares), tabela de planos com preço/maxAlunos/treinadores usando cada um, lista dos 5 treinadores mais recentes com status chip e data
- 11 requests paralelos no `Promise.all`

---

### Documentação

| Arquivo | O que mudou |
|---|---|
| `README.md` | Stack atualizado (Stripe 9.x/6.x), contagem de testes 161→174 |
| `frontend/README.md` | Seção ExcelJS com `safeCell`, assinatura de `exportarFichaParaExcel`, padrão de mock via `vi.hoisted` + `class` |
| `docs/fluxo-sistema.md` | Seção 8 "Pagamentos" adicionada — Stripe Connect, Pix, Cartão, Onboarding; seções renumeradas; glossário atualizado |

---

## Pendências

### P1 — Crítico

**Página de pagamentos do aluno (`(aluno)/aluno/pagamentos/page.tsx`) é stub**

A página existe mas não busca dados reais:

```tsx
// Estado atual — vazio, sem fetch
const [pagamentos] = useState<PagamentoResponse[]>([]);
const [loading] = useState(false);
```

Para funcionar precisa de:
1. Buscar a assinatura ativa do aluno (não há endpoint direto — verificar se `pagamentoApi.obterAssinatura` ou similar existe no backend)
2. Com o `assinaturaId`, chamar `pagamentoApi.listarPagamentosAssinatura(assinaturaId)`
3. Implementar tratamento de loading, erro e estado vazio

O componente `TabelaPagamentos` já está pronto e correto — só falta o fetch.

---

### P2 — Alto

**S6667 — StripeService.cs: logging sem exception**

Em blocos `catch`, o logger registra a mensagem de erro mas não passa a `Exception` como argumento. Em produção o stack trace é perdido, dificultando debug de falhas no Stripe.

```csharp
// Atual (perde stack trace)
catch (Exception ex)
{
    _logger.LogError("Falha ao processar pagamento: {Message}", ex.Message);
}

// Correto
catch (Exception ex)
{
    _logger.LogError(ex, "Falha ao processar pagamento: {Message}", ex.Message);
}
```

Afeta: `StripeService.cs` (avaliar todos os blocos catch do serviço).

---

### P3 — Médio

**Fluxo de aprovação de alunos não mapeado**

`AlunoStatus` tem valor `AguardandoAprovacao`, o dashboard admin exibe contagem de alunos pendentes, mas `adminApi` não tem nenhum método `aprovarAluno`. Verificar:

- Alunos se auto-aprovam ao criar conta? (status inicial = `Ativo`)
- Aprovação é feita pelo treinador ao criar vínculo?
- Falta endpoint `POST /admin/alunos/{id}/aprovar`?

Se alunos não passam por aprovação admin, remover o stat card "Pendentes" do dashboard admin para não causar confusão.

---

### P4 — Baixo

**Abrir PR feat/pagamento → main**

Branch está pronta (4 commits, TypeScript limpo, 0 CVEs). Criar PR com:
- Descrição do módulo de pagamentos
- Checklist de testes manuais (onboarding Stripe, pagamento Pix, pagamento Cartão)
- Link para `.security-memory.md` com achados de segurança

---

## Commits desta branch (após merge de main)

```
b752350 docs: atualizar documentação do módulo de pagamentos e fluxo do sistema
64982b9 feat(dashboard): relatório financeiro do treinador e dashboard admin detalhado
5aae944 fix(frontend): migrar props do sistema MUI v9 para sx no módulo de pagamentos
78c652e fix: corrigir vulnerabilidades de segurança e migrar xlsx para ExcelJS
7a5af64 Remove comentários redundantes de seção em todo o codebase
4387a7a Merge origin/main em feat/pagamento
8dd6186 Implementação do módulo de pagamentos (Stripe Connect + Pix + Cartão)
```
