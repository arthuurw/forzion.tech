# Plano de Refatoração do Domínio — forzion.tech

## Contexto

O modelo anterior foi construído em torno de "Tenant/Academia" como unidade de isolamento multi-tenant. O modelo correto não possui entidade Academia — o isolamento é feito pelo próprio **Treinador**. Toda a arquitetura de domínio, auth e API deve ser reconstruída a partir deste plano.

---

## Decisões Arquiteturais

| Decisão | Escolha |
|---|---|
| Auth | JWT próprio (BCrypt + HMAC-SHA256). Sem Supabase Auth. |
| Senha | BCrypt no banco. Sem OAuth providers no MVP. |
| Isolamento | `TreinadorId` nos dados; sem `TenantId` nos dados de negócio |
| Schemas | `homolog` e `public` (prod) continuam separados via `Database:Schema` config |
| MaxAlunos | Limite hard — sistema bloqueia ao atingir o máximo do `PlanoTreinador` |
| Limite de PacoteAluno | Sem limite — treinador cria quantos pacotes quiser |
| Limite de fichas | Hard — `PacoteAluno.MaxFichas` é validado ao vincular ficha ao aluno |
| Exercícios | Biblioteca global (admin) + biblioteca própria por treinador (copia ou cria) |

---

## Modelo de Domínio

### Autenticação

```
Conta
  Id              Guid
  Email           string (único)
  PasswordHash    string (BCrypt)
  TipoConta       enum: SystemAdmin | Treinador | Aluno
  CreatedAt       DateTime
  UpdatedAt       DateTime?
```

### Usuários

```
SystemUser
  Id              Guid
  ContaId         → Conta
  Nome            string
  SystemRole      enum: SuperAdmin | Support | Operator
  Status          enum: Ativo | Inativo

Treinador
  Id              Guid
  ContaId         → Conta
  Nome            string
  PlanoTreinadorId → PlanoTreinador
  Status          enum: AguardandoAprovacao | Ativo | Inativo
  AprovadoPorId   → SystemUser (nullable)
  AprovadoEm      DateTime? (nullable)
  CreatedAt       DateTime

Aluno
  Id              Guid
  ContaId         → Conta
  Nome            string
  Status          enum: AguardandoAprovacao | Ativo | Inativo
  CreatedAt       DateTime
```

### Vínculo Treinador ↔ Aluno

```
VinculoTreinadorAluno
  Id              Guid
  TreinadorId     → Treinador
  AlunoId         → Aluno
  PacoteAlunoId   → PacoteAluno (null até aprovação; treinador escolhe ao aprovar)
  Status          enum: AguardandoAprovacao | Ativo | Inativo
  AprovadoPorId   → Treinador (nullable)
  AprovadoEm      DateTime? (nullable)
  DataInicio      DateTime? (preenchida na aprovação)
  DataFim         DateTime? (preenchida na inativação)
  CreatedAt       DateTime

Regras:
- Aluno só pode ter 1 VinculoTreinadorAluno com Status = Ativo por vez
- Inativar Treinador → inativa todos os vínculos ativos daquele treinador (cascade)
- Inativar vínculo → inativa todos os TreinoAluno daquele par (cascade)
```

### Planos

```
PlanoTreinador  ← global, gerenciado por admins
  Id            Guid
  Nome          string  (ex.: "Starter – 5 alunos")
  MaxAlunos     int     (limite hard)
  Preco         decimal
  IsAtivo       bool
  CreatedAt     DateTime

PlanConfigurationTreinador  ← versionamento do PlanoTreinador
  Id            Guid
  PlanoId       → PlanoTreinador
  MaxAlunos     int
  ValidFrom     DateTime
  ValidTo       DateTime? (null = vigente)
  CriadoPor     → SystemUser

PacoteAluno  ← por treinador, treinador cria livremente (sem limite de quantidade)
  Id            Guid
  TreinadorId   → Treinador
  Nome          string  (ex.: "Treino ABC – 3 fichas")
  MaxFichas     int     (limite hard ao vincular ficha ao aluno)
  Preco         decimal
  IsAtivo       bool
  CreatedAt     DateTime
```

### Exercícios

```
Exercicio
  Id              Guid
  TreinadorId     → Treinador (null = biblioteca global, admin gerencia)
  Nome            string
  GrupoMuscular   enum
  Descricao       string?
  CreatedAt       DateTime

Regras:
- TreinadorId null → exercício global (admin cria/edita)
- Treinador pode copiar exercício global para sua biblioteca (novo registro com TreinadorId)
- Treinador só enxerga seus próprios exercícios + os globais
```

### Treinos (Fichas de Treino)

```
Treino
  Id            Guid
  TreinadorId   → Treinador
  Nome          string
  Objetivo      enum: Hipertrofia | Forca | Resistencia | Emagrecimento | Reabilitacao | Outro
  Status        enum: Ativo | Inativo
  CreatedAt     DateTime
  UpdatedAt     DateTime?

TreinoExercicio  ← composição da ficha (valores PREVISTOS)
  Id            Guid
  TreinoId      → Treino
  ExercicioId   → Exercicio
  Series        int
  Repeticoes    int
  Carga         decimal?
  Descanso      int? (segundos)
  Ordem         int

TreinoAluno  ← ficha vinculada a um aluno
  Id            Guid
  TreinoId      → Treino
  AlunoId       → Aluno
  Status        enum: Ativo | Inativo
  CreatedAt     DateTime
  UpdatedAt     DateTime?

Regras:
- MaxFichas do PacoteAluno é validado ao criar TreinoAluno
- TreinoAluno inativado quando VinculoTreinadorAluno é inativado (cascade)
- Treino executado não pode ser editado (existência de ExecucaoTreino = imutável)
```

### Execuções

```
ExecucaoTreino  ← log do aluno
  Id              Guid
  TreinoId        → Treino
  AlunoId         → Aluno
  DataExecucao    DateTime
  Observacao      string?
  CreatedAt       DateTime

ExecucaoExercicio  ← valores EXECUTADOS (previsto vem do TreinoExercicio)
  Id                    Guid
  ExecucaoTreinoId      → ExecucaoTreino
  TreinoExercicioId     → TreinoExercicio
  SeriesExecutadas      int
  RepeticoesExecutadas  int
  CargaExecutada        decimal?
  Observacao            string?
```

### Auditoria

```
LogAprovacao
  Id              Guid
  TipoAcao        enum: AprovacaoTreinador | ReprovacaoTreinador | InativacaoTreinador
                        | AprovacaoVinculo | ReprovacaoVinculo | InativacaoVinculo
                        | AtribuicaoPlanTreinador
  RealizadoPorId  Guid  (ContaId de quem executou a ação)
  EntidadeId      Guid  (Id da entidade afetada)
  EntidadeTipo    string
  Observacao      string?
  CreatedAt       DateTime
```

---

## Fluxos Principais

### Cadastro de Treinador
1. `POST /auth/register/treinador` → cria `Conta` (TipoConta = Treinador) + `Treinador` (Status = AguardandoAprovacao)
2. Admin aprova: `POST /admin/treinadores/{id}/aprovar` → Status = Ativo + grava `LogAprovacao` (quem, quando)
3. Admin atribui plano: `PATCH /admin/treinadores/{id}/plano`

### Cadastro de Aluno
1. `POST /auth/register/aluno` → cria `Conta` (TipoConta = Aluno) + `Aluno` (Status = AguardandoAprovacao) + `VinculoTreinadorAluno` (Status = AguardandoAprovacao, TreinadorId informado)
2. Treinador aprova: `POST /treinador/vinculos/{id}/aprovar` → escolhe `PacoteAlunoId` → Status = Ativo + valida MaxAlunos do PlanoTreinador + grava log

### Inativação de Treinador (admin)
1. `POST /admin/treinadores/{id}/inativar`
2. Treinador.Status = Inativo
3. Cascade: todos VinculoTreinadorAluno ativos → Inativo (DataFim = now)
4. Cascade: todos TreinoAluno dos vínculos afetados → Inativo
5. Grava LogAprovacao

### Vincular Ficha a Aluno (treinador)
1. Valida: VinculoTreinadorAluno ativo entre treinador e aluno
2. Valida: quantidade de TreinoAluno ativos do aluno < PacoteAluno.MaxFichas
3. Cria TreinoAluno (Status = Ativo)

---

## Autorização por TipoConta

| Endpoint | SystemAdmin | Treinador | Aluno |
|---|---|---|---|
| `/admin/*` | ✅ | ❌ | ❌ |
| `/treinador/alunos` | ❌ | ✅ (próprios) | ❌ |
| `/treinador/treinos` | ❌ | ✅ (próprios) | ❌ |
| `/treinador/vinculos/{id}/aprovar` | ❌ | ✅ | ❌ |
| `/aluno/execucoes` | ❌ | ❌ | ✅ (próprias) |
| `/aluno/fichas` | ❌ | ❌ | ✅ (próprias) |

---

## Entidades Removidas

- `Tenant` — removida, não existe mais
- `Usuario` — substituída por `Conta` + `Treinador`
- `TenantRole` — removida
- `Role` — removida
- `SupabaseId` em todas as entidades — removido

---

## Plano de Implementação

### Fase 1 — Auth (base de tudo)
- **T1** ✅ `Conta`: entidade, BCrypt hash, validações
- **T2** ✅ `JwtService`: geração de token (HMAC-SHA256), claims: `conta_id`, `tipo_conta`, `perfil_id`
- **T3** `POST /auth/login`: valida email+senha, retorna JWT
- **T4** Middleware de autenticação: substituir validação Supabase pelo JWT próprio
- **T5** `IUserContext`: extrair `ContaId`, `TipoConta`, `PerfilId` do token

### Fase 2 — Domain: entidades base
- **T6** `SystemUser` vinculado a `Conta`
- **T7** `Treinador`: ContaId, PlanoTreinadorId, Status, AprovadoPor
- **T8** `Aluno`: ContaId, Status
- **T9** `VinculoTreinadorAluno`: com flags de aprovação e cascade
- **T10** `PlanoTreinador` + `PlanConfigurationTreinador`
- **T11** `PacoteAluno`
- **T12** `LogAprovacao`
- **T13** `Exercicio`: TreinadorId nullable (global quando null)
- **T14** Refatorar `Treino`, `ExecucaoTreino`: remover TenantId
- **T15** Remover `Tenant`, `Usuario`, `TenantRole`, `Role`

### Fase 3 — Infrastructure
- **T16** Configurações EF para todas as novas entidades
- **T17** Remover `TenantConfiguration`, `UsuarioConfiguration`
- **T18** Atualizar `AppDbContext`
- **T19** Migration completa
- **T20** Seed: primeira `Conta` + `SystemUser` (SuperAdmin) com senha segura

### Fase 4 — Application: serviços de limite
- **T21** `LimiteTreinadorService`: valida MaxAlunos do PlanoTreinador ao aprovar vínculo
- **T22** `LimiteFichasService`: valida MaxFichas do PacoteAluno ao vincular TreinoAluno

### Fase 5 — Application: use cases de auth e usuários
- **T23** `RegistrarTreinador` (auto-cadastro)
- **T24** `AprovarTreinador` (admin) + log
- **T25** `InativarTreinador` (admin) + cascade + log
- **T26** `AtribuirPlanoAoTreinador` (admin)
- **T27** `RegistrarAluno` + cria VinculoTreinadorAluno (AguardandoAprovacao)
- **T28** `AprovarVinculoAluno` (treinador) + valida limite + log
- **T29** `DesvincularAluno` (treinador ou admin) + cascade TreinoAluno

### Fase 6 — Application: use cases de treino e execução
- **T30** Atualizar handlers de `Treino`/`Exercicio`: remover tenant, usar TreinadorId
- **T31** `VincularFichaAoAluno`: valida MaxFichas do PacoteAluno
- **T32** `RegistrarExecucao`: caller é o Aluno (via IUserContext)
- **T33** `CopiarExercicioGlobal`: copia exercício global para biblioteca do treinador

### Fase 7 — API
- **T34** `POST /auth/login`, `POST /auth/register/treinador`, `POST /auth/register/aluno`
- **T35** Admin: `/admin/treinadores/{id}/aprovar|inativar`, `/admin/planos`, `/admin/treinadores/{id}/plano`
- **T36** Treinador: `/treinador/vinculos/{id}/aprovar`, `/treinador/alunos`, `/treinador/treinos`, `/treinador/exercicios`, `/treinador/pacotes`
- **T37** Aluno: `/aluno/fichas`, `/aluno/execucoes`
- **T38** Global (leitura): `GET /planos/treinador`, `GET /treinadores/{id}/pacotes` (para aluno ver no cadastro)

### Fase 8 — Testes
- **T39** Domain: `Conta`, `Treinador`, `Aluno`, `VinculoTreinadorAluno`, `LogAprovacao`, `PacoteAluno`
- **T40** Application: aprovação, inativação, cascades, limites
- **T41** Integration: fluxo completo cadastro → aprovação → vínculo → ficha → execução

---

## Estado da Implementação

### Concluído

#### Limpeza do modelo anterior
- Removidas entidades: `Tenant`, `Usuario`, `TenantRole`, `Role`
- Removidas exceções: `UsuarioInativoException`, `UsuarioJaRegistradoException`, `UsuarioNaoEncontradoException`
- Removidas interfaces: `ITenantContext`, `ITenantRepository`, `IUsuarioRepository`, `IPlanoLimitService`
- Removidos handlers: todos os use cases de `Usuarios/` e `SystemAdministration/`
- Removidos endpoints: `Endpoints/Usuarios/`, `Endpoints/Administration/`
- Removidas migrations: histórico completo apagado (fresh migration será criada após Fase 2)
- Removidas configs EF: `TenantConfiguration`, `UsuarioConfiguration`, `TenantInterceptor`
- `AppDbContext`: removidos `DbSet<Tenant>` e `DbSet<Usuario>`, adicionado `DbSet<Conta>`
- `AuthenticationExtensions`: migrado de Supabase (`Auth:Authority`) para HMAC-SHA256 (`Auth:JwtSecret/JwtIssuer/JwtAudience`)
- `appsettings.json`: atualizado para nova estrutura `Auth:JwtSecret/JwtIssuer/JwtAudience`
- Todos os endpoints migrados de `ITenantContext` para `IUserContext`
- 230 testes passando após limpeza

#### Fase 1 — Auth
- **T1** ✅ `Conta`: entidade, `Email` value object, `TipoConta` enum, 8 testes
- **T2** ✅ `JwtService` (`Infrastructure/Services`): HMAC-SHA256, claims `conta_id`/`tipo_conta`/`perfil_id`, 8 testes
- **T3** ✅ `POST /auth/login`: `LoginHandler`, `IContaRepository`, `ContaRepository`, `IPasswordHasher`, `BcryptPasswordHasher`, endpoint, 8 testes
- **T4** ✅ (parcial) `AuthenticationExtensions`: valida JWT próprio com chave simétrica; fallback para testes
- **T5** ✅ (stub) `IUserContext` + `HttpUserContext`: extrai claims do token; perfilId será refinado na Fase 2

### Pendente

#### Fase 2 — Domain: entidades base
- **T6** `SystemUser` vinculado a `Conta`
- **T7** `Treinador`: ContaId, PlanoTreinadorId, Status, AprovadoPor
- **T8** `Aluno`: ContaId, Status (remover TenantId/TreinadorId do modelo atual)
- **T9** `VinculoTreinadorAluno`
- **T10** `PlanoTreinador` + `PlanConfigurationTreinador`
- **T11** `PacoteAluno`
- **T12** `LogAprovacao`
- **T13** `Exercicio`: TreinadorId nullable (global quando null)
- **T14** Refatorar `Treino`, `ExecucaoTreino`: remover TenantId
- **T15** Remover entidades legadas restantes (`Plano`, `PlanConfiguration`, `SystemUser` antigo)

#### Fase 3 — Infrastructure
- **T16** Configurações EF para todas as novas entidades
- **T17** Atualizar `AppDbContext` (remover DbSets legados)
- **T18** Migration completa (primeira do novo modelo)
- **T19** Seed: primeira `Conta` + `SystemUser` (SuperAdmin)

#### Fase 4 — Application: serviços de limite
- **T20** `LimiteTreinadorService`: valida MaxAlunos do PlanoTreinador ao aprovar vínculo
- **T21** `LimiteFichasService`: valida MaxFichas do PacoteAluno ao vincular TreinoAluno

#### Fase 5 — Application: use cases de auth e usuários
- **T22** `RegistrarTreinador` (auto-cadastro)
- **T23** `AprovarTreinador` (admin) + log
- **T24** `InativarTreinador` (admin) + cascade + log
- **T25** `AtribuirPlanoAoTreinador` (admin)
- **T26** `RegistrarAluno` + cria VinculoTreinadorAluno (AguardandoAprovacao)
- **T27** `AprovarVinculoAluno` (treinador) + valida limite + log
- **T28** `DesvincularAluno` (treinador ou admin) + cascade TreinoAluno

#### Fase 6 — Application: use cases de treino e execução
- **T29** Atualizar handlers de `Treino`/`Exercicio`: remover tenant, usar TreinadorId
- **T30** `VincularFichaAoAluno`: valida MaxFichas do PacoteAluno
- **T31** `RegistrarExecucao`: caller é o Aluno (via IUserContext)
- **T32** `CopiarExercicioGlobal`: copia exercício global para biblioteca do treinador

#### Fase 7 — API
- **T33** `POST /auth/register/treinador`, `POST /auth/register/aluno`
- **T34** Admin: `/admin/treinadores/{id}/aprovar|inativar`, `/admin/planos`, `/admin/treinadores/{id}/plano`
- **T35** Treinador: `/treinador/vinculos/{id}/aprovar`, `/treinador/alunos`, `/treinador/treinos`, `/treinador/exercicios`, `/treinador/pacotes`
- **T36** Aluno: `/aluno/fichas`, `/aluno/execucoes`

#### Fase 8 — Testes
- **T37** Domain: `Treinador`, `Aluno` (refatorado), `VinculoTreinadorAluno`, `LogAprovacao`, `PacoteAluno`
- **T38** Application: aprovação, inativação, cascades, limites
- **T39** Integration: fluxo completo cadastro → aprovação → vínculo → ficha → execução
