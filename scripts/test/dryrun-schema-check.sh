#!/usr/bin/env bash
# Repro (DEPLOY-01/02): prova que o migrate-dryrun.sh aplica migrations no schema CLONADO,
# não num schema vazio à parte. EF/Npgsql auto-cria o schema do MigrationsHistoryTable
# (InfrastructureExtensions.cs:106) antes da 1a migration — um Search Path divergente do
# clonado não aborta o migrate, ele silenciosamente reseeda um schema novo e vazio e nunca
# toca o dado real clonado (Gate A vira no-op "verde"). Sinal do delta: o log do DataSeeder
# ("Grupos musculares criados") só aparece quando reseedou do zero. Requer Docker.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

command -v docker >/dev/null || { echo "dryrun-schema-check: requer Docker." >&2; exit 1; }

WORK="$(mktemp -d)"
SRC="dryrun-schema-check-src"

cleanup() {
  docker rm -f "$SRC" >/dev/null 2>&1 || true
  docker compose -p forzion-dryrun -f docker-compose.dryrun.yml down -v --remove-orphans >/dev/null 2>&1 || true
  git checkout -- docker-compose.dryrun.yml 2>/dev/null || true
  rm -rf "$WORK"
}
trap cleanup EXIT

JWT_SECRET_VAL="$(openssl rand -base64 64 | tr -d '\n')"
MFA_KEY_VAL="$(openssl rand -base64 32 | tr -d '\n')"
DP_KEY_VAL="$(openssl rand -base64 32 | tr -d '\n')"
ADMIN_PW_VAL="Dryrun-Check-Adm1n-$$"

# 2 grafias no mesmo arquivo: JWT_SECRET/etc alimentam a INTERPOLAÇÃO do compose
# (docker-compose.dryrun.yml usa ${JWT_SECRET}); Auth__JwtSecret/etc são as chaves .NET reais,
# usadas no bootstrap direto (docker run) que não passa pelo compose.
cat > "$WORK/env" <<EOF
JWT_SECRET=$JWT_SECRET_VAL
JWT_ISSUER=dryrun-check.local
JWT_AUDIENCE=dryrun-check.local
MFA_ENCRYPTION_KEY=$MFA_KEY_VAL
DATA_PROTECTION_KEY=$DP_KEY_VAL
Auth__JwtSecret=$JWT_SECRET_VAL
Auth__JwtIssuer=dryrun-check.local
Auth__JwtAudience=dryrun-check.local
Mfa__EncryptionKey=$MFA_KEY_VAL
DataProtection__EncryptionKey=$DP_KEY_VAL
Seed__AdminPassword=$ADMIN_PW_VAL
EOF

# sslmode=require é hardcoded no pg_dump do migrate-dryrun.sh (espelha o pooler real) — o
# source efêmero precisa de TLS mesmo sendo throwaway.
openssl req -new -x509 -days 1 -nodes -subj "//CN=localhost" \
  -out "$WORK/server.crt" -keyout "$WORK/server.key" >/dev/null 2>&1
cat > "$WORK/enable-ssl.sh" <<'EOF'
set -e
cp /host-certs/server.crt /host-certs/server.key "$PGDATA/"
chmod 600 "$PGDATA/server.key"
{
  echo "ssl = on"
  echo "ssl_cert_file = 'server.crt'"
  echo "ssl_key_file = 'server.key'"
} >> "$PGDATA/postgresql.conf"
EOF

# Cria a rede do project ANTES do source subir nela, com os labels que o compose reconhece
# como "própria" (senão "up -d dryrun-db" recusa reusá-la).
docker network rm forzion-dryrun_default >/dev/null 2>&1 || true
docker network create \
  --label com.docker.compose.network=default \
  --label com.docker.compose.project=forzion-dryrun \
  --label com.docker.compose.version=2 \
  forzion-dryrun_default >/dev/null

echo "dryrun-schema-check: subindo source-db efêmero (simula o DB real)..."
docker rm -f "$SRC" >/dev/null 2>&1 || true
docker run -d --name "$SRC" --network forzion-dryrun_default \
  -e POSTGRES_USER=src -e POSTGRES_PASSWORD=src -e POSTGRES_DB=src \
  -v "$WORK:/host-certs:ro" \
  -v "$WORK/enable-ssl.sh:/docker-entrypoint-initdb.d/enable-ssl.sh:ro" \
  postgres:17-alpine >/dev/null

for _ in $(seq 1 30); do
  docker exec "$SRC" pg_isready -U src -d src >/dev/null 2>&1 && break
  sleep 1
done
docker exec "$SRC" pg_isready -U src -d src >/dev/null 2>&1 || { echo "dryrun-schema-check: source-db não ficou pronto." >&2; exit 1; }

docker compose -p forzion-dryrun -f docker-compose.dryrun.yml build migrate >/dev/null
IMG="forzion-dryrun-migrate:latest" # tag padrão do compose (project forzion-dryrun + serviço migrate)

# Bootstrap: 1 migrate real por schema no source, como um cutover já feito — o dry-run deve
# encontrar dado JÁ SEEDADO (não recriar do zero) quando aponta pro schema certo.
bootstrap_source() {
  local schema="$1"
  docker exec "$SRC" psql -U src -d src -v ON_ERROR_STOP=1 -q -c "CREATE SCHEMA IF NOT EXISTS \"$schema\";"
  docker run --rm --network forzion-dryrun_default \
    -e ASPNETCORE_ENVIRONMENT=Homolog \
    -e "ConnectionStrings__AppConnection=Host=$SRC;Port=5432;Database=src;Username=src;Password=src;Search Path=$schema" \
    --env-file "$WORK/env" \
    "$IMG" migrate >/dev/null
}

echo "dryrun-schema-check: bootstrap do source (public + homolog)..."
bootstrap_source public
bootstrap_source homolog

run_dryrun_capture() {
  local schema="$1"
  DB_CONNECTION="Host=$SRC;Port=5432;Database=src;Username=src;Password=src;Search Path=$schema" \
    ENV_FILE="$WORK/env" bash scripts/migrate-dryrun.sh 2>&1 || true
}

# Caso correto (schema-alvo == clonado): exige "OK" E ausência do reseed (dado real já lido).
assert_touched_real_data() {
  local schema="$1" out
  out="$(run_dryrun_capture "$schema")"
  echo "$out" | grep -q "OK — migrate aplicou limpo" || { echo "$out"; echo "dryrun-schema-check: migrate-dryrun.sh não terminou OK (schema=$schema)." >&2; exit 1; }
  echo "$out" | grep -q "Grupos musculares criados" && { echo "$out"; return 1; }
  return 0
}

# Caso quebrado (delta pré-fix): não exige "OK" (o schema fresh pode falhar por outro motivo
# a jusante, ex. seed de admin) — só prova que reseedou do zero (ignorou o clone real).
assert_ignored_real_data() {
  local schema="$1" out
  out="$(run_dryrun_capture "$schema")"
  echo "$out" | grep -q "Grupos musculares criados" || { echo "$out"; return 1; }
  return 0
}

echo "=== caso: schema=public (schema-alvo == schema clonado) ==="
assert_touched_real_data public || { echo "dryrun-schema-check: FALHOU — migrate reseedou do zero em vez de usar o schema 'public' clonado." >&2; exit 1; }
echo "OK — migrate operou sobre o dado REAL clonado do schema 'public'."

echo "=== caso: schema=homolog (não regride) ==="
assert_touched_real_data homolog || { echo "dryrun-schema-check: FALHOU — homolog regrediu (reseed do zero)." >&2; exit 1; }
echo "OK — migrate operou sobre o dado REAL clonado do schema 'homolog'."

echo "=== delta: compose PRÉ-fix (Search Path=homolog hardcoded) com schema=public ==="
git show HEAD~1:docker-compose.dryrun.yml > docker-compose.dryrun.yml
assert_ignored_real_data public || {
  echo "dryrun-schema-check: FALHOU — pré-fix deveria ignorar o clone de 'public' e reseedar um schema vazio (delta não observado)." >&2
  git checkout -- docker-compose.dryrun.yml
  exit 1
}
echo "OK — pré-fix confirmado: reseedou um schema vazio (Search Path hardcoded), nunca leu o dado clonado de 'public'."
git checkout -- docker-compose.dryrun.yml

echo "dryrun-schema-check: OK — dry-run aplica SEMPRE no schema efetivamente clonado (public/homolog); delta pré-fix demonstrado."
