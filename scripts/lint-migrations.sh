#!/usr/bin/env bash
# R5 (deploy-safety): lint de migration arriscada. Varre migrations e sinaliza padrões
# data-dependentes que o CI em DB VAZIO (Testcontainers) nunca pega, mas que quebram contra o
# schema homolog populado:
#   - CreateIndex(..., unique: true)        → falha se já houver linhas duplicadas
#   - AddColumn(..., nullable: false, ...)  sem defaultValue → falha se a tabela tiver linhas
#   - AlterColumn(..., nullable: false, ...) sem defaultValue → falha se houver NULLs
# Achado exige justificativa/dedup-step: adicione um comentário `lint-migrations:allow` no
# arquivo da migration (depois de garantir o dedup/backfill) para liberar.
#
# Uso:
#   scripts/lint-migrations.sh                      # arquivos ADICIONADOS no PR (diff vs BASE_REF)
#   scripts/lint-migrations.sh path/Migr.cs ...     # arquivos explícitos (usado nos testes)
set -euo pipefail

# Coleta os arquivos-alvo: explícitos (args) ou os adicionados no diff vs base.
files=()
if [ "$#" -gt 0 ]; then
  files=("$@")
else
  base="${BASE_REF:-origin/homolog}"
  while IFS= read -r f; do
    [ -n "$f" ] && files+=("$f")
  done < <(git diff --name-only --diff-filter=A "$base...HEAD" -- '*/Migrations/*.cs' \
            | grep -vE '\.Designer\.cs$|ModelSnapshot\.cs$' || true)
fi

[ "${#files[@]}" -eq 0 ] && { echo "lint-migrations: nenhuma migration nova para checar."; exit 0; }

rc=0
for f in "${files[@]}"; do
  [ -f "$f" ] || { echo "lint-migrations: arquivo inexistente: $f" >&2; rc=1; continue; }
  # Justificativa explícita do autor libera o arquivo inteiro.
  grep -q 'lint-migrations:allow' "$f" && continue

  # Quebra o arquivo em "statements" por chamada migrationBuilder.<X>(...) e normaliza espaços,
  # já que EF gera as chamadas multi-linha (o padrão arriscado fica numa linha separada).
  while IFS= read -r stmt; do
    case "$stmt" in
      CreateIndex*unique:*true*)
        echo "$f: CreateIndex UNIQUE — falha sobre linhas duplicadas pré-existentes; dedup antes ou justifique."
        rc=1 ;;
      AddColumn*nullable:*false*)
        printf '%s' "$stmt" | grep -q 'defaultValue' || {
          echo "$f: AddColumn NOT NULL sem defaultValue — falha em tabela já populada."
          rc=1 ; } ;;
      AlterColumn*nullable:*false*)
        printf '%s' "$stmt" | grep -q 'defaultValue' || {
          echo "$f: AlterColumn -> NOT NULL sem defaultValue — falha se houver NULLs."
          rc=1 ; } ;;
    esac
  done < <(awk 'BEGIN{RS="migrationBuilder\\."} {gsub(/\n/," "); gsub(/ +/," "); print}' "$f")
done

[ "$rc" -eq 0 ] && echo "lint-migrations: OK (nenhum padrão arriscado)."
exit "$rc"
