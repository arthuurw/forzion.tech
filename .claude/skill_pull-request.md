## Skill: Pull Request (PR)

### Objetivo

Garantir que toda alteração no sistema seja introduzida de forma controlada, rastreável, revisável e segura, permitindo validação completa antes da integração ao código principal.

---

### Princípios

* Nenhuma alteração deve ser integrada sem Pull Request.
* Toda PR deve ser clara, objetiva e revisável.
* O objetivo da PR é facilitar a análise, não apenas entregar código.
* PRs devem ser pequenas, focadas e de fácil entendimento.

---

### Estrutura Obrigatória da Pull Request

Toda PR deve conter:

#### 1. Título

* Claro e objetivo
* Deve resumir a alteração

Exemplo:

* `feat: criação de endpoint para cadastro de treino`
* `fix: correção de validação de login`

---

#### 2. Descrição

Deve responder:

* O que foi feito?
* Por que foi feito?
* Qual problema resolve?

---

#### 3. Escopo da Alteração

* O que foi alterado (módulos, arquivos, funcionalidades)
* O que não foi alterado (para evitar ambiguidade)

---

#### 4. Impacto

* Impacto técnico (performance, arquitetura, banco)
* Impacto funcional (mudança para usuário)
* Riscos envolvidos

---

#### 5. Evidência de Testes

* Como foi testado?
* Cenários validados
* Evidências (prints, logs, etc., se aplicável)

---

#### 6. Checklist Obrigatório

* A alteração foi revisada pelo autor
* Código segue os padrões do projeto
* Não há segredos expostos
* Testado localmente
* Não quebra funcionalidades existentes
* Logs e tratamento de erro foram considerados

---

### Boas Práticas

* PR deve conter uma única responsabilidade (single purpose)
* Evitar PRs grandes (dificultam revisão)
* Manter consistência com padrões do projeto
* Remover código morto antes de submeter
* Garantir que o código compila e executa corretamente

---

### Tamanho da PR

* Ideal: pequena a média (até ~400 linhas alteradas)
* PRs grandes devem ser evitadas ou divididas

---

### Tipos de Pull Request

* **Feature**: nova funcionalidade
* **Fix**: correção de bug
* **Refactor**: melhoria sem mudança funcional
* **Chore**: ajustes técnicos (configuração, build, etc.)

---

### Critérios de Bloqueio

Uma PR não deve ser aprovada se houver:

* Falhas de segurança
* Código não funcional
* Violação de arquitetura
* Falta de clareza na descrição
* Ausência de validação/testes mínimos
* Impacto não analisado

---

### Fluxo Padrão

1. Criar branch (`feature/*`, `fix/*`, etc.)
2. Implementar a alteração
3. Validar localmente
4. Criar Pull Request
5. Preencher todas as seções obrigatórias
6. Submeter para review
7. Ajustar conforme feedback
8. Aprovação
9. Merge

---

### Anti-padrões (Proibidos)

* PR sem descrição
* PR genérica ou ambígua
* PR com múltiplas responsabilidades
* PR sem validação
* PR muito grande sem justificativa
* Merge sem aprovação

---

### Responsabilidade do Autor

* Garantir que a PR está clara e revisável
* Antecipar possíveis dúvidas do revisor
* Validar o funcionamento antes de submeter
* Corrigir feedbacks de forma adequada

---

### Regra Final

Se uma Pull Request não pode ser entendida rapidamente por outro desenvolvedor, ela não está pronta para revisão.