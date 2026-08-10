# Auditoria de conformidade WCAG 2.1 AA — forzion.tech

Documento vivo, versionado. Complementa a camada automatizada (`vitest-axe`,
`@axe-core/playwright`, Lighthouse a11y — ver `specs/specification-frontend-ui.md`
§ACESSIBILIDADE) com os critérios AA que ferramenta automática não decide:
ordem de foco, qualidade de texto alternativo, percurso real de leitor de
tela, reflow/zoom, e `prefers-reduced-motion`.

**Status**: roteiro pronto — vereditos pendentes até o percurso manual (NVDA +
VoiceOver) ser executado e relatado.
**Data da avaliação**: TODO — preenchida ao fechar a auditoria (feature
`a11y-conformidade-aa`, fase de compilação de veredito).
**Escopo**: TODO — rotas representativas por role (pública, aluno, treinador,
admin), preenchido junto do veredito.

## Como este documento é usado

- **Critérios automatizáveis por engine de browser** (ordem de foco lógica,
  foco visível, navegação por teclado sem armadilha, reflow a 320px, zoom
  200%, `prefers-reduced-motion`) são cobertos por
  `frontend/e2e/specs/a11y/manual-checklist-automatizavel.spec.ts` — os
  resultados desse spec alimentam a tabela de veredito abaixo.
- **Critérios que exigem leitor de tela real** (o que é efetivamente
  anunciado, ordem de leitura percebida, qualidade semântica) são cobertos
  pelo roteiro manual desta seção, executado por humano com NVDA (Windows) e
  VoiceOver (macOS/iOS) — sem substituto automatizado confiável hoje.
- Achado que também seria detectável por `axe`/Lighthouse é tratado como
  falha do harness automatizado (investigar por que a regra/rota não
  pegou), não só como item desta auditoria manual.

## Roteiro de percurso manual — NVDA (Windows) + VoiceOver (macOS/iOS)

Executar cada fluxo com **ambos** os leitores de tela, um de cada vez, do
zero, **sem mouse** (navegação só por teclado/gestos do leitor de tela).
Para cada passo, anotar o que foi **efetivamente anunciado** (texto e
ordem) — não só se "funcionou". Divergência entre o anunciado e a coluna
"Esperado" é candidata a achado (vira correção se for violação AA; vira
backlog se for melhoria acima de AA).

### Fluxo 1: Login (`/login`)

| # | Ação | Esperado (o que o leitor de tela deve anunciar / focar) |
|---|---|---|
| 1 | Carregar `/login` do zero | Título da página anunciado antes de qualquer outro conteúdo; primeiro elemento focável ao dar Tab é o skip-link ("Pular para o conteúdo") |
| 2 | Ativar skip-link (Enter) | Foco pula para `#main-content`; cabeçalho "Acesse sua conta" (h1) é o próximo conteúdo lido, seguido do subtítulo "Informe suas credenciais para acessar a plataforma." |
| 3 | Tab até o campo "E-mail" | Anuncia rótulo "E-mail", tipo de campo (edit/text), e se é obrigatório (`required`) |
| 4 | Tab até o campo "Senha" | Anuncia rótulo "Senha", tipo "password"/protegido, e o botão de alternar visibilidade ("Mostrar senha"/"Ocultar senha") é alcançável e anunciado antes de sair do grupo do campo |
| 5 | Submeter com credenciais inválidas | O erro (`AlertBanner` de severidade error) é anunciado sem exigir navegação manual até ele (região seria assertiva/live ou foco movido para o alerta) |
| 6 | Submeter com credenciais válidas quando MFA está pendente | Novo conteúdo (rótulo "Código de verificação" ou "Código de recuperação") é anunciado; campo recebe foco automaticamente (`autoFocus`) — confirmar que o leitor de tela também move o cursor de leitura pra lá, não só o foco de teclado |
| 7 | Navegar pelas opções alternativas de MFA ("Usar código por e-mail", "Usar código de recuperação") | Cada botão é anunciado como botão, com rótulo claro da ação, não ambíguo |

### Fluxo 2: Cadastro de aluno (`/cadastro/aluno`)

Fluxo em stepper de 4 passos (`Stepper` MUI, `alternativeLabel`). Roteirizar
o percurso completo, com atenção especial à troca de passo (região viva /
foco movido, já que o conteúdo muda sem navegação de página).

| # | Ação | Esperado |
|---|---|---|
| 1 | Carregar `/cadastro/aluno` do zero | h1 "Criar conta como aluno" + subtítulo anunciados; indicador "Passo 1 de N" perceptível (texto, não só visual) |
| 2 | Percorrer o `Stepper` (`alternativeLabel`) via teclado | Cada step é identificável (rótulo + posição, ex. "passo 1 de 4", não numeração solta sem contexto); o step ativo é diferenciável dos demais |
| 3 | Passo 1 (seleção de treinador) → avançar sem selecionar nada | Erro/validação é anunciado antes de tentar avançar silenciosamente |
| 4 | Passo 3 (dados da conta): Tab pelos campos `Nome completo`, `E-mail`, `Celular`, `Senha`, `Confirmar senha` | Cada rótulo anunciado corretamente associado ao campo (não herdado do campo anterior); `helperText` do campo "Celular" ("Somente dígitos, ex: 11987654321") é anunciado como parte da descrição do campo, não perdido |
| 5 | Passo 3 → submeter com senha e confirmação divergentes | Erro de validação Zod é anunciado associado ao campo `Confirmar senha` (`aria-describedby`), sem exigir busca manual pelo erro |
| 6 | Passo 4 (perfil de treino): Tab pelos `FormSelect` ("Dias disponíveis por semana", "Tempo disponível por dia", "Finalidade do treino", "Nível de condicionamento atual") | Cada `Select` anuncia rótulo + valor selecionado + que é um combobox/listbox; opções desabilitadas (se houver) são anunciadas como indisponíveis |
| 7 | Passo 4 → checkbox de consentimento de dados de saúde | Rótulo completo do consentimento é lido antes do estado marcado/desmarcado, não truncado |
| 8 | Concluir o cadastro | Confirmação de sucesso é anunciada sem exigir navegação manual até ela |

### Fluxo 3: Registro de execução de treino (`/aluno/fichas/[fichaId]/executar`)

Fluxo com estado local complexo (progresso entre exercícios, séries por
exercício, confirmação final) — maior risco de anúncio desatualizado ou
foco perdido entre trocas de exercício.

| # | Ação | Esperado |
|---|---|---|
| 1 | Carregar a página do zero | Nome do primeiro exercício (h5) é anunciado; indicador de progresso ("N/total", `Chip`) é perceptível como texto, não só cor/posição |
| 2 | Se houver rascunho pendente (banner `role="alert"` com "Continuar"/"Descartar") | O alerta é anunciado assertivamente (sem precisar navegar até ele) antes do restante do conteúdo do exercício |
| 3 | Tab pelos campos de série (carga/repetições, `TextField`) da tabela de séries | Cada campo anuncia a que série/exercício pertence (não um "editar" genérico sem contexto — checar se o rótulo é suficiente fora do contexto visual da linha da tabela) |
| 4 | Avançar pro próximo exercício (botão com `endIcon`) | Ao trocar de exercício, o leitor de tela anuncia o novo nome do exercício e o novo progresso — não fica "preso" lendo o estado antigo; foco não é perdido (não cai em `body`) |
| 5 | Navegar pelos indicadores de exercício (`aria-label="Ir para exercício N"`) | Cada indicador é identificável individualmente (não um grupo de botões idênticos sem diferenciação por leitor de tela) |
| 6 | Abrir o diálogo de confirmação de conclusão ("Concluir treino") | Foco move para dentro do diálogo; título/descrição do diálogo são anunciados antes dos botões de ação |
| 7 | Cancelar o diálogo (Escape ou botão "Cancelar") | Diálogo fecha e foco retorna ao botão que abriu (mesmo padrão de `ConfirmDialog` verificado automaticamente em `manual-checklist-automatizavel.spec.ts`) |
| 8 | Confirmar a conclusão | Resultado (sucesso/erro) é anunciado sem exigir busca manual |

## Critérios adicionais fora do percurso dos 3 fluxos

Verificar uma vez por role (não exige repetir os 3 fluxos):

| Critério | Como verificar |
|---|---|
| Qualidade do texto alternativo | Inspecionar imagens/ícones semânticos das rotas amostradas (pública, aluno, treinador, admin); `alt` vazio em decorativo, descritivo em informativo |
| Hierarquia de cabeçalhos | Percorrer com o leitor de tela em "modo cabeçalhos" (NVDA: tecla `H`; VoiceOver: rotor) nas mesmas rotas amostradas — sem pulo de nível (h1→h3 sem h2) |
| Identificação do idioma do documento | Inspecionar `<html lang="...">` renderizado (`view-source` ou devtools) — confirmar `pt-BR` |

## Vereditos — TODO

Preenchido na compilação final da auditoria (feature `a11y-conformidade-aa`),
após: (a) o relato do usuário do percurso manual acima com NVDA + VoiceOver,
e (b) o resultado de `frontend/e2e/specs/a11y/manual-checklist-automatizavel.spec.ts`
(critérios automatizáveis). Veredito possível por critério: **conforme** /
**não-conforme** / **não-aplicável** (com razão).

| Critério | Método | Veredito | Evidência |
|---|---|---|---|
| Ordem de foco lógica | E2E automatizado | TODO | |
| Foco visível | E2E automatizado | TODO | |
| Navegação por teclado sem armadilha | E2E automatizado | TODO | |
| Reflow a 320px | E2E automatizado | TODO | |
| Zoom 200% | E2E automatizado | TODO | |
| `prefers-reduced-motion` | E2E automatizado | TODO | |
| Qualidade do texto alternativo | Percurso manual | TODO | |
| Hierarquia de cabeçalhos | Percurso manual | TODO | |
| `lang` do documento | Inspeção manual | TODO | |
| Percurso NVDA — Login | Percurso manual (Fluxo 1) | TODO | |
| Percurso NVDA — Cadastro de aluno | Percurso manual (Fluxo 2) | TODO | |
| Percurso NVDA — Registro de execução | Percurso manual (Fluxo 3) | TODO | |
| Percurso VoiceOver — Login | Percurso manual (Fluxo 1) | TODO | |
| Percurso VoiceOver — Cadastro de aluno | Percurso manual (Fluxo 2) | TODO | |
| Percurso VoiceOver — Registro de execução | Percurso manual (Fluxo 3) | TODO | |

## Achados

TODO — preenchido na compilação final. Toda violação de nível AA encontrada
vira correção nesta mesma feature; achado acima de AA (melhoria, não
exigência AA) vira item de backlog, fora desta feature.
