## Regras de Ouro

* Valide todos os arquivos que contêm skills a procura de problemas/adaptações/melhorias/duplicidades.
* Essas são as regras de ouro do projeto backend. NUNCA podem ser quebradas.
* Caso o Claude verifique a necessidade de alteração/adaptação do documento atual, deve solicitar aprovação MANUAL, mesmo que o modo de execução NÃO necessite ou solicite aprovação.
* Todos os arquivos contendo skills devem ser tratados também como regras de ouro.
* O Claude não pode tomar NENHUMA decisão que não esteja de acordo com as skills mapeadas. Em caso de divergência ou necessidade de alteração, uma aprovação MANUAL deve ser realizada.

---

### Segurança (Prioridade Máxima)

* É proibido expor segredos, tokens, credenciais ou qualquer informação sensível no código.
* Toda entrada de dados deve ser validada.
* Toda operação deve respeitar mecanismos adequados de autenticação e autorização.
* Dados devem ser protegidos em trânsito (HTTPS) e, quando aplicável, em repouso.
* Qualquer alteração que impacte segurança deve ser tratada como crítica.

---

### Rastreabilidade e Auditoria

* Toda alteração deve ser rastreável por meio de commits e Pull Requests.
* Nenhuma mudança pode ocorrer fora do controle de versão.
* Commits devem ser claros, objetivos e descritivos.
* Logs devem permitir rastrear:

  * ações críticas
  * erros
  * operações sensíveis

---

### Qualidade e Confiabilidade

* Nenhuma funcionalidade deve ser considerada concluída sem validação mínima.
* O código deve ser:

  * legível
  * testável
  * desacoplado

* Toda e qualquer alteração deve possuir testes.
* Bugs devem ser tratados com análise de causa raiz.

---

### Controle de Risco

* Toda e qualquer alteração deve considerar impacto em produção (main).
* Deve existir estratégia de rollback para mudanças relevantes.
* Deploy deve ser previsível, controlado e reproduzível.
* Mudanças que possam causar indisponibilidade devem ser evitadas ou devidamente mitigadas.

---

## Code Review (Obrigatório)

* Detalhamento está no arquivo "skill_code-review.md".

---

## Padrão de Pull Request

* Detalhamento está no arquivo "skill_pull-request.md".

---

## Definition of Done (DoD)

Uma funcionalidade só é considerada concluída quando:

* A implementação atende ao requisito definido
* O código foi revisado e aprovado
* Testes foram realizados
* Não há erros críticos conhecidos
* Logs e tratamento de erros foram implementados
* Está pronta para deploy sem necessidade de ajustes adicionais

---

## Regras de Arquitetura

* Detalhamento está no arquivo "skill_regras-arquitetura.md".

---

## Engenharia

* Detalhamento está no arquivo "skill_engenharia.md".

---

## Requisitos Funcionais

* Detalhamento está no arquivo "skill_requisitos-funcionais.md".

---

## Regra Absoluta

* Nenhuma dessas regras pode ser ignorada sem justificativa explícita.
* Em caso de dúvida, deve-se sempre priorizar:

* Segurança > Escalabilidade > Simplicidade