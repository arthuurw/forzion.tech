## Skill: Code Review

### Objetivo

Garantir que toda alteração no código seja segura, correta, consistente com a arquitetura e sustentável a longo prazo, antes de ser integrada ao sistema.

---

### Princípios

* Code Review não é opcional.
* O foco é na **qualidade do sistema**, não no autor do código.
* Revisões devem ser objetivas, técnicas e construtivas.
* Melhorar o código é mais importante do que apenas aprovar rapidamente.

---

### Critérios Obrigatórios de Avaliação

#### 1. Correção e Lógica

* A implementação resolve corretamente o problema proposto?
* Existem cenários não tratados (edge cases)?
* Há risco de erro em tempo de execução?
* Fluxos negativos e exceções foram considerados?

---

#### 2. Segurança

* Inputs estão sendo validados corretamente?
* Existe risco de:

  * SQL Injection
  * exposição de dados sensíveis
  * falhas de autenticação/autorização
* Dados sensíveis estão protegidos?
* Nenhum segredo foi exposto no código?

---

#### 3. Arquitetura e Organização

* O código respeita a Clean Architecture?
* Há separação correta de responsabilidades?
* Existe lógica indevida em controllers ou camada errada?
* Dependências estão corretamente invertidas (interfaces)?

---

#### 4. Legibilidade e Manutenibilidade

* O código é fácil de entender?
* Nomes de variáveis, métodos e classes são claros?
* Métodos estão pequenos e coesos?
* Existe código duplicado ou desnecessário?

---

#### 5. Performance

* Existem operações desnecessárias ou ineficientes?
* Queries estão otimizadas?
* Há risco de:

  * N+1 queries
  * loops custosos
  * processamento excessivo em memória?

---

#### 6. Testes

* A funcionalidade foi testada?
* Existem testes automatizados quando necessário?
* Casos críticos possuem cobertura mínima?

---

#### 7. Impacto e Compatibilidade

* A alteração quebra algo existente?
* Existe impacto em outras partes do sistema?
* APIs mantêm compatibilidade?

---

### Checklist de Aprovação

Antes de aprovar uma PR, validar:

* A implementação está correta
* Não há falhas de segurança
* Código segue padrões definidos
* Não há impacto negativo relevante
* Está legível e sustentável
* Foi testado adequadamente

Se qualquer item acima falhar, a PR não deve ser aprovada.

---

### Níveis de Feedback

#### Obrigatório (bloqueia aprovação)

* Falhas de segurança
* Erros de lógica
* Violação de arquitetura
* Código não funcional

---

#### Recomendado (não bloqueia, mas deve ser considerado)

* Melhorias de legibilidade
* Refatorações
* Otimizações leves

---

#### Opcional

* Sugestões de estilo pessoal
* Melhorias não críticas

---

### Boas Práticas de Review

* Ser claro e direto
* Explicar o motivo da sugestão
* Sugerir soluções, não apenas apontar problemas
* Evitar comentários vagos como “melhorar isso”

---

### Anti-padrões (Proibidos)

* Aprovar sem revisar
* Revisar superficialmente
* Fazer críticas pessoais
* Ignorar problemas conhecidos
* Aprovar código com dúvida

---

### Responsabilidade

* Quem revisa é responsável pela qualidade da alteração aprovada.
* Aprovação implica concordância técnica com a implementação.

---

### Regra Final

Nenhuma Pull Request deve ser aprovada sem revisão completa, consciente e alinhada com os princípios do projeto.