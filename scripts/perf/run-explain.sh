#!/usr/bin/env bash
# Roda explain-targets.sql 2× com toggle do índice composto execucoes_treino
# (aluno_id, data_execucao DESC) e captura ANTES/DEPOIS.
# Estado inicial esperado = homolog: índice single-col ix_execucoes_treino_aluno_id.
# Restaura o estado single-col ao final → reexecutável.
#
# Uso:  scripts/perf/run-explain.sh [outdir]
# Env:  CONTAINER (default forzion-perfbench), DB (default forzion_bench),
#       PGUSER (default postgres), SCHEMA (default perf_bench)
set -euo pipefail

CONTAINER="${CONTAINER:-forzion-perfbench}"
DB="${DB:-forzion_bench}"
PGUSER="${PGUSER:-postgres}"
SCHEMA="${SCHEMA:-perf_bench}"
OUTDIR="${1:-$(dirname "$0")/out}"
HERE="$(dirname "$0")"
mkdir -p "$OUTDIR"

psql() { docker exec -i -e PGOPTIONS="--search_path=$SCHEMA" "$CONTAINER" psql -U "$PGUSER" -d "$DB" "$@"; }

echo ">> ANTES (single-col ix_execucoes_treino_aluno_id)"
psql -q -f - < "$HERE/explain-targets.sql" > "$OUTDIR/explain-antes.txt" 2>&1

echo ">> toggle: drop single-col, create composite (aluno_id, data_execucao DESC)"
psql -q -v ON_ERROR_STOP=1 <<SQL
DROP INDEX IF EXISTS ix_execucoes_treino_aluno_id;
CREATE INDEX IF NOT EXISTS ix_execucoes_treino_aluno_id_data_execucao
  ON execucoes_treino (aluno_id, data_execucao DESC);
ANALYZE execucoes_treino;
SQL

echo ">> DEPOIS (composto)"
psql -q -f - < "$HERE/explain-targets.sql" > "$OUTDIR/explain-depois.txt" 2>&1

echo ">> restaura estado single-col (idempotência)"
psql -q -v ON_ERROR_STOP=1 <<SQL
DROP INDEX IF EXISTS ix_execucoes_treino_aluno_id_data_execucao;
CREATE INDEX IF NOT EXISTS ix_execucoes_treino_aluno_id
  ON execucoes_treino (aluno_id);
ANALYZE execucoes_treino;
SQL

echo ">> pronto: $OUTDIR/explain-antes.txt  $OUTDIR/explain-depois.txt"
