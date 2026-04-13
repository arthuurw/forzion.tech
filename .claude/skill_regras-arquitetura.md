## Skill: Regras de Arquitetura

### Objetivo

Garantir que o sistema seja desenvolvido com uma arquitetura consistente, escalável e sustentável, evitando acoplamento excessivo, facilitando manutenção e permitindo evolução contínua do produto.

---

### Princípios Fundamentais

* Separação clara de responsabilidades
* Baixo acoplamento entre camadas
* Alta coesão dentro de cada módulo
* Independência do domínio em relação a frameworks
* Código orientado a regras de negócio, não a tecnologia
* Facilidade de testes e evolução

---

### Estrutura Obrigatória

A aplicação deve seguir obrigatoriamente o padrão de **Clean Architecture**, com as seguintes camadas:

#### API (Presentation Layer)

* Responsável pela entrada e saída de dados
* Contém controllers e endpoints
* Não deve conter regras de negócio

---

#### Application (Application Layer)

* Contém casos de uso e regras de aplicação
* Orquestra o fluxo entre domínio e infraestrutura
* Realiza validações de entrada
* Define interfaces para dependências externas

---

#### Domain (Domain Layer)

* Contém regras de negócio puras
* Entidades e Value Objects
* Não depende de nenhuma outra camada
* Não deve conter lógica de infraestrutura

---

#### Infrastructure (Infrastructure Layer)

* Implementação de acesso a banco de dados
* Integrações externas (APIs, serviços)
* Implementação de repositórios e serviços

---

### Diretrizes

* A camada **Domain** não pode depender de nenhuma outra camada
* A camada **Application** não pode depender diretamente da Infrastructure (usar interfaces)
* A camada **Infrastructure** depende das demais, nunca o contrário
* Controllers devem ser finos e delegar para a camada Application
* Nenhuma regra de negócio deve existir fora do Domain/Application
* Dependências devem ser invertidas (Dependency Inversion Principle)

---

### Boas Práticas Obrigatórias

* Uso de DTOs para entrada e saída de dados
* Validação centralizada na camada Application
* Uso de interfaces para desacoplamento
* Repositórios devem ser abstrações (interfaces no Application/Domain)
* Evitar lógica em controllers
* Evitar acesso direto ao banco fora da Infrastructure
* Métodos devem ter responsabilidade única
* Nomes devem ser claros e descritivos
* Evitar duplicação de código
* Utilizar injeção de dependência

---

### Organização de Código

* Estrutura por domínio/feature sempre que possível
* Evitar organização apenas por tipo técnico (ex: "Services", "Utils" genéricos)
* Cada módulo deve ser isolado e independente
* Código deve refletir o negócio, não a tecnologia

---

### Fluxo de Dependências

* API → Application → Domain
* Infrastructure → Application / Domain (implementações)

Nunca:

* Domain → Infrastructure
* Application → acesso direto ao banco

---

### Engenharia

#### Segurança

* Toda entrada deve ser validada
* Nenhuma informação sensível deve ser exposta
* Autenticação e autorização devem ser centralizadas
* Evitar lógica de segurança duplicada

---

#### Escalabilidade

* Preferir serviços stateless
* Evitar dependências rígidas
* Preparar arquitetura para crescimento (horizontal)
* Separar responsabilidades desde o início

---

#### Performance

* Evitar consultas desnecessárias ao banco
* Prevenir N+1 queries
* Evitar processamento pesado na camada errada
* Utilizar paginação quando necessário

---

#### Manutenibilidade

* Código deve ser fácil de entender e modificar
* Evitar complexidade desnecessária
* Refatoração contínua é permitida e incentivada
* Estrutura deve suportar crescimento do sistema

---

#### Testabilidade

* Código deve permitir testes unitários
* Regras de negócio devem ser testáveis isoladamente
* Evitar dependências diretas em infraestrutura

---

### Anti-padrões (Proibidos)

* Lógica de negócio em controllers
* Acesso direto ao banco fora da Infrastructure
* Dependência da Domain em frameworks
* Classes com múltiplas responsabilidades
* Código duplicado
* Uso excessivo de lógica em camada errada
* Acoplamento forte entre módulos

---

### Critérios de Validação Arquitetural

Uma implementação só é válida se:

* Respeita a separação de camadas
* Não viola regras de dependência
* Está alinhada com Clean Architecture
* Mantém o sistema desacoplado
* Permite evolução futura sem reescrita

---

### Regra Final

Se uma implementação compromete a arquitetura, ela deve ser rejeitada, mesmo que funcione.

A arquitetura é um ativo estratégico do sistema e deve ser protegida continuamente.
