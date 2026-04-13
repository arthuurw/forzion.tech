## Skill: Requisitos Funcionais e Não Funcionais

### Objetivo

Definir de forma clara, completa e estruturada todos os comportamentos esperados do sistema (funcionais) e as características de qualidade (não funcionais), garantindo alinhamento entre produto, engenharia e validação.

---

## 1. Requisitos Funcionais

## 1.1 Usuários e Autenticação

O sistema deve permitir:

* Cadastro de usuários com email e senha
* Login com autenticação baseada em JWT
* Recuperação de senha
* Atualização de dados do perfil
* Controle de acesso por roles (Admin, Trainer)

### Regras

* Cada usuário deve estar vinculado a um tenant
* Apenas usuários autenticados podem acessar o sistema
* Tokens devem ser validados a cada requisição

---

## 1.2 Tenants (Organizações)

O sistema deve permitir:

* Criação de tenant ao registrar usuário
* Gerenciamento de organização
* Convite de novos membros via e-mail
* Definição de permissões por usuário

### Regras

* Dados devem ser isolados por tenant
* Usuários não podem acessar dados de outros tenants

---

## 1.3 Gestão de Alunos

O sistema deve permitir:

* Cadastro de alunos
* Atualização de dados do aluno
* Listagem de alunos por treinador
* Associação de aluno a treinador
* Definição de status (ativo/inativo)

### Regras

* Cada aluno pertence a um único treinador
* Dados devem respeitar o tenant do treinador

---

## 1.4 Gestão de Treinos (Core Feature)

O sistema deve permitir:

* Criação de treinos
* Edição de treinos (antes da execução)
* Associação de treinos a alunos
* Adição de exercícios aos treinos
* Duplicação de treinos
* Registro de execução do treino
* Visualização de histórico de treinos

### Regras

* Um treino pertence a um aluno
* Treinos executados não podem ser alterados (imutabilidade)
* Exercícios podem ser reutilizados

---

## 1.5 Exercícios

O sistema deve permitir:

* Cadastro de exercícios
* Reutilização em múltiplos treinos
* Classificação por grupo muscular

---

## 1.6 Planos e Assinaturas

O sistema deve permitir:

* Visualização de planos disponíveis
* Assinatura de planos pagos
* Upgrade e downgrade de plano
* Controle de limites por plano

### Regras

* Plano define limites (ex: número de alunos)
* Apenas admins podem alterar plano
* Limites devem ser validados antes de ações

---

## 1.7 Notificações (Opcional no MVP)

O sistema pode permitir:

* Envio de e-mails (convites, alertas)
* Notificação de eventos importantes

---

## 2. Requisitos Não Funcionais

## 2.1 Segurança

* Todas as requisições devem ser autenticadas (exceto públicas)
* Dados devem ser protegidos via HTTPS
* Inputs devem ser validados
* Proteção contra:

  * SQL Injection
  * acesso indevido
* Dados sensíveis não devem ser expostos

---

## 2.2 Escalabilidade

* Sistema deve suportar crescimento horizontal
* Arquitetura deve ser stateless
* Deve suportar múltiplos tenants simultaneamente
* Banco deve suportar aumento de volume de dados

---

## 2.3 Performance

* Tempo de resposta médio inferior a 500ms
* Uso de paginação em listagens
* Evitar N+1 queries
* Otimização de consultas ao banco

---

## 2.4 Disponibilidade

* Sistema deve ter alta disponibilidade
* Falhas devem ser tratadas sem derrubar o sistema
* Integrações externas não devem comprometer o core

---

## 2.5 Confiabilidade

* Operações críticas devem ser consistentes
* Dados não devem ser perdidos
* Logs devem registrar erros e eventos relevantes

---

## 2.6 Manutenibilidade

* Código deve ser organizado e desacoplado
* Arquitetura deve permitir evolução
* Refatoração deve ser possível sem impacto elevado

---

## 2.7 Observabilidade

* Logs estruturados obrigatórios
* Monitoramento de erros
* Métricas básicas:

  * requisições
  * erros
  * tempo de resposta

---

## 2.8 Testabilidade

* Código deve permitir testes unitários
* Regras de negócio devem ser testáveis isoladamente
* Integrações devem ser mockáveis

---

## 2.9 Usabilidade

* API deve ser consistente e previsível
* Mensagens de erro devem ser claras
* Fluxos devem ser simples para o usuário final

---

## 2.10 Deploy e Infraestrutura

* Sistema deve ser containerizado (Docker)
* Deploy automatizado (CI/CD)
* Configuração via variáveis de ambiente
* Suporte a múltiplos ambientes (dev, staging, prod)

---

## 2.11 Compatibilidade

* API deve manter compatibilidade entre versões
* Alterações breaking devem ser controladas

---

## 3. Critérios de Aceitação

Uma funcionalidade só é considerada válida quando:

* Atende aos requisitos funcionais definidos
* Não viola requisitos não funcionais
* Está segura e validada
* Está alinhada com arquitetura
* Está testada minimamente

---

## 4. Anti-padrões (Proibidos)

* Implementar funcionalidade sem requisito definido
* Ignorar requisitos de segurança
* Criar soluções não escaláveis
* Não validar limites de plano
* Misturar regra de negócio com infraestrutura

---

## 5. Regra Final

Se uma funcionalidade atende ao requisito funcional, mas viola um requisito não funcional (especialmente segurança ou escalabilidade), ela não deve ser considerada válida.

Requisitos não funcionais são obrigatórios, não opcionais.