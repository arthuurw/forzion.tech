#!/bin/bash
# Gera o spec OpenAPI (swagger v1) do backend de forma offline, sem subir Kestrel
# nem banco. Usa a CLI do Swashbuckle (`swagger tofile`), que carrega o container
# de DI da app e serializa o documento.
#
# Truque de ambiente:
#   - ASPNETCORE_ENVIRONMENT=Test  -> pula AddInfrastructure (sem DB / connection string).
#   - Auth__JwtSecret=<>=32 bytes> -> AddJwtAuthentication exige um secret válido em
#     qualquer ambiente; aqui usamos um placeholder só para o registro de serviços passar.
#
# Uso: bash scripts/gen-swagger.sh [arquivo-de-saida]
# Default: docs/api/swagger.v1.json
#
# O job `openapi-drift` do CI roda este script e falha se o resultado divergir do
# arquivo versionado (drift não coordenado). Regenere conscientemente ao mudar a API.

set -euo pipefail

OUTPUT="${1:-docs/api/swagger.v1.json}"
API_DLL="forzion.tech.Api/bin/Release/net8.0/forzion.tech.Api.dll"
SWAGGER_DOC="v1"

# Placeholder de 32+ bytes — nunca usado para assinar nada real, só satisfaz a
# validação de AddJwtAuthentication durante a geração do spec.
export ASPNETCORE_ENVIRONMENT=Test
export Auth__JwtSecret="ci-swagger-generation-placeholder-secret-0123456789"

if [ ! -f "$API_DLL" ]; then
  echo "::error::$API_DLL não encontrado. Rode 'dotnet build -c Release' antes." >&2
  exit 1
fi

# Garante a CLI pinada. Idempotente: ignora 'already installed'.
dotnet tool install --global Swashbuckle.AspNetCore.Cli --version 6.6.2 2>/dev/null || true
export PATH="$PATH:$HOME/.dotnet/tools"

mkdir -p "$(dirname "$OUTPUT")"
swagger tofile --output "$OUTPUT" "$API_DLL" "$SWAGGER_DOC"

echo "OpenAPI spec gerado em $OUTPUT"
