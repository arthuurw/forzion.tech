# secrets-e-configuracao.md
atualizado: 2026-04-15

## princípio
Nenhuma credencial em código ou arquivos commitados. Dois ambientes: Homolog e Production. Development foi removido.

ambiente → ASPNETCORE_ENVIRONMENT → mecanismo de secrets
- Local → Homolog → .NET User Secrets (%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json)
- Homologação → Homolog → variáveis de ambiente
- Produção → Production → variáveis de ambiente

User Secrets carregados com optional:true — não falha em servidores onde o arquivo não existe. Variáveis de ambiente sempre têm prioridade.

## secrets necessários
sensíveis:
- ConnectionStrings:AppConnection — conn string PostgreSQL usuário forzion_api
- ConnectionStrings:DefaultConnection — conn string admin postgres (migrations manuais, não usado em runtime)
- Auth:Authority — URL base Supabase Auth (issuer JWT)
- Supabase:Url — mesma que Auth:Authority (mantidos separados por clareza)
- Supabase:JwtSecret — reservado, não consumido por nenhum código atual

não-sensíveis (podem estar em appsettings.json):
- Auth:Audience — "authenticated" (valor fixo Supabase)
- Database:Schema — "public" (padrão)

## User Secrets — setup local
UserSecretsId: 049d65fb-2c12-483c-b56e-cb753632d11f (em forzion.tech.Api.csproj)
Carregados explicitamente em Program.cs: builder.Configuration.AddUserSecrets<Program>(optional:true) quando ASPNETCORE_ENVIRONMENT=Homolog

```bash
dotnet user-secrets set "ConnectionStrings:AppConnection" "Host=<host>;Database=postgres;Username=forzion_api;Password=<senha>;SSL Mode=Require;Trust Server Certificate=true;Search Path=public" --project forzion.tech.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=<host>;Database=postgres;Username=postgres;Password=<senha>;SSL Mode=Require;Trust Server Certificate=true" --project forzion.tech.Api
dotnet user-secrets set "Auth:Authority" "https://<project-ref>.supabase.co/auth/v1" --project forzion.tech.Api
dotnet user-secrets set "Auth:Audience" "authenticated" --project forzion.tech.Api
dotnet user-secrets set "Supabase:Url" "https://<project-ref>.supabase.co/auth/v1" --project forzion.tech.Api
dotnet user-secrets set "Supabase:JwtSecret" "<jwt-secret>" --project forzion.tech.Api
```

Valores reais nos User Secrets locais (não há mais anotacoes.txt).

```bash
# verificar
dotnet user-secrets list --project forzion.tech.Api
```

## variáveis de ambiente (homolog/prod)
Separador hierárquico: __ (duplo underscore)

```bash
ConnectionStrings__AppConnection="Host=...;Username=forzion_api;..."
Auth__Authority="https://<ref>.supabase.co/auth/v1"
Auth__Audience="authenticated"
Supabase__Url="https://<ref>.supabase.co/auth/v1"
Supabase__JwtSecret="<secret>"
Database__Schema="public"    # produção
Database__Schema="homolog"   # homologação
```

## prioridade de carregamento
1. appsettings.json (base, commitado)
2. appsettings.{Environment}.json (por ambiente, não commitado)
3. User Secrets (apenas Homolog local)
4. variáveis de ambiente (maior prioridade)

## arquivos commitados vs não commitados
- appsettings.json: commitado — AllowedHosts restrito a forzion.tech;*.forzion.tech
- appsettings.Homolog.json: NÃO commitado — deve conter AllowedHosts: "*" para dev local
- forzion.tech.Api.csproj: commitado (contém apenas UserSecretsId, não é segredo)
- anotacoes.txt: NÃO commitado, fonte da verdade local

.gitignore protege: appsettings.*.json (exceto appsettings.json)
appsettings.Development.json foi removido — projeto usa apenas Homolog e Production.

## AllowedHosts
appsettings.json (produção): "forzion.tech;*.forzion.tech" — rejeita Host headers de outros domínios
appsettings.Homolog.json (dev local, não commitado): "*" — permite qualquer host em desenvolvimento

## observações
- DefaultConnection (postgres admin) não é usado pelo runtime da API — apenas para migrations manuais e RLS via psql
- Supabase:JwtSecret não é consumido por nenhum código atual — reservado para uso futuro
- Auth:Authority e Supabase:Url têm o mesmo valor — separados por clareza (JWT middleware vs SDK Supabase)
