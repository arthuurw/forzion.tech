# GEMINI.md - Diretrizes e Documentação do Projeto

Este arquivo consolida todas as diretrizes, regras e documentações técnicas do projeto forzion.tech, integrando as instruções do CLAUDE.md, as skills definidas e a documentação técnica.

---

## 1. Visão Geral e Comandos (CLAUDE.md)

Backend em ASP.NET Core 8.0 Web API para forzion.tech. Módulo de usuários implementado com testes. O banco de dados é PostgreSQL hospedado no Supabase.

### Comandos Principais

```bash
# Rodar a API (HTTP na porta 5230, HTTPS na porta 7220)
dotnet run --project forzion.tech.Api

# Build
dotnet build

# Rodar testes (139 testes, 97.27% cobertura excluindo Infrastructure)
dotnet test forzion.tech.Tests

# Rodar testes com cobertura
dotnet test forzion.tech.Tests --collect:"XPlat Code Coverage" --settings forzion.tech.Tests/coverage.runsettings
```

### Arquitetura (Resumo)
- **Padrão**: Clean Architecture (Api, Application, Domain, Infrastructure)
- **Framework**: ASP.NET Core 8.0, C# com nullable reference types
- **ORM**: Entity Framework Core + PostgreSQL via Supabase
- **Auth**: Supabase Auth (validação de JWT em middleware)

---

## 2. Regras de Ouro

* Valide todos os arquivos que contêm skills a procura de problemas/adaptações/melhorias/duplicidades.
* Estas são as regras de ouro do projeto backend. NUNCA podem ser quebradas.
* Caso o Agente verifique a necessidade de alteração/adaptação do documento atual, deve solicitar aprovação MANUAL.
* O Agente não pode tomar NENHUMA decisão que não esteja de acordo com as skills mapeadas.

### Segurança (Prioridade Máxima)
* É proibido expor segredos, tokens, credenciais ou qualquer informação sensível no código.
* Toda entrada de dados deve ser validada.
* Toda operação deve respeitar mecanismos adequados de autenticação e autorização.

### Qualidade e Confiabilidade
* Nenhuma funcionalidade deve ser considerada concluída sem validação mínima.
* Todo código deve ser legível, testável e desacoplado.
* Toda e qualquer alteração deve possuir testes.

---

## 3. Regras de Arquitetura (Clean Architecture)

### Estrutura Obrigatória
A aplicação segue o padrão de **Clean Architecture**:

1.  **API (Presentation Layer)**: Entrada e saída de dados, controllers/endpoints. Sem regras de negócio.
2.  **Application (Application Layer)**: Casos de uso, orquestração, validações de entrada.
3.  **Domain (Domain Layer)**: Regras de negócio puras, Entidades e Value Objects. Independente de frameworks.
4.  **Infrastructure (Infrastructure Layer)**: Acesso a banco, integrações externas, implementações de repositórios.

### Diretrizes de Dependência
* API → Application → Domain
* Infrastructure → Application / Domain
* **NUNCA**: Domain → Infrastructure ou Application → Acesso direto ao banco.

---

## 4. Engenharia e Domínio

### Entidades Principais
* **Usuario**: Id (Supabase UUID), Nome, Email (VO), Role (Admin/Trainer), Status, TenantId.
* **Tenant**: Id, Nome, Slug (VO), PlanoId.
* **Plano**: Id, Nome, Preço, Limite de Alunos, IsFree.

### Regras de Negócio
* Um usuário pertence a apenas um tenant.
* Um treinador só acessa dados do seu tenant.
* Planos limitam a quantidade de alunos (Free: 5, Pro: Ilimitado).
* Apenas admins gerenciam assinaturas.

---

## 5. Requisitos Funcionais e Não Funcionais

### Funcionais
* Cadastro/Login via Supabase (JWT).
* Gestão de Tenants e isolamento de dados.
* Gestão de Alunos e Treinos (Core).
* Imutabilidade de treinos após execução.

### Não Funcionais
* Performance: Tempo de resposta < 500ms.
* Escalabilidade: Arquitetura stateless e suporte a múltiplos tenants.
* Observabilidade: Logs estruturados obrigatórios.
* Testabilidade: Regras de negócio testáveis isoladamente.

---

## 6. Padrões de Desenvolvimento

### Code Review
* Foco na qualidade do sistema, correção lógica e segurança.
* Validação de arquitetura e cobertura de testes.
* Feedbacks obrigatórios para falhas de segurança e erros de lógica.

### Pull Requests
* Título claro e descrição detalhada (O que? Por que?).
* Evidências de testes e checklist obrigatório.
* PRs pequenas e focadas (Single Purpose).

---

## 7. Documentação Técnica

### Banco de Dados (PostgreSQL + EF Core)
* **Convenção**: snake_case.
* **Isolamento**: Via schemas PostgreSQL (Public para Prod, Homolog para Staging).
* **Multi-tenancy**: Coluna `tenant_id` em todas as tabelas de negócio.
* **Migrations**: Devem ser aplicadas em ambos os schemas. Ownership é crítico (admin para public).

### Implementação Atual
* **Minimal API** com MapGroup.
* **Value Objects**: Email e Slug com validações e normalização.
* **Exceptions**: Herdam de `DomainException`, mapeadas para status codes (404, 409, 422, etc).
* **Testes**: xUnit, Moq, FluentAssertions. Cobertura de ~97% (excluíndo infra).

### Segredos e Configuração
* **Mecanismo**: .NET User Secrets (Local/Homolog) e Variáveis de Ambiente (Prod).
* **Prioridade**: Variáveis de Ambiente > User Secrets > appsettings.json.
* **Não Commitar**: Credenciais reais em arquivos de configuração.

---

## 8. Definition of Done (DoD)

Uma funcionalidade só é considerada concluída quando:
1. Atende ao requisito definido.
2. Código revisado e aprovado.
3. Testes realizados (unitários e integração).
4. Logs e tratamento de erros implementados.
5. Sem segredos expostos.
6. Pronta para deploy sem ajustes adicionais.

---

**Regra Final**: Em caso de dúvida, priorize **Segurança > Escalabilidade > Simplicidade**.
Toda alteração deve ser verificada empiricamente através dos testes existentes e novos testes para a funcionalidade.
