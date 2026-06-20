#!/bin/bash
# Gera o spec OpenAPI (v1) do backend de forma offline, sem subir Kestrel nem banco.
# Usa a geração build-time nativa do ASP.NET (Microsoft.Extensions.ApiDescription.Server):
# o alvo GetDocument carrega o container de DI da app e serializa o documento.
#
# Truque de ambiente:
#   - ASPNETCORE_ENVIRONMENT=Test  -> pula AddInfrastructure (sem DB / connection string).
#   - Auth__JwtSecret=<>=32 bytes> -> AddJwtAuthentication exige um secret válido em
#     qualquer ambiente; aqui usamos um placeholder só para o registro de serviços passar.
#   - GenerateOpenApi=true         -> liga OpenApiGenerateDocuments (desligado por padrão
#     para não rodar GetDocument em todo build).
#
# Uso: bash scripts/gen-swagger.sh [arquivo-de-saida]
# Default: docs/api/swagger.v1.json
#
# O job `openapi-drift` do CI roda este script e falha se o resultado divergir do
# arquivo versionado (drift não coordenado). Regenere conscientemente ao mudar a API.

set -euo pipefail

OUTPUT="${1:-docs/api/swagger.v1.json}"
PROJECT="forzion.tech.Api/forzion.tech.Api.csproj"
GEN_DIR="artifacts/openapi"
GEN_FILE="$GEN_DIR/forzion.tech.Api.json"

# Placeholder de 32+ bytes — nunca usado para assinar nada real, só satisfaz a
# validação de AddJwtAuthentication durante a geração do spec.
export ASPNETCORE_ENVIRONMENT=Test
export Auth__JwtSecret="ci-openapi-generation-placeholder-secret-0123456789"

rm -rf "$GEN_DIR"
dotnet build "$PROJECT" -c Release -p:GenerateOpenApi=true --no-incremental

if [ ! -f "$GEN_FILE" ]; then
  echo "::error::$GEN_FILE não gerado. Verifique OpenApiGenerateDocuments / GetDocument." >&2
  exit 1
fi

mkdir -p "$(dirname "$OUTPUT")"
cp "$GEN_FILE" "$OUTPUT"

echo "OpenAPI spec gerado em $OUTPUT"
