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
  # Valida que a base resolve ANTES de concluir "nada a checar": se a ref não foi fetchada
  # (`bad revision`), o git diff sairia vazio e o gate passaria em silêncio — falso negativo.
  git rev-parse --verify --quiet "$base^{commit}" >/dev/null || {
    echo "lint-migrations: base ref '$base' não resolve (fetch faltando?). Abortando para não pular o gate." >&2
    exit 2
  }
  # Captura o diff com rc próprio (sem mascarar falha do git num `|| true`); só DEPOIS filtra.
  diff_out="$(git diff --name-only --diff-filter=A "$base...HEAD" -- '*/Migrations/*.cs')" || {
    echo "lint-migrations: git diff falhou para '$base...HEAD'. Abortando." >&2
    exit 2
  }
  while IFS= read -r f; do
    [ -n "$f" ] || continue
    case "$f" in *.Designer.cs|*ModelSnapshot.cs) continue ;; esac
    files+=("$f")
  done <<< "$diff_out"
fi

[ "${#files[@]}" -eq 0 ] && { echo "lint-migrations: nenhuma migration nova para checar."; exit 0; }

rc=0
for f in "${files[@]}"; do
  [ -f "$f" ] || { echo "lint-migrations: arquivo inexistente: $f" >&2; rc=1; continue; }
  # Justificativa explícita do autor libera o arquivo inteiro.
  grep -q 'lint-migrations:allow' "$f" && continue

  # Quebra o arquivo em "statements" por chamada migrationBuilder.<X>(...) e normaliza espaços,
  # já que EF gera as chamadas multi-linha (o padrão arriscado fica numa linha separada).
  # nocasematch: DDL cru em Sql(...) pode vir minúsculo ("create unique index").
  shopt -s nocasematch
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
      # DDL cru via Sql(...) escapa do schema tipado do EF — varre o texto SQL direto.
      Sql*CREATE\ UNIQUE\ INDEX*|Sql*ADD\ CONSTRAINT*UNIQUE*)
        echo "$f: Sql() com CREATE UNIQUE INDEX/UNIQUE — falha sobre duplicatas pré-existentes; dedup antes ou justifique."
        rc=1 ;;
      Sql*NOT\ NULL*)
        echo "$f: Sql() com NOT NULL — em tabela populada exige default/backfill; justifique (lint-migrations:allow) se seguro."
        rc=1 ;;
    esac
  done < <(awk 'BEGIN{RS="migrationBuilder\\."} {gsub(/\n/," "); gsub(/ +/," "); print}' "$f")
  shopt -u nocasematch
done

[ "$rc" -eq 0 ] && echo "lint-migrations: OK (nenhum padrão arriscado)."
exit "$rc"
