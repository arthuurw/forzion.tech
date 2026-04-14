# banco-de-dados.md
atualizado: 2026-04-14

## stack
SGBD: PostgreSQL (Supabase) | ORM: EF Core 8.0 | Driver: Npgsql 8.0.11
Convenção: snake_case (EFCore.NamingConventions)
Config keys: ConnectionStrings:AppConnection | Database:Schema

## isolamento por schema
Banco compartilhado entre ambientes. Isolamento via schemas PostgreSQL. Cada schema tem suas próprias tabelas e própria __EFMigrationsHistory.

ambiente → schema → appsettings usado
- Produção → public → appsettings.json
- Homolog → homolog → appsettings.Homolog.json

AppDbContext recebe schema no construtor, aplica HasDefaultSchema(schema). Toda query qualifica tabelas com schema.
InfrastructureExtensions lê Database:Schema e instancia AppDbContext via AddScoped com factory (não usa AddDbContext).

## permissões de migration — CRÍTICO
- schema homolog: usar forzion_api (tem ownership das tabelas)
- schema public: OBRIGATÓRIO usar postgres admin (DefaultConnection)
  Motivo: tabelas em public foram criadas pelo admin. forzion_api não tem ownership → erro 42501: must be owner of table

## rodar migrations
```bash
# Homolog
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef database update --project forzion.tech.Infrastructure --startup-project forzion.tech.Api

# Produção (usar DefaultConnection com postgres)
ASPNETCORE_ENVIRONMENT=Production ConnectionStrings__AppConnection="Host=<host>;Database=postgres;Username=postgres;Password=<senha>;SSL Mode=Require;Trust Server Certificate=true;Search Path=public" Database__Schema="public" dotnet ef database update --project forzion.tech.Infrastructure --startup-project forzion.tech.Api
```

## setup schema homolog (executar uma vez no Supabase SQL Editor)
```sql
CREATE SCHEMA IF NOT EXISTS homolog;
GRANT USAGE  ON SCHEMA homolog TO forzion_api;
GRANT CREATE ON SCHEMA homolog TO forzion_api;
```

## AppDbContextFactory (design-time)
Usado apenas pelo CLI do EF. Lê config na ordem: appsettings.json → appsettings.{ASPNETCORE_ENVIRONMENT}.json → User Secrets (049d65fb-2c12-483c-b56e-cb753632d11f) → env vars.
Aplica Database:Schema em dois lugares obrigatórios:
- HasDefaultSchema(schema) → qualifica tabelas de negócio
- MigrationsHistoryTable("__EFMigrationsHistory", schema) → qualifica tabela de controle
Sem MigrationsHistoryTable, EF Core falha em schema não-padrão.
Mesmo MigrationsHistoryTable configurado em InfrastructureExtensions (runtime).

## tabelas

### planos
Dados estáticos, populada via seed. Sem CreatedAt.
- id: uuid PK
- nome: varchar(100) NOT NULL
- preco: numeric(10,2) NOT NULL
- limite_alunos: integer NOT NULL
- is_free: boolean NOT NULL

seed fixo:
- 00000000-0000-0000-0000-000000000001 | Free | 0.00 | 5 | true
- 00000000-0000-0000-0000-000000000002 | Pro | 49.90 | 2147483647 | false

IDs em Domain/Constants/PlanoIds.cs: PlanoIds.FreeId / PlanoIds.ProId

### tenants
- id: uuid PK
- nome: varchar(100) NOT NULL
- slug: varchar(200) NOT NULL UNIQUE
- plano_id: uuid NOT NULL FK→planos(id) OnDelete:Restrict
- created_at: timestamptz NOT NULL
- updated_at: timestamptz NULL

índices: pk_tenants(id), ix_tenants_slug(slug) UNIQUE, ix_tenants_plano_id(plano_id)
mapeamento: slug usa HasConversion(s=>s.Value, v=>Slug.Reconstituir(v)) — tipo .NET: Slug VO

### usuarios
- id: uuid PK — igual ao UUID do Supabase Auth
- nome: varchar(100) NOT NULL
- email: varchar(256) NOT NULL UNIQUE
- role: text NOT NULL — valores: Admin | Trainer
- status: text NOT NULL — valores: Ativo | Inativo
- tenant_id: uuid NOT NULL FK→tenants(id) OnDelete:Restrict
- foto_url: varchar(500) NULL
- bio: varchar(500) NULL
- created_at: timestamptz NOT NULL
- updated_at: timestamptz NULL

índices: pk_usuarios(id), ix_usuarios_email(email) UNIQUE, ix_usuarios_tenant_id(tenant_id)
mapeamentos: email usa HasConversion(e=>e.Value, v=>Email.Reconstituir(v)) | role usa HasConversion<string>()

decisão: id do Usuario = UUID do Supabase Auth (sub claim do JWT). Sem camada de indireção.

## relacionamentos
planos ──< tenants ──< usuarios
Todos os deletes: Restrict (sem cascade)

## multi-tenancy
Estratégia: tenant_id por linha.
Camada 1 (app): ITenantContext expõe TenantId do claim tenant_id do JWT. Toda query de negócio deve filtrar por TenantId.
Camada 2 (banco, preparado mas não ativo): TenantInterceptor executa a cada conexão:
  SELECT set_config('app.current_tenant_id', @id, false)
RLS policies ainda NÃO foram criadas.

## value objects e conversões EF Core
Reconstituir bypassa validação (dado do banco é confiável). Criar/FromNome aplicam validações (dados externos).
- Usuario.Email: Email VO → varchar(256) | leitura: Email.Reconstituir(v) | escrita: e.Value
- Tenant.Slug: Slug VO → varchar(200) | leitura: Slug.Reconstituir(v) | escrita: s.Value

## histórico de migrations
- 20260413223047_InitialCreate (2026-04-13): cria planos, tenants, usuarios; índices; seed Free e Pro
- 20260413232230_AddPlanoIsFree (2026-04-13): adiciona planos.is_free; atualiza seed
- 20260414201833_EnriquecimentoDoDominio (2026-04-14): narrowing usuarios.nome(200→100), usuarios.email(300→256), tenants.nome(200→100); adiciona usuarios.foto_url, usuarios.bio, usuarios.updated_at, tenants.updated_at
- 20260414211515_AddUsuarioStatus (2026-04-14): adiciona usuarios.status (text NOT NULL, default 'Ativo')
- 20260414212447_FixUsuarioStatusDefault (2026-04-14): corrige linhas com status='' para 'Ativo' (geradas pela migration anterior com defaultValue errado)

## armadilha de schema nas migrations — CRÍTICO
Migrations são sempre geradas com ASPNETCORE_ENVIRONMENT=Homolog, que carrega os User Secrets com Database:Schema=homolog. O snapshot do EF Core fica com schema "homolog" hardcoded. Isso é esperado e correto — todas as migrations devem ser geradas com esse ambiente para manter consistência no snapshot.

NUNCA gerar uma migration com um ambiente diferente do anterior. Se o snapshot anterior usou "homolog" e a nova migration for gerada com "public", o EF Core detecta mudança de schema e gera RenameTable indevidos.

Se uma migration gerada contiver EnsureSchema ou RenameTable — remover imediatamente antes de aplicar. Esses comandos só devem existir em migrations que intencionalmente criam novos schemas.

## checklist obrigatório a cada nova migration
Toda migration DEVE ser aplicada nos dois schemas. Nunca deixar um schema desatualizado.

1. Gerar a migration (ASPNETCORE_ENVIRONMENT=Homolog para que o AppDbContextFactory use as credenciais corretas):
```bash
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef migrations add <Nome> --project forzion.tech.Infrastructure --startup-project forzion.tech.Api
```

2. Aplicar em homolog (forzion_api tem ownership):
```bash
ASPNETCORE_ENVIRONMENT=Homolog dotnet ef database update --project forzion.tech.Infrastructure --startup-project forzion.tech.Api
```

3. Aplicar em produção (OBRIGATÓRIO usar postgres admin — forzion_api não tem ownership em public):
```bash
ASPNETCORE_ENVIRONMENT=Production \
  ConnectionStrings__AppConnection="Host=<host>;Database=postgres;Username=postgres;Password=<senha>;SSL Mode=Require;Trust Server Certificate=true;Search Path=public" \
  Database__Schema="public" \
  dotnet ef database update --project forzion.tech.Infrastructure --startup-project forzion.tech.Api
```

## arquivos-chave
- Infrastructure/Persistence/AppDbContext.cs — DbContext principal; HasDefaultSchema
- Infrastructure/Persistence/AppDbContextFactory.cs — factory design-time para CLI EF
- Infrastructure/Persistence/Interceptors/TenantInterceptor.cs — seta app.current_tenant_id
- Infrastructure/Persistence/Configurations/UsuarioConfiguration.cs — Fluent API usuarios; conversão Email VO
- Infrastructure/Persistence/Configurations/TenantConfiguration.cs — Fluent API tenants; conversão Slug VO
- Infrastructure/Persistence/Configurations/PlanoConfiguration.cs — Fluent API planos; seed
- Infrastructure/DependencyInjection/InfrastructureExtensions.cs — DI; injeta schema

## o que ainda não existe
- RLS policies (interceptor preparado, policies não criadas)
- tabelas: alunos, treinos, exercicios, treino_exercicios, assinaturas
- índices em tenant_id para entidades futuras
- paginação
