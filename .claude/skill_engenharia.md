## Skill: Engenharia — Exemplo Real (Forzion)

---

## 1. Módulos e Funcionalidades

### Usuários / Auth

* Cadastro de usuário (email + senha)
* Login com JWT (via Supabase)
* Recuperação de senha
* Atualização de perfil (nome, foto, bio)
* Controle de acesso por roles:

  * Admin (dono da conta)
  * Trainer (profissional)

---

### Tenants / Organizações

* Criação automática de tenant ao registrar
* Convite de novos treinadores por e-mail
* Gestão de membros do tenant
* Definição de permissões por usuário
* Isolamento total de dados por tenant

---

### Alunos

* Cadastro de aluno:

  * nome
  * email (opcional)
  * telefone
* Associação com treinador
* Status (ativo/inativo)
* Histórico de acompanhamento

---

### Core Feature — Gestão de Treinos

* Criação de treino:

  * nome (ex: “Treino A - Hipertrofia”)
  * objetivo (hipertrofia, força, etc.)
* Adição de exercícios ao treino:

  * séries
  * repetições
  * carga
  * descanso
* Biblioteca de exercícios (reutilizável)
* Duplicação de treinos
* Versionamento de treino (histórico)
* Associação de treino a aluno
* Registro de execução (log do aluno)

---

### Planos / Assinaturas

* Plano Free:

  * até 5 alunos
* Plano Pro:

  * alunos ilimitados
* Controle de limites por plano
* Integração com Stripe:

  * cobrança recorrente
  * upgrade/downgrade

---

## 2. Entidades Principais

```
Usuario:
- id
- nome
- email
- role (Admin, Trainer)
- tenantId
- createdAt

Tenant:
- id
- nome
- slug
- planoId
- createdAt

Aluno:
- id
- nome
- email
- telefone
- status
- tenantId
- treinadorId

Treino:
- id
- nome
- objetivo
- alunoId
- tenantId
- createdAt

Exercicio:
- id
- nome
- grupoMuscular
- descricao

TreinoExercicio:
- id
- treinoId
- exercicioId
- series
- repeticoes
- carga
- descanso

Plano:
- id
- nome
- preco
- limiteAlunos

Assinatura:
- id
- tenantId
- planoId
- status
- dataExpiracao
```

---

## 3. Regras de Negócio

* Um usuário pertence a apenas um tenant
* Um tenant pode ter múltiplos usuários
* Um treinador só pode acessar dados do seu tenant
* Um aluno pertence a um treinador
* Um treino pertence a um aluno
* Um treino não pode ser editado após execução (imutabilidade)
* Planos limitam quantidade de alunos
* Apenas admins podem gerenciar assinatura
* Exercícios podem ser reutilizados entre treinos

---

## 4. Rotas / Endpoints Esperados

```
POST   /auth/register
POST   /auth/login
GET    /users/me

POST   /tenants
POST   /tenants/invite
GET    /tenants/members

POST   /alunos
GET    /alunos
GET    /alunos/{id}
PATCH  /alunos/{id}

POST   /treinos
GET    /treinos/{id}
POST   /treinos/{id}/exercicios
POST   /treinos/{id}/duplicar

POST   /planos/checkout
GET    /assinaturas
```

---

## 5. Integrações Externas

* Supabase Auth
  → autenticação e emissão de JWT

* Stripe
  → pagamentos e assinaturas

* SendGrid
  → envio de convites e notificações

* Cloud (AWS ou similar)
  → hospedagem da aplicação

---

## 6. Observações Técnicas

### Arquitetura

* Clean Architecture
* Separação em:

  * API
  * Application
  * Domain
  * Infrastructure

---

### Banco de Dados

* PostgreSQL (Supabase)
* Multi-tenant via tenantId
* Índices em:

  * tenantId
  * alunoId
  * treinoId

---

### ORM

* Entity Framework Core
* Uso de:

  * Include controlado
  * Paginação obrigatória

---

### Autenticação

* JWT via Supabase
* Middleware para validação de token
* Claims contendo tenantId

---

### Infraestrutura

* Docker para containerização
* Preparado para Kubernetes
* Variáveis de ambiente para configuração

---

### CI/CD

* GitHub Actions
* Pipeline:

  * build
  * testes
  * validação

---

### Observabilidade

* Logs estruturados
* Monitoramento de erros
* Métricas:

  * número de requisições
  * tempo de resposta
  * falhas

---

## Regra Final

O sistema deve ser construído de forma que permita crescer de dezenas para milhares de usuários sem necessidade de reescrita estrutural.